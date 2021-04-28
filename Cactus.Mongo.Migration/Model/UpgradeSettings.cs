using System;

namespace Cactus.Mongo.Migration.Model
{
    public interface IUpgradeSettings
    {
        string VersionCollectionName { get; }
        bool IsTransactionRequired { get; }
        TimeSpan DistributedLockTimeout { get; }

        /// <summary>
        /// Define empty db initiation strategy.
        /// If true, run init & the whole upgrade chain on empty database to upgrade it to the last version.
        /// This strategy is quite similar with EF migrations when all migrations are applied one by one even if db is just created.
        /// If false (default), apply only init script on empty db and then set up the latest db version in the chain.
        /// In this case upgrades is ONLY upgrades to existed db & its data. Init should initiate db completely.
        /// </summary>
        bool ExecWholeChainOnInit { get; }
    }

    public class UpgradeSettings : IUpgradeSettings
    {
        protected static readonly string DefaultVersionCollectionName = "ver";
        protected static readonly TimeSpan DefaultDistributedLockTimeout = TimeSpan.FromSeconds(10);
        private static readonly Lazy<UpgradeSettings> DefaultLazy = new Lazy<UpgradeSettings>(() => new UpgradeSettings());

        public static UpgradeSettings Default => DefaultLazy.Value;

        public UpgradeSettings()
        {
            VersionCollectionName = DefaultVersionCollectionName;
            DistributedLockTimeout = DefaultDistributedLockTimeout;
        }
        public string VersionCollectionName { get; set; }

        public bool IsTransactionRequired { get; set; }

        public TimeSpan DistributedLockTimeout { get; set; }
        public bool ExecWholeChainOnInit { get; set; }
    }
}
