﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Varekai.Locker.RedisClients;
using Varekai.Utils;
using Varekai.Utils.Logging;

namespace Varekai.Locker
{
    public class LockingCoordinator : IDisposable
    {
        const string SuccessResult = "OK";
        const string FailResult = "FAIL";
        
        readonly CancellationTokenSource _lockAcquisitionCancellation;

        readonly ILogger _logger;
        readonly ReadOnlyCollection<LockingNode> _nodes;
        readonly Func<long> _timeProvider;
        readonly Func<LockingNode, IRedisClient> _redisClientFactory;
        readonly List<IRedisClient> _redisClients;

        LockingCoordinator(
            IEnumerable<LockingNode> nodes,
            Func<long> timeProvider,
            Func<LockingNode, IRedisClient> redisClientFactory,
            ILogger logger)
        {
            _logger = logger;
            _nodes = new ReadOnlyCollection<LockingNode>(nodes.ToArray());
            _timeProvider = timeProvider;
            _redisClientFactory = redisClientFactory;
            _lockAcquisitionCancellation = new CancellationTokenSource();

            _redisClients = _nodes
                            .Select(_redisClientFactory)
                            .ToList();
        }

        public static Task<LockingCoordinator> CreateNewForNodes(
            IEnumerable<LockingNode> nodes, 
            Func<long> timeProvider,
            ILogger logger)
        {
            return
                CreateNewForNodesWithClient(
                    nodes,
                    timeProvider,
                    nd => new ServiceStackClient(nd, () => SuccessResult, () => FailResult, logger),
                    logger);
        }

        public static async Task<LockingCoordinator> CreateNewForNodesWithClient(
            IEnumerable<LockingNode> nodes, 
            Func<long> timeProvider,
            Func<LockingNode, IRedisClient> redisClientFactory,
            ILogger logger)
        {
            var coordinator = new LockingCoordinator(
                nodes,
                timeProvider,
                redisClientFactory,
                logger);

            await TryConnectClients(coordinator);

            return coordinator;
        }

        static Task TryConnectClients(LockingCoordinator coordinator)
        {
            var connectActions = coordinator
                ._redisClients
                .Select(async cli => await cli.TryConnect());

            return TaskUtils.SilentlyCanceledWhenAll(connectActions);
        }

        public async Task<bool> TryAcquireLock(LockId lockId)
        {
            _logger.ToDebugLog("Trying to acquire the lock...");

            var startTime = _timeProvider();

            var lockAcquired = await TryAcquireLockOnAllNodes(lockId);

            var finishTime = _timeProvider();

            if (lockAcquired 
                && lockId.HasEnoughTimeBeforeExpire(startTime, finishTime)) return lockAcquired;

            if (!lockAcquired)
                _logger.ToDebugLog("Unable to acquire the lock. Releasing all...");
            else
                _logger.ToDebugLog("Lock correctly acquired but the time left to use it is not enough. Releasing all...");

            await TryReleaseTheLock(lockId);

            return false;
        }

        async Task<bool> TryAcquireLockOnAllNodes(LockId lockId)
        {
            return 
                _redisClients.Count != 0 
                &&
                await TryInParallelOnAllClients(async cli => await cli.Set(lockId));
        }

        public Task<bool> TryConfirmTheLock(LockId lockId)
        {
            return TryInParallelOnAllClients(async cli => await cli.Confirm(lockId));
        }

        public Task<bool> TryReleaseTheLock(LockId lockId)
        {
            _logger.ToDebugLog("Releasing the lock...");

            if (_redisClients == null 
                || _redisClients.Count == 0) return false.FromResult();

            return TryInParallelOnAllClients(async cli => await cli.Release(lockId));
        }

        async Task<bool> TryInParallelOnAllClients(Func<IRedisClient, Task<string>> operationOnClient)
        {
            var quorum = _nodes.CalculateQuorum();

            var sessions = 
                _redisClients
                .AsParallel()
                .Select(cli => TryOnClient(
                    async () => await operationOnClient(cli),
                    _logger));

            var succeded = await TaskUtils.SilentlyCanceledWhenAll(sessions);

            return 
                succeded.Count(r => r)
                >=
                quorum;
        }

        static async Task<bool> TryOnClient(Func<Task<string>> operationOnClient, ILogger logger)
        {
            try
            {
                var result = await operationOnClient();

                return 
                    result != null
                    &&
                    result.Equals(
                        SuccessResult,
                        StringComparison.InvariantCultureIgnoreCase);
            }
            catch (Exception ex)
            {
                logger.ToErrorLogDigest(ex);
                return false;
            }
        }

        public void Dispose()
        {
            if (!_lockAcquisitionCancellation.IsCancellationRequested)
                _lockAcquisitionCancellation.Cancel();

            if (_redisClients == null)
                return;
            
            if (_redisClients.Any())
            {
                foreach (var client in _redisClients)
                    client.Dispose();

                _redisClients.Clear();
            }
        }
    }
}