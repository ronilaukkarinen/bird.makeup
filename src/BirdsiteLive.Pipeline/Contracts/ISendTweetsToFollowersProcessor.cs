﻿using System.Threading;
using System.Threading.Tasks;
using BirdsiteLive.Pipeline.Models;

namespace BirdsiteLive.Pipeline.Contracts
{
    public interface ISendTweetsToFollowersProcessor
    {
        Task ProcessAsync(UserWithDataToSync userWithTweetsToSync, CancellationToken ct);
    }
}