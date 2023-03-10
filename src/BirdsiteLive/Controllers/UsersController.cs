using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Mime;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using BirdsiteLive.ActivityPub;
using BirdsiteLive.ActivityPub.Models;
using BirdsiteLive.Common.Regexes;
using BirdsiteLive.Common.Settings;
using BirdsiteLive.Domain;
using BirdsiteLive.Models;
using BirdsiteLive.Tools;
using BirdsiteLive.Twitter;
using BirdsiteLive.Twitter.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using Newtonsoft.Json;

namespace BirdsiteLive.Controllers
{
    public class UsersController : Controller
    {
        private readonly ITwitterUserService _twitterUserService;
        private readonly ICachedTwitterTweetsService _twitterTweetService;
        private readonly IUserService _userService;
        private readonly IStatusService _statusService;
        private readonly InstanceSettings _instanceSettings;
        private readonly ILogger<UsersController> _logger;

        #region Ctor
        public UsersController(ITwitterUserService twitterUserService, IUserService userService, IStatusService statusService, InstanceSettings instanceSettings, ICachedTwitterTweetsService twitterTweetService, ILogger<UsersController> logger)
        {
            _twitterUserService = twitterUserService;
            _userService = userService;
            _statusService = statusService;
            _instanceSettings = instanceSettings;
            _twitterTweetService = twitterTweetService;
            _logger = logger;
        }
        #endregion

        [Route("/users")]
        public IActionResult Index()
        {
            var acceptHeaders = Request.Headers["Accept"];
            if (acceptHeaders.Any())
            {
                var r = acceptHeaders.First();
                if (r.Contains("application/activity+json")) return NotFound();
            }
            return View("UserNotFound");
        }

        [Route("/@{id}")]
        [Route("/users/{id}")]
        [Route("/users/{id}/remote_follow")]
        public async Task<IActionResult> Index(string id)
        {
            _logger.LogTrace("User Index: {Id}", id);

            id = id.Trim(new[] { ' ', '@' }).ToLowerInvariant();

            TwitterUser user = null;
            var isSaturated = false;
            var notFound = false;

            // Ensure valid username 
            // https://help.twitter.com/en/managing-your-account/twitter-username-rules
            if (!string.IsNullOrWhiteSpace(id) && UserRegexes.TwitterAccount.IsMatch(id) && id.Length <= 15)
            {
                try
                {
                    user = await _twitterUserService.GetUserAsync(id);
                }
                catch (UserNotFoundException)
                {
                    notFound = true;
                }
                catch (UserHasBeenSuspendedException)
                {
                    notFound = true;
                }
                catch (RateLimitExceededException)
                {
                    isSaturated = true;
                }
                catch (Exception e)
                {
                    _logger.LogError(e, "Exception getting {Id}", id);
                    throw;
                }
            }
            else
            {
                notFound = true;
            }

            //var isSaturated = _twitterUserService.IsUserApiRateLimited();

            var acceptHeaders = Request.Headers["Accept"];
            if (acceptHeaders.Any())
            {
                var r = acceptHeaders.First();
                if (r.Contains("application/activity+json"))
                {
                    if (isSaturated) return new ObjectResult("Too Many Requests") { StatusCode = 429 };
                    if (notFound) return NotFound();
                    var apUser = _userService.GetUser(user);
                    var jsonApUser = JsonConvert.SerializeObject(apUser);
                    return Content(jsonApUser, "application/activity+json; charset=utf-8");
                }
            }

            if (isSaturated) return View("ApiSaturated");
            if (notFound) return View("UserNotFound");

            var displayableUser = new DisplayTwitterUser
            {
                Name = user.Name,
                Description = user.Description,
                Acct = user.Acct.ToLowerInvariant(),
                Url = user.Url,
                ProfileImageUrl = user.ProfileImageUrl,
                Protected = user.Protected,

                InstanceHandle = $"@{user.Acct.ToLowerInvariant()}@{_instanceSettings.Domain}"
            };
            return View(displayableUser);
        }

