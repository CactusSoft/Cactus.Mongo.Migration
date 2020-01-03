using System;
using MongoDB.Bson;

namespace Cactus.Mongo.Migration.Model
{
    public interface IUpgradeSettings
    {
        string VersionCollectionName { get; }
        string VersionDocumentId { get; }
        bool IsTransactionRequired { get; }
        TimeSpan DistributedLockTimeout { get; }
        bool PreventUpgradesOnError { get; }
    }

    public class UpgradeSettings : IUpgradeSettings
    {
        protected static readonly string DefaultVersionCollectionName = "ver";
        private static readonly string DefaultVersionDocumentId = ObjectId.Empty.ToString();
        protected static readonly TimeSpan DefaultDistributedLockTimeout = TimeSpan.FromSeconds(10);
        private static readonly Lazy<UpgradeSettings> LazyDefault = new Lazy<UpgradeSettings>(() => new UpgradeSettings());

        public static UpgradeSettings Default => LazyDefault.Value;

        public UpgradeSettings()
        {
            VersionCollectionName = DefaultVersionCollectionName;
            DistributedLockTimeout = DefaultDistributedLockTimeout;
            VersionDocumentId = DefaultVersionDocumentId;
            PreventUpgradesOnError = true;
        }
        public string VersionCollectionName { get; set; }
        public string VersionDocumentId { get; set; }
        public bool IsTransactionRequired { get; set; }
        public TimeSpan DistributedLockTimeout { get; set; }
        public bool PreventUpgradesOnError { get; set; }
    }
}
