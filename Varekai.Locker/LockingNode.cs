﻿namespace Varekai.Locker
{
    public struct LockingNode
    {
        public readonly string Host;
        public readonly long Port;

        public readonly long ConnectTimeoutMillis;
        public readonly long SyncOperationsTimeoutMillis;

        LockingNode(string host, long port) : this()
        {
            Host = host;
            Port = port;
            ConnectTimeoutMillis = 500;
            SyncOperationsTimeoutMillis = 50;
        }

        public static LockingNode CreateNew(string host, long port)
        {
            return new LockingNode(host, port);
        }

        public LockingNode ChangeHost(string host)
        {
            return new LockingNode(host, Port);
        }

        public LockingNode ChangePort(long port)
        {
            return new LockingNode(Host, port);
        }

        public string GetStackExchangeConnectionString()
        {
            return string.Format("{0}:{1},connectTimeout={2},syncTimeout={3}", Host, Port, ConnectTimeoutMillis, SyncOperationsTimeoutMillis);
        }

        public string GetServiceStackConnectionString()
        {
            return string.Format(
                "redis://{0}:{1}?ConnectTimeout={2}&SendTimeout={3}&ReceiveTimeout={3}",
                Host,
                Port,
                ConnectTimeoutMillis,
                SyncOperationsTimeoutMillis);
        }
    }
}