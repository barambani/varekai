﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Moq;
using NUnit.Framework;
using Varekai.Utils.Logging;

namespace Varekai.Locker.Tests.Unit
{
    [TestFixture]
    public class LockingCoordinatorTests
    {
        [Test]
        [TestCase("OK", true)]
        [TestCase("ok", true)]
        [TestCase("AA", false)]
        [TestCase(null, false)]
        public async Task TryAcquireLockTests(string acquireRsult, bool expectedResult)
        {
            var coordinator = LockingCoordinator.CreateNewForNodesWithClient(
                CreateNodes(),
                () => DateTime.Now,
                CreateClientFactory(
                    lockId => acquireRsult,
                    lockId => "OK",
                    lockId => "OK"
                ),
                Mock.Of<ILogger>());

            var id = LockId.CreateNewFor("resource");

            Assert.AreEqual(expectedResult, await coordinator.TryAcquireLock(id));
        }

        [Test]
        [TestCase("OK", true)]
        [TestCase("ok", true)]
        [TestCase("AA", false)]
        [TestCase(null, false)]
        public async Task TryConfirmLockTests(string confirmRsult, bool expectedResult)
        {
            var coordinator = LockingCoordinator.CreateNewForNodesWithClient(
                CreateNodes(),
                () => DateTime.Now,
                CreateClientFactory(
                    lockId => "OK",
                    lockId => "OK",
                    lockId => confirmRsult
                ),
                Mock.Of<ILogger>());

            var id = LockId.CreateNewFor("resource");

            Assert.AreEqual(expectedResult, await coordinator.TryConfirmTheLock(id));
        }

        [Test]
        [TestCase("OK", true)]
        [TestCase("ok", true)]
        [TestCase("AA", false)]
        [TestCase(null, false)]
        public async Task TryReleaseLockTests(string releaseRsult, bool expectedResult)
        {
            var coordinator = LockingCoordinator.CreateNewForNodesWithClient(
                CreateNodes(),
                () => DateTime.Now,
                CreateClientFactory(
                    lockId => "OK",
                    lockId => releaseRsult,
                    lockId => "OK"
                ),
                Mock.Of<ILogger>());

            var id = LockId.CreateNewFor("resource");

            Assert.AreEqual(expectedResult, await coordinator.TryReleaseTheLock(id));
        }

        static Func<LockingNode, IRedisClient> CreateClientFactory(
            Func<LockId, string> setCalback,
            Func<LockId, string> releaseCalback,
            Func<LockId, string> confirmCalback)
        {
            var mockClient = new Mock<IRedisClient>();

            mockClient
                .Setup(cli => cli.Set(It.IsAny<LockId>()))
                .Returns<LockId>(setCalback);

            mockClient
                .Setup(cli => cli.Release(It.IsAny<LockId>()))
                .Returns<LockId>(releaseCalback);

            mockClient
                .Setup(cli => cli.Confirm(It.IsAny<LockId>()))
                .Returns<LockId>(confirmCalback);
            
            return node => mockClient.Object;
        }

        static IEnumerable<LockingNode> CreateNodes()
        {
            return new [] {
                LockingNode.CreateNew("localhost", 7001),
                LockingNode.CreateNew("localhost", 7002),
                LockingNode.CreateNew("localhost", 7003)
            };
        }
    }
}