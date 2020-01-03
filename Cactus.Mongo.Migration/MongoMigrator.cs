using System;
using System.Threading.Tasks;
using Cactus.Mongo.Migration.Model;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using MongoDB.Driver.Core.Clusters;

namespace Cactus.Mongo.Migration
{
    /// <summary>
    /// 
    /// </summary>
    public interface IMigrator
    {
        /// <summary>
        /// Perform DB upgrade if necessary 
        /// </summary>
        /// <returns></returns>
        Task UpgradeOrInit();
    }

    public class MongoMigrator : IMigrator
    {
        private readonly ILogger<MongoMigrator> _log;
        private readonly ILoggerFactory _logFactory;
        private readonly IMongoDatabase _db;
        private readonly IMigrationChain _upgrades;
        private readonly IUpgrade _initializer;
        private readonly IUpgradeSettings _settings;
        private readonly IDbLock _dbLock;
        private readonly IMigrationTracker _tracker;
        private readonly bool _isTransactionsAvailable;
        protected IMigrationState MigrationState;

        /// <summary>
        /// Only for test
        /// </summary>
        /// <param name="db"></param>
        /// <param name="dbLock"></param>
        /// <param name="upgrades"></param>
        /// <param name="initializer"></param>
        /// <param name="settings"></param>
        /// <param name="tracker"></param>
        /// <param name="logFactory"></param>
        public MongoMigrator(
            IMongoDatabase db,
            IMigrationChain upgrades,
            IUpgrade initializer,
            IUpgradeSettings settings,
            IDbLock dbLock,
            IMigrationTracker tracker,
            ILoggerFactory logFactory)
        {
            _log = logFactory.CreateLogger<MongoMigrator>();
            _logFactory = logFactory;
            _db = db;
            _upgrades = upgrades;
            _initializer = initializer;
            _settings = settings;
            _dbLock = dbLock;
            _tracker = tracker;
            _isTransactionsAvailable = _db.Client.Cluster.Description.Type > ClusterType.Standalone;
        }

        protected virtual bool IsNewDb()
        {
            return MigrationState.Version == null && MigrationState.LastUpgradeError == null && MigrationState.AutoUpgradeEnabled;
        }

        public async Task UpgradeOrInit()
        {
            _log.LogDebug("Validate upgrade chain...");
            _upgrades.Validate();

            if (_settings.IsTransactionRequired && !_isTransactionsAvailable)
            {
                throw new MigrationException("Upgrade failed: transactions are not available on standalone server, but required by the config.");
            }

            try
            {
                _log.LogInformation("Try to obtain db lock...");
                await _dbLock.ObtainLock(_settings.DistributedLockTimeout);
                MigrationState = await _tracker.GetState();

                _log.LogInformation("DB lock obtained, current ver: {0}, autoupgrade is {1}.", MigrationState.Version, MigrationState.AutoUpgradeEnabled ? "enabled" : "disabled");

                if (IsNewDb())
                {
                    _log.LogInformation("It new database, applying init...");
                    //Get max(init.ver, updates.lastVer)
                    var targetVersion = _initializer?.UpgradeTo ?? _upgrades.Target;
                    targetVersion = targetVersion < _upgrades.Target ? _upgrades.Target : targetVersion;

                    await Init(targetVersion);
                }
                else
                {
                    await Upgrade();
                }
            }
            finally
            {
                await _dbLock.ReleaseLock();
            }
        }

        private async Task Upgrade()
        {
            if (!_upgrades.HasAny(MigrationState.Version))
            {
                _log.LogInformation("There's nothing to upgrade, finishing up...");
                return;
            }

            if (MigrationState.AutoUpgradeEnabled == false)
            {
                _log.LogError("Upgrade failed: autoupgrade is disabled, that could mean error on the previous upgrades.");
                throw new MigrationException("Upgrade failed: autoupgrade is disabled, that could mean error on the previous upgrades.");
            }

            foreach (var upgrade in _upgrades.GetUpgradePath(MigrationState.Version))
            {
                _log.LogInformation("Start to upgrade to {0}...", upgrade.UpgradeTo);
                try
                {
                    await Apply(upgrade);
                }
                catch (Exception ex)
                {
                    await WriteUpgradeError(ex.ToString());
                    throw;
                }
                _log.LogInformation("{0} upgrade applied successfully", upgrade.UpgradeTo);
            }
        }

        protected virtual async Task Apply(IUpgrade upgrade)
        {
            _log.LogDebug("Upgrade to {0}", upgrade.UpgradeTo);
            using (var session = await _db.Client.StartSessionAsync())
            {
                if (_isTransactionsAvailable)
                    session.StartTransaction();
                try
                {
                    await upgrade.Apply(session, _db, _logFactory.CreateLogger(upgrade.GetType()));
                    await _tracker.TrackSuccessUpgrade(session, upgrade.UpgradeTo);
                    if (_isTransactionsAvailable)
                        await session.CommitTransactionAsync();
                }
                catch (Exception ex)
                {
                    if (_isTransactionsAvailable)
                    {
                        _log.LogError("Upgrade to {0} failed, rollback the transaction...", upgrade.UpgradeTo);
                        await session.AbortTransactionAsync();
                    }
                    else
                    {
                        _log.LogCritical("Upgrade to {0} failed & transactions are not supported. DB may be inconsistent.", upgrade.UpgradeTo);
                    }

                    throw new MigrationException($"Upgrade to ver {upgrade.UpgradeTo} failed.", ex);
                }
            }

            MigrationState = await _tracker.GetState();
            _log.LogDebug("Upgrade success, current version is {0}", upgrade.UpgradeTo);
        }

        private async Task Init(Version target)
        {
            try
            {
                await Apply(new InitDecorator(_initializer, target));
            }
            catch (Exception ex)
            {
                await WriteUpgradeError(ex.ToString());
                throw;
            }
        }

        private async Task WriteUpgradeError(string error)
        {
            try
            {
                await _tracker.TrackFail(error);
            }
            catch
            {
                _log.LogError("Unable to write message about the error. I'm ignoring the issue to allow the prev exception pop up.");
            }
        }
    }
}
