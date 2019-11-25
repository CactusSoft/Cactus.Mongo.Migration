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

namespace Cactus.Mongo.Migration.Test.Integration
{
    [TestFixture]
    public class TransactionalUpgraderTests
    {
        private MongoDbRunner _runner;
        private IMongoDatabase _database;
        private MongoClient _client;

        [OneTimeSetUp]
        public void SetUp()
        {
            // All these means nothing to upgrade
            _runner = MongoDbRunner.Start();
            _client = new MongoClient(_runner.ConnectionString);
            _database = _client.GetDatabase("intergationtest");

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
                new DbVersion
                {
                    AutoUpgradeEnabled = true,
                    IsLocked = true,
                    Id = ObjectId.GenerateNewId().ToString()
                }));
            var upgrades = new UpgradeChain(new List<IUpgradeLink>
            {
                new UpgradeStub("0.0", "0.1"),
                new UpgradeStub("0.1", "0.2"),
                new UpgradeStub("0.2", "0.3"),
            });
            var upgrader = new TransactionalUpgrader(
                _database,
                upgrades,
                null,
                new UpgradeSettings { IsTransactionRequired = true },
                dbLockMoq.Object, new NullLoggerFactory());

            var ex = Assert.ThrowsAsync<MongoMigrationException>(upgrader.UpgradeOrInit);
            Assert.IsTrue(ex.Message.Contains("transactions are not available", StringComparison.OrdinalIgnoreCase),
                ex.Message);
        }

        [Test]
        public void ThrowsIfAutoupgradeIsDisabledTest()
        {
            var dbLockMoq = new Mock<IDbLock>();
            dbLockMoq.Setup(e => e.ObtainLock(It.IsAny<TimeSpan>())).Returns(Task.FromResult(
                new DbVersion
                {
                    AutoUpgradeEnabled = false,
                    IsLocked = true,
                    Id = ObjectId.GenerateNewId().ToString()
                }));
            var upgrades = new UpgradeChain(new List<IUpgradeLink>
            {
                new UpgradeStub("0.0", "0.1"),
                new UpgradeStub("0.1", "0.2"),
                new UpgradeStub("0.2", "0.3"),
            });
            var upgrader = new TransactionalUpgrader(
                _database,
                upgrades,
                null,
                new UpgradeSettings(),
                dbLockMoq.Object, new NullLoggerFactory());

            var ex = Assert.ThrowsAsync<MongoMigrationException>(upgrader.UpgradeOrInit);
            Assert.IsTrue(ex.Message.Contains("autoupgrade is disabled", StringComparison.OrdinalIgnoreCase),
                ex.Message);
        }

        [Test]
        public async Task EmptyUpgradeListTest()
        {
            var settings = new UpgradeSettings();
            var verCollection = _database.GetCollection<DbVersion>(settings.VersionCollectionName);
            await _database.DropCollectionAsync(settings.VersionCollectionName);
            var dbLock = new MongoDbLock(verCollection);

            var upgrader = new TransactionalUpgrader(
                _database,
                new UpgradeChain(null),
                null,
                settings,
                dbLock,
                new NullLoggerFactory());

            await upgrader.UpgradeOrInit();
            var ver = (await verCollection.FindAsync(e => true)).First();
            Assert.IsNull(ver.Version);
        }

        [Test]
        public async Task InitToTheLastVersionTest()
        {
            var settings = new UpgradeSettings();
            var verCollection = _database.GetCollection<DbVersion>(settings.VersionCollectionName);
            await _database.DropCollectionAsync(settings.VersionCollectionName);
            var dbLock = new MongoDbLock(verCollection);
            var upgrades = new UpgradeChain(new List<IUpgradeLink>
            {
                new UpgradeStub("0.0", "0.1"),
                new UpgradeStub("0.1", "0.2"),
                new UpgradeStub("0.2", "0.3"),
            });
            var upgrader = new TransactionalUpgrader(
                _database,
                upgrades,
                null,
                settings,
                dbLock,
                new NullLoggerFactory());

            await upgrader.UpgradeOrInit();
            var ver = (await verCollection.FindAsync(e => true)).First();
            Assert.AreEqual(Version.Parse("0.3"), ver.Version);
        }

        [Test]
        public async Task InitFailTest()
        {
            var settings = new UpgradeSettings();
            var verCollection = _database.GetCollection<DbVersion>(settings.VersionCollectionName);
            await _database.DropCollectionAsync(settings.VersionCollectionName);
            var dbLock = new MongoDbLock(verCollection);
            var upgrades = new UpgradeChain(new List<IUpgradeLink>
            {
                new UpgradeStub("0.0", "0.1"),
                new UpgradeStub("0.1", "0.2"),
                new UpgradeStub("0.2", "0.3"),
            });
            var upgrader = new TransactionalUpgrader(
                _database,
                upgrades,
                new UpgradeStub(null, "0.0", (s, db, log) => throw new Exception("test init failed")),
                settings,
                dbLock,
                new NullLoggerFactory());

            var ex = Assert.ThrowsAsync<MongoMigrationException>(upgrader.UpgradeOrInit);
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
            var dbLock = new MongoDbLock(verCollection);

            //Step1: init db
            var upgrader = new TransactionalUpgrader(
                _database,
                new UpgradeChain(null),
                new UpgradeStub(null, "0.0"),
                settings,
                dbLock,
                new NullLoggerFactory());
            await upgrader.UpgradeOrInit();

            var ver = (await verCollection.FindAsync(e => true)).First();
            Assert.AreEqual(Version.Parse("0.0"), ver.Version);

            //Step2: Apply upgrade chain
            var upgrades = new UpgradeChain(new List<IUpgradeLink>
            {
                new UpgradeStub("0.0", "0.1"),
                new UpgradeStub("0.1", "0.2"),
                new UpgradeStub("0.2", "0.3"),
            });

            upgrader = new TransactionalUpgrader(
                _database,
                upgrades,
                new UpgradeStub(null, "0.0", (s, db, log) => throw new Exception("test init failed")),
                settings,
                dbLock,
                new NullLoggerFactory());
            await upgrader.UpgradeOrInit();

            ver = (await verCollection.FindAsync(e => true)).First();
            Assert.AreEqual(Version.Parse("0.3"), ver.Version);
        }

        [Test]
        public async Task UpgradeToLatestAvailableVersionTest()
        {
            var settings = new UpgradeSettings();
            var verCollection = _database.GetCollection<DbVersion>(settings.VersionCollectionName);
            await _database.DropCollectionAsync(settings.VersionCollectionName);
            var dbLock = new MongoDbLock(verCollection);

            //Step2: Apply upgrade chain
            var upgrades = new UpgradeChain(new List<IUpgradeLink>
            {
                new UpgradeStub("0.0", "0.1"),
                new UpgradeStub("0.1", "0.2"),
                new UpgradeStub("0.2", "0.3"),
            });

            var upgrader = new TransactionalUpgrader(
                _database,
                upgrades,
                new UpgradeStub(null, "0.0"),
                settings,
                dbLock,
                new NullLoggerFactory());
            await upgrader.UpgradeOrInit();

            var ver = (await verCollection.FindAsync(e => true)).First();
            Assert.AreEqual(Version.Parse("0.3"), ver.Version, "After init, DB should marked with the latest version");
        }
    }
}