﻿using System;
using System.Threading;
using System.Threading.Tasks;
using Cactus.Mongo.Migration.Model;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;

namespace Cactus.Mongo.Migration.Test
{
    internal class UpgradeStub : IUpgradeLink
    {
        private volatile int _execCounter;
        private readonly Func<IClientSessionHandle, IMongoDatabase, ILogger, Task> _upgrade;

        public int ExecCounter => _execCounter;
        public UpgradeStub(string from, string to) : this(from, to, (s, d, l) => Task.CompletedTask)
        {
            MinFrom = from == null ? null : Version.Parse(from);
            UpgradeTo = Version.Parse(to);
        }

        public UpgradeStub(string from, string to, Func<IClientSessionHandle, IMongoDatabase, ILogger, Task> upgrade)
        {
            _upgrade = upgrade;
            MinFrom = from == null ? null : Version.Parse(from);
            UpgradeTo = Version.Parse(to);
        }

        public Version MinFrom { get; protected set; }

        public Version UpgradeTo { get; protected set; }

        public Task Apply(IClientSessionHandle session, IMongoDatabase db, ILogger log)
        {
            Interlocked.Increment(ref _execCounter);
            return _upgrade(session, db, log);
        }
    }
}