        [Route("/@{id}/{statusId}")]
        [Route("/users/{id}/statuses/{statusId}")]
        public async Task<IActionResult> Tweet(string id, string statusId)
        {
            var acceptHeaders = Request.Headers["Accept"];
            if (!long.TryParse(statusId, out var parsedStatusId))
                return NotFound();

            var tweet = await _twitterTweetService.GetTweetAsync(parsedStatusId);
            if (tweet == null)
                return NotFound();
            
            var user = await _twitterUserService.GetUserAsync(id);

            var status = _statusService.GetStatus(id, tweet);

            if (acceptHeaders.Any())
            {
                var r = acceptHeaders.First();

                if (r.Contains("application/activity+json"))
                {
                    var jsonApUser = JsonConvert.SerializeObject(status);
                    return Content(jsonApUser, "application/activity+json; charset=utf-8");
                }
            }

            //return Redirect($"https://twitter.com/{id}/status/{statusId}");
            var displayTweet = new DisplayTweet 
            {
                Text = tweet.MessageContent,
                OgUrl = $"https://twitter.com/{id}/status/{statusId}",
                UserProfileImage = user.ProfileImageUrl,
                UserName = user.Name,
            };
            return View(displayTweet);
        }

        [Route("/users/{id}/statuses/{statusId}/activity")]
        public async Task<IActionResult> Activity(string id, string statusId)
        {
            if (!long.TryParse(statusId, out var parsedStatusId))
                return NotFound();

            var tweet = await _twitterTweetService.GetTweetAsync(parsedStatusId);
            if (tweet == null)
                return NotFound();
            
            var user = await _twitterUserService.GetUserAsync(id);

            var status = _statusService.GetActivity(id, tweet);

            var jsonApUser = JsonConvert.SerializeObject(status);
            return Content(jsonApUser, "application/activity+json; charset=utf-8");
        }

        [Route("/users/{id}/inbox")]
        [HttpPost]
        public async Task<IActionResult> Inbox()
        {
            try
            {
                var r = Request;
                using (var reader = new StreamReader(Request.Body))
                {
                    var body = await reader.ReadToEndAsync();

                    _logger.LogTrace("User Inbox: {Body}", body);
                    //System.IO.File.WriteAllText($@"C:\apdebug\{Guid.NewGuid()}.json", body);

                    var activity = ApDeserializer.ProcessActivity(body);
                    var signature = r.Headers["Signature"].First();

                    switch (activity?.type)
                    {
                        case "Follow":
                        {
                            var succeeded = await _userService.FollowRequestedAsync(signature, r.Method, r.Path,
                                r.QueryString.ToString(), HeaderHandler.RequestHeaders(r.Headers),
                                activity as ActivityFollow, body);
                            if (succeeded) return Accepted();
                            else return Unauthorized();
                        }
                        case "Undo":
                            if (activity is ActivityUndoFollow)
                            {
                                var succeeded = await _userService.UndoFollowRequestedAsync(signature, r.Method, r.Path,
                                    r.QueryString.ToString(), HeaderHandler.RequestHeaders(r.Headers),
                                    activity as ActivityUndoFollow, body);
                                if (succeeded) return Accepted();
                                else return Unauthorized();
                            }

                            return Accepted();
                        case "Delete":
                        {
                            var succeeded = await _userService.DeleteRequestedAsync(signature, r.Method, r.Path,
                                r.QueryString.ToString(), HeaderHandler.RequestHeaders(r.Headers),
                                activity as ActivityDelete, body);
                            if (succeeded) return Accepted();
                            else return Unauthorized();
                        }
                        default:
                            return Accepted();
                    }
                }
            }
            catch (FollowerIsGoneException)  //TODO: check if user in DB
            {
                return Accepted();
            }
            catch (UserNotFoundException)
            {
                return NotFound();
            }
            catch (UserHasBeenSuspendedException)
            {
                return NotFound();
            }
            catch (RateLimitExceededException)
            {
                return new ObjectResult("Too Many Requests") { StatusCode = 429 };
            }
        }

        [Route("/users/{id}/followers")]
        [HttpGet]
        public IActionResult Followers(string id)
        {
            var r = Request.Headers["Accept"].First();
            if (!r.Contains("application/activity+json")) return NotFound();

            var followers = new Followers
            {
                id = $"https://{_instanceSettings.Domain}/users/{id}/followers"
            };
            var jsonApUser = JsonConvert.SerializeObject(followers);
            return Content(jsonApUser, "application/activity+json; charset=utf-8");
        }
    }
}