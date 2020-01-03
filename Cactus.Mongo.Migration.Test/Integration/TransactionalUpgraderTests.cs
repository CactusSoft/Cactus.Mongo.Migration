using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Cactus.Mongo.Migration.Model;
using Microsoft.Extensions.Logging.Abstractions;
using Mongo2Go;
using MongoDB.Bson;
using MongoDB.Driver;
using Moq;
using NUnit.Framework;
using Task = System.Threading.Tasks.Task;

namespace Cactus.Mongo.Migration.Test.Integration
{
    [TestFixture]
    public class TransactionalUpgraderTests
    {
        private MongoDbRunner _runner;
        private IMongoDatabase _database;
        private MongoClient _client;
        private UpgradeSettings _settings;

        [OneTimeSetUp]
        public void SetUp()
        {
            // All these means nothing to upgrade
            _runner = MongoDbRunner.Start();
            _client = new MongoClient(_runner.ConnectionString);
            _database = _client.GetDatabase("intergationtest");
            _settings = UpgradeSettings.Default;

        }

        [OneTimeTearDown]
        public void Teardown()
        {
            _client.DropDatabase("intergationtest");
            _runner.Dispose();
        }

        [Test]
        public void ThrowsIfTransactionSupportRequiredTest()
        {
            var dbLockMoq = new Mock<IDbLock>();
            dbLockMoq.Setup(e => e.ObtainLock(It.IsAny<TimeSpan>())).Returns(Task.FromResult(
                (IDbLockState)new DbVersion
                {
                    IsLocked = true,
                    Id = ObjectId.GenerateNewId().ToString()
                }));

            var trackerMoq = new Mock<IMigrationTracker>();
            trackerMoq.Setup(e => e.GetState()).Returns(Task.FromResult(
                (IMigrationState)new DbVersion
                {
                    AutoUpgradeEnabled = true
                }));
            var upgrades = new MigrationChain(new List<IUpgradeLink>
            {
                new UpgradeStub("0.0", "0.1"),
                new UpgradeStub("0.1", "0.2"),
                new UpgradeStub("0.2", "0.3"),
            });
            var upgrader = new MongoMigrator(
                _database,
                upgrades,
                null,
                new UpgradeSettings { IsTransactionRequired = true },
                dbLockMoq.Object,
                trackerMoq.Object,
                new NullLoggerFactory());

            var ex = Assert.ThrowsAsync<MigrationException>(upgrader.UpgradeOrInit);
            Assert.IsTrue(ex.Message.Contains("transactions are not available", StringComparison.OrdinalIgnoreCase),
                ex.Message);
        }

        [Test]
        public void ThrowsIfAutoupgradeIsDisabledTest()
        {
            var dbLockMoq = new Mock<IDbLock>();
            dbLockMoq.Setup(e => e.ObtainLock(It.IsAny<TimeSpan>())).Returns(Task.FromResult(
                (IDbLockState)new DbVersion
                {
                    IsLocked = true,
                    Id = ObjectId.GenerateNewId().ToString()
                }));

            var trackerMoq = new Mock<IMigrationTracker>();
            trackerMoq.Setup(e => e.GetState()).Returns(Task.FromResult(
                (IMigrationState)new DbVersion
                {
                    AutoUpgradeEnabled = false
                }));
            var upgrades = new MigrationChain(new List<IUpgradeLink>
            {
                new UpgradeStub("0.0", "0.1"),
                new UpgradeStub("0.1", "0.2"),
                new UpgradeStub("0.2", "0.3"),
            });
            var upgrader = new MongoMigrator(
                _database,
                upgrades,
                null,
                new UpgradeSettings(),
                dbLockMoq.Object,
                trackerMoq.Object,
                new NullLoggerFactory());

            var ex = Assert.ThrowsAsync<MigrationException>(upgrader.UpgradeOrInit);
            Assert.IsTrue(ex.Message.Contains("autoupgrade is disabled", StringComparison.OrdinalIgnoreCase),
                ex.Message);
        }

