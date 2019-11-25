using System;

namespace Cactus.Mongo.Migration.Model
{
    public interface IUpgradeSettings
    {
        string VersionCollectionName { get; }
        bool IsTransactionRequired { get; }
        TimeSpan DistributedLockTimeout { get; }
    }

    public class UpgradeSettings : IUpgradeSettings
    {
        protected static readonly string DefaultVersionCollectionName = "ver";
        protected static readonly TimeSpan DefaultDistributedLockTimeout = TimeSpan.FromSeconds(10);
        public UpgradeSettings()
        {
            VersionCollectionName = DefaultVersionCollectionName;
            DistributedLockTimeout = DefaultDistributedLockTimeout;
        }
        public string VersionCollectionName { get; set; }

        public bool IsTransactionRequired { get; set; }

        public TimeSpan DistributedLockTimeout { get; set; }
    }
}
