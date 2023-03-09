﻿using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using BirdsiteLive.Pipeline.Models;
using BirdsiteLive.Pipeline.Contracts;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace BirdsiteLive.Pipeline.Tests
{
    [TestClass]
    public class StatusPublicationPipelineTests
    {
        [TestMethod]
        public async Task ExecuteAsync_Test()
        {
            #region Stubs
            var ct = new CancellationTokenSource(10);
            #endregion

            #region Mocks

            var retrieveTwitterUserProcessor = new Mock<IRetrieveTwitterUsersProcessor>(MockBehavior.Strict);
            retrieveTwitterUserProcessor
                .Setup(x => x.GetTwitterUsersAsync(
                    It.IsAny<BufferBlock<UserWithDataToSync[]>>(),
                    It.IsAny<CancellationToken>()))
                .Returns(Task.Delay(0));
            var retrieveTweetsProcessor = new Mock<IRetrieveTweetsProcessor>(MockBehavior.Strict);
            var retrieveFollowersProcessor = new Mock<IRetrieveFollowersProcessor>(MockBehavior.Strict);
            var sendTweetsToFollowersProcessor = new Mock<ISendTweetsToFollowersProcessor>(MockBehavior.Strict);
            var saveProgressionProcessor = new Mock<ISaveProgressionTask>(MockBehavior.Strict);
            var logger = new Mock<ILogger<StatusPublicationPipeline>>();
            #endregion

            var pipeline = new StatusPublicationPipeline(retrieveTweetsProcessor.Object, retrieveTwitterUserProcessor.Object, retrieveFollowersProcessor.Object, sendTweetsToFollowersProcessor.Object, saveProgressionProcessor.Object, logger.Object);
            await pipeline.ExecuteAsync(ct.Token);

            #region Validations
            retrieveTweetsProcessor.VerifyAll();
            retrieveFollowersProcessor.VerifyAll();
            sendTweetsToFollowersProcessor.VerifyAll();
            saveProgressionProcessor.VerifyAll();
            logger.VerifyAll();
            #endregion
        }
    }
}