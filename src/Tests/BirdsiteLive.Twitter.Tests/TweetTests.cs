﻿using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;
using BirdsiteLive.Twitter;
using BirdsiteLive.Twitter.Tools;
using BirdsiteLive.Statistics.Domain;
using Moq;
using BirdsiteLive.DAL.Contracts;
using BirdsiteLive.Common.Settings;
using System.Net.Http;

namespace BirdsiteLive.ActivityPub.Tests
{
    [TestClass]
    public class TweetTests
    {
        private ITwitterTweetsService _tweetService;
        [TestInitialize]
        public async Task TestInit()
        {
            var logger1 = new Mock<ILogger<TwitterAuthenticationInitializer>>(MockBehavior.Strict);
            var logger2 = new Mock<ILogger<TwitterUserService>>(MockBehavior.Strict);
            var logger3 = new Mock<ILogger<TwitterTweetsService>>();
            var stats = new Mock<ITwitterStatisticsHandler>();
            var twitterDal = new Mock<ITwitterUserDal>();
            var httpFactory = new Mock<IHttpClientFactory>();
            httpFactory.Setup(_ => _.CreateClient(string.Empty)).Returns(new HttpClient());
            var settings = new InstanceSettings
            {
                Domain = "domain.name"
            };
            ITwitterAuthenticationInitializer auth = new TwitterAuthenticationInitializer(httpFactory.Object, logger1.Object);
            ITwitterUserService user = new TwitterUserService(auth, stats.Object, logger2.Object);
            ICachedTwitterUserService user2 = new CachedTwitterUserService(user, settings);
            _tweetService = new TwitterTweetsService(auth, stats.Object, user2, twitterDal.Object, settings, logger3.Object);
        }

        [TestMethod]
        public async Task SimpleTextTweet()
        {
            var tweet = await _tweetService.GetTweetAsync(1600905296892891149);
            Assert.AreEqual(tweet.MessageContent, "We’re strengthening American manufacturing by creating 750,000 manufacturing jobs since I became president.");
            Assert.AreEqual(tweet.Id, 1600905296892891149);
            Assert.IsFalse(tweet.IsRetweet);
            Assert.IsFalse(tweet.IsReply);
        }

        [TestMethod]
        public async Task SimpleTextAndSinglePictureTweet()
        {
            var tweet = await _tweetService.GetTweetAsync(1593344577385160704);
            Assert.AreEqual(tweet.MessageContent, "Speaker Nancy Pelosi will go down as one of most accomplished legislators in American history—breaking barriers, opening doors for others, and working every day to serve the American people. I couldn’t be more grateful for her friendship and leadership.");

            Assert.AreEqual(tweet.Media[0].MediaType, "image/jpeg");
            Assert.AreEqual(tweet.Media.Length, 1);
            // TODO test alt-text of images
        }

        [TestMethod]
        public async Task SimpleTextAndSingleLinkTweet()
        {
            var tweet = await _tweetService.GetTweetAsync(1602618920996945922);
            Assert.AreEqual(tweet.MessageContent, "#Linux 6.2 Expands Support For More #Qualcomm #Snapdragon SoCs, #Apple M1 Pro/Ultra/Max\n\nhttps://www.phoronix.com/news/Linux-6.2-Arm-SoC-Updates");
        }

        [TestMethod]
        public async Task SimpleTextAndSingleVideoTweet()
        {
            var tweet = await _tweetService.GetTweetAsync(1604231025311129600);
            Assert.AreEqual(tweet.MessageContent, "Falcon 9’s first stage has landed on the Just Read the Instructions droneship, completing the 15th launch and landing of this booster!");

            Assert.AreEqual(tweet.Media.Length, 1);
            Assert.AreEqual(tweet.Media[0].MediaType, "video/mp4");
            Assert.IsTrue(tweet.Media[0].Url.StartsWith("https://video.twimg.com/"));
        }

        [TestMethod]
        public async Task GifAndQT()
        {
            var tweet = await _tweetService.GetTweetAsync(1612901861874343936);
            // TODO test QT

            Assert.AreEqual(tweet.Media.Length, 1);
            Assert.AreEqual(tweet.Media[0].MediaType, "image/gif");
            Assert.IsTrue(tweet.Media[0].Url.StartsWith("https://video.twimg.com/"));
        }

        [TestMethod]
        public async Task SimpleQT()
        {
            var tweet = await _tweetService.GetTweetAsync(1610807139089383427);

            Assert.AreEqual(tweet.MessageContent, "When you gave them your keys you gave them your coins.\n\nhttps://domain.name/users/kadhim/statuses/1610706613207285773");
        }

        [TestMethod]
        public async Task SimpleThread()
        {
            var tweet = await _tweetService.GetTweetAsync(1445468404815597573);

            Assert.AreEqual(tweet.InReplyToAccount, "punk6529");
            Assert.AreEqual(tweet.InReplyToStatusId, 1445468401745289235);
            Assert.IsTrue(tweet.IsReply);
            Assert.IsTrue(tweet.IsThread);
        }

        [TestMethod]
        public async Task SimpleReply()
        {
            var tweet = await _tweetService.GetTweetAsync(1612622335546363904);

            Assert.AreEqual(tweet.InReplyToAccount, "DriveTeslaca");
            Assert.AreEqual(tweet.InReplyToStatusId, 1612610060194312193);
            Assert.IsTrue(tweet.IsReply);
            Assert.IsFalse(tweet.IsThread);
        }
    }
}