        [Test]
        public async Task EmptyUpgradeListTest()
        {
            var settings = new UpgradeSettings();
            var verCollection = _database.GetCollection<DbVersion>(settings.VersionCollectionName);
            await _database.DropCollectionAsync(settings.VersionCollectionName);
            var dbLock = new MongoDbLock(_settings,_database);
            var tracker = new MongoMigrationTracker(_settings, _database);

            var upgrader = new MongoMigrator(
                _database,
                new MigrationChain(null),
                null,
                settings,
                dbLock,
                tracker,
                new NullLoggerFactory());

            await upgrader.UpgradeOrInit();
            var ver = (await verCollection.FindAsync(e => true)).First();
            Assert.IsNull(ver.Version);
            Assert.IsTrue(ver.AutoUpgradeEnabled);
        }

        [Test]
        public async Task InitToTheLastVersionTest()
        {
            var settings = new UpgradeSettings();
            var verCollection = _database.GetCollection<DbVersion>(settings.VersionCollectionName);
            await _database.DropCollectionAsync(settings.VersionCollectionName);
            var dbLock = new MongoDbLock(_settings,_database);
            var tracker = new MongoMigrationTracker(_settings, _database);
            var upgrades = new MigrationChain(new List<IUpgradeLink>
            {
                new UpgradeStub("0.0", "0.1"),
                new UpgradeStub("0.1", "0.2"),
                new UpgradeStub("0.2", "0.3"),
            });
            var upgrader = new MongoMigrator(
                _database,
                upgrades,
                null,
                settings,
                dbLock,
                tracker,
                new NullLoggerFactory());

            await upgrader.UpgradeOrInit();
            var ver = (await verCollection.FindAsync(e => true)).First();
            Assert.AreEqual(Version.Parse("0.3"), ver.Version);
            Assert.IsTrue(ver.AutoUpgradeEnabled);
            Assert.IsNull(ver.LastUpgradeError);
        }

        [Test]
        public async Task InitFailTest()
        {
            var settings = new UpgradeSettings();
            var verCollection = _database.GetCollection<DbVersion>(settings.VersionCollectionName);
            await _database.DropCollectionAsync(settings.VersionCollectionName);
            var dbLock = new MongoDbLock(_settings,_database);
            var tracker = new MongoMigrationTracker(_settings, _database);
            var upgrades = new MigrationChain(new List<IUpgradeLink>
            {
                new UpgradeStub("0.0", "0.1"),
                new UpgradeStub("0.1", "0.2"),
                new UpgradeStub("0.2", "0.3"),
            });
            var upgrader = new MongoMigrator(
                _database,
                upgrades,
                new UpgradeStub(null, "0.0", (s, db, log) => throw new Exception("test init failed")),
                settings,
                dbLock,
                tracker,
                new NullLoggerFactory());

            var ex = Assert.ThrowsAsync<MigrationException>(upgrader.UpgradeOrInit);
            Assert.IsNotNull(ex.InnerException);
            Assert.AreEqual("test init failed", ex.InnerException?.Message);

            var ver = (await verCollection.FindAsync(e => true)).First();
            Assert.IsNull(ver.Version);
            Assert.IsFalse(ver.AutoUpgradeEnabled);
            Assert.IsNotNull(ver.LastUpgradeError);
            Assert.IsTrue(ver.LastUpgradeError.Contains("test init failed"));
        }

