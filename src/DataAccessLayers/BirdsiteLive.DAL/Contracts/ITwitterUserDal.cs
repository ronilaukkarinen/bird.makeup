﻿using System;
using System.Threading.Tasks;
using BirdsiteLive.DAL.Models;

namespace BirdsiteLive.DAL.Contracts
{
    public interface ITwitterUserDal
    {
        Task CreateTwitterUserAsync(string acct, long lastTweetPostedId);
        Task<SyncTwitterUser> GetTwitterUserAsync(string acct);
        Task<SyncTwitterUser> GetTwitterUserAsync(int id);
        Task<SyncTwitterUser[]> GetAllTwitterUsersWithFollowersAsync(int maxNumber);
        Task<SyncTwitterUser[]> GetAllTwitterUsersAsync(int maxNumber);
        Task<SyncTwitterUser[]> GetAllTwitterUsersAsync();
        Task UpdateTwitterUserAsync(int id, long lastTweetPostedId, long lastTweetSynchronizedForAllFollowersId, int fetchingErrorCount, DateTime lastSync);
        Task UpdateTwitterUserIdAsync(string username, long twitterUserId);
        Task UpdateTwitterUserAsync(SyncTwitterUser user);
        Task DeleteTwitterUserAsync(string acct);
        Task DeleteTwitterUserAsync(int id);
        Task<int> GetTwitterUsersCountAsync();
        Task<TimeSpan> GetTwitterSyncLag();
        Task<int> GetFailingTwitterUsersCountAsync();
    }
}