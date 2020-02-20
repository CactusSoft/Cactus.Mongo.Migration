using System;
using System.Threading.Tasks;
using Cactus.Mongo.Migration.Model;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using MongoDB.Driver.Core.Clusters;

namespace Cactus.Mongo.Migration
{
    public class TransactionalUpgrader : IUpgrader
    {
        private readonly ILogger<TransactionalUpgrader> _log;
        private readonly ILoggerFactory _logFactory;
        private readonly IMongoDatabase _db;
        private readonly IUpgradeChain _upgrades;
        private readonly IUpgrade _initializer;
        private readonly IUpgradeSettings _settings;
        private readonly IMongoCollection<DbVersion> _versionCollection;
        private readonly IDbLock _dbLock;
        private readonly bool _isTransactionsAvailable;
        private DbVersion _currentVersion;

        /// <summary>
        /// Only for test
        /// </summary>
        /// <param name="db"></param>
        /// <param name="upgrades"></param>
        /// <param name="initializer"></param>
        /// <param name="settings"></param>
        /// <param name="logFactory"></param>
        public TransactionalUpgrader(
            IMongoDatabase db,
            IUpgradeChain upgrades,
            IUpgrade initializer,
            IUpgradeSettings settings,
            ILoggerFactory logFactory)
        {
            _log = logFactory.CreateLogger<TransactionalUpgrader>();
            _logFactory = logFactory;
            _db = db;
            _upgrades = upgrades;
            _initializer = initializer;
            _settings = settings;
            _versionCollection = _db.GetCollection<DbVersion>(settings.VersionCollectionName);
            _dbLock = new MongoDbLock(_versionCollection);
            _isTransactionsAvailable = _db.Client.Cluster.Description.Type > ClusterType.Standalone;
        }

        public TransactionalUpgrader(
            IMongoDatabase db,
            IUpgradeChain upgrades,
            IUpgrade initializer,
            IUpgradeSettings settings,
            IDbLock dbLock,
            ILoggerFactory logFactory)
        {
            _log = logFactory.CreateLogger<TransactionalUpgrader>();
            _logFactory = logFactory;
            _db = db;
            _upgrades = upgrades;
            _initializer = initializer;
            _settings = settings;
            _versionCollection = _db.GetCollection<DbVersion>(settings.VersionCollectionName);
            _dbLock = dbLock;
            _isTransactionsAvailable = _db.Client.Cluster.Description.Type > ClusterType.Standalone;
        }

        protected virtual bool IsNewDatabase()
        {
            return _currentVersion.Version == null && _currentVersion.LastUpgradeError == null && _currentVersion.AutoUpgradeEnabled;
        }

        public async Task UpgradeOrInit()
        {
            _log.LogDebug("Validate upgrade chain...");
            _upgrades.Validate();

            if (_settings.IsTransactionRequired && !_isTransactionsAvailable)
            {
                throw new MongoMigrationException("Upgrade failed: transactions are not available on standalone server, but required by the config.");
            }

            try
            {
                _log.LogInformation("Try to obtain db lock...");
                _currentVersion = await _dbLock.ObtainLock(_settings.DistributedLockTimeout);

                _log.LogInformation("DB lock obtained, current ver: {0}, autoupgrade is {1}.", _currentVersion.Version, _currentVersion.AutoUpgradeEnabled ? "enabled" : "disabled");

                if (IsNewDatabase())
                {
                    if (!_settings.ExecWholeChainOnInit)
                    {
                        //Get max(init.ver, updates.lastVer)
                        var targetVersion = _initializer?.UpgradeTo ?? _upgrades.Target;
                        targetVersion = targetVersion < _upgrades.Target ? _upgrades.Target : targetVersion;
                        await Init(targetVersion);
                    }
                    else
                    {
                        if (_initializer != null)
                        {
                            await Init(_initializer.UpgradeTo);
                        }
                        await Upgrade();
                    }
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
            if (!_upgrades.HasAny(_currentVersion.Version))
            {
                _log.LogInformation("There's nothing to upgrade, finishing up...");
                return;
            }

            if (_currentVersion.AutoUpgradeEnabled == false)
            {
                _log.LogError("Upgrade failed: autoupgrade is disabled, that could mean error on the previous upgrades.");
                throw new MongoMigrationException("Upgrade failed: autoupgrade is disabled, that could mean error on the previous upgrades.");
            }

            foreach (var upgrade in _upgrades.GetUpgradePath(_currentVersion.Version))
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
                try
                {
                    if (_isTransactionsAvailable)
                    {
                        await session.WithTransactionAsync(async (s, c) =>
                        {
                            await upgrade.Apply(s, _db, _logFactory.CreateLogger(upgrade.GetType()));
                            await _versionCollection.UpdateOneAsync(s, e => e.Id == _currentVersion.Id, Builders<DbVersion>.Update.Set(e => e.Version, upgrade.UpgradeTo), cancellationToken: c);
                            return 0;
                        });
                    }
                    else
                    {
                        await upgrade.Apply(session, _db, _logFactory.CreateLogger(upgrade.GetType()));
                        await _versionCollection.UpdateOneAsync(session, e => e.Id == _currentVersion.Id, Builders<DbVersion>.Update.Set(e => e.Version, upgrade.UpgradeTo));
                    }
                }
                catch (Exception ex)
                {
                    if (_isTransactionsAvailable)
                    {
                        _log.LogError("Upgrade to {0} failed, tx has been rolled back", upgrade.UpgradeTo);
                    }
                    else
                    {
                        _log.LogCritical("Upgrade to {0} failed & transactions are not supported. DB may be inconsistent.", upgrade.UpgradeTo);
                    }

                    throw new MongoMigrationException($"Upgrade to ver {upgrade.UpgradeTo} failed.", ex);
                }
            }

            _currentVersion.Version = upgrade.UpgradeTo;
            _log.LogDebug("Upgrade success, current version is {0}", upgrade.UpgradeTo);
        }

        private async Task Init(Version target)
        {
            try
            {
                await Apply(new InitializerDecorator(_initializer, target));
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
                await _versionCollection.UpdateOneAsync(
                    e => e.Id == _currentVersion.Id,
                    Builders<DbVersion>.Update
                        .Set(e => e.AutoUpgradeEnabled, false)
                        .Set(e => e.LastUpgradeError, error));
            }
            catch
            {
                _log.LogError("Unable to write message about the error. I'm ignoring the issue to allow the prev exception pop up.");
            }
        }
    }
}