        [Test]
        public async Task UpgradeTest()
        {
            var settings = new UpgradeSettings();
            var verCollection = _database.GetCollection<DbVersion>(settings.VersionCollectionName);
            await _database.DropCollectionAsync(settings.VersionCollectionName);
            var dbLock = new MongoDbLock(_settings,_database);
            var tracker = new MongoMigrationTracker(_settings, _database);

            //Step1: init db
            var upgrader = new MongoMigrator(
                _database,
                new MigrationChain(null),
                new UpgradeStub(null, "0.0"),
                settings,
                dbLock,
                tracker,
                new NullLoggerFactory());
            await upgrader.UpgradeOrInit();

            var ver = (await verCollection.FindAsync(e => true)).First();
            Assert.AreEqual(Version.Parse("0.0"), ver.Version);

            //Step2: Apply upgrade chain
            var upgrades = new MigrationChain(new List<IUpgradeLink>
            {
                new UpgradeStub("0.0", "0.1"),
                new UpgradeStub("0.1", "0.2"),
                new UpgradeStub("0.2", "0.3"),
            });

            upgrader = new MongoMigrator(
                _database,
                upgrades,
                new UpgradeStub(null, "0.0", (s, db, log) => throw new Exception("test init failed")),
                settings,
                dbLock,
                tracker,
                new NullLoggerFactory());
            await upgrader.UpgradeOrInit();

            ver = (await verCollection.FindAsync(e => true)).First();
            Assert.AreEqual(Version.Parse("0.3"), ver.Version);
            Assert.IsTrue(ver.AutoUpgradeEnabled);
        }

        [Test]
        public async Task UpgradeWithDifferentVerCollectionNameTest()
        {
            var settings = new UpgradeSettings { VersionCollectionName = "testtestete" };
            var verCollection = _database.GetCollection<DbVersion>(settings.VersionCollectionName);
            await _database.DropCollectionAsync(settings.VersionCollectionName);
            var dbLock = new MongoDbLock(settings, _database);
            var tracker = new MongoMigrationTracker(settings, _database);

            //Step1: init db
            var upgrader = new MongoMigrator(
                _database,
                new MigrationChain(null),
                new UpgradeStub(null, "0.0"),
                settings,
                dbLock,
                tracker,
                new NullLoggerFactory());
            await upgrader.UpgradeOrInit();

            var ver = (await verCollection.FindAsync(e => true)).First();
            Assert.AreEqual(Version.Parse("0.0"), ver.Version);

            //Step2: Apply upgrade chain
            var upgrades = new MigrationChain(new List<IUpgradeLink>
            {
                new UpgradeStub("0.0", "0.1"),
                new UpgradeStub("0.1", "0.2"),
                new UpgradeStub("0.2", "0.3"),
            });

            upgrader = new MongoMigrator(
                _database,
                upgrades,
                new UpgradeStub(null, "0.0", (s, db, log) => throw new Exception("test init failed")),
                settings,
                dbLock,
                tracker,
                new NullLoggerFactory());
            await upgrader.UpgradeOrInit();

            ver = (await verCollection.FindAsync(e => true)).First();
            Assert.AreEqual(Version.Parse("0.3"), ver.Version);
            Assert.IsTrue(ver.AutoUpgradeEnabled);
        }

        [Test]
        public async Task UpgradeToLatestAvailableVersionTest()
        {
            var settings = new UpgradeSettings();
            var verCollection = _database.GetCollection<DbVersion>(settings.VersionCollectionName);
            await _database.DropCollectionAsync(settings.VersionCollectionName);
            var dbLock = new MongoDbLock(_settings,_database);
            var tracker = new MongoMigrationTracker(_settings, _database);

            //Step2: Apply upgrade chain
            var upgrades = new MigrationChain(new List<IUpgradeLink>
            {
                new UpgradeStub("0.0", "0.1"),
                new UpgradeStub("0.1", "0.2"),
                new UpgradeStub("0.2", "0.3"),
            });

            var upgrader = new MongoMigrator(
                _database,
                upgrades,
                new UpgradeStub(null, "0.0"),
                settings,
                dbLock,
                tracker,
                new NullLoggerFactory());
            await upgrader.UpgradeOrInit();

            var ver = (await verCollection.FindAsync(e => true)).First();
            Assert.AreEqual(Version.Parse("0.3"), ver.Version, "After init, DB should marked with the latest version");
            Assert.IsTrue(ver.AutoUpgradeEnabled);
        }
    }
}