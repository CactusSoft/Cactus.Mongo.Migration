using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Cactus.Mongo.Migration.Model;
using Mongo2Go;
using MongoDB.Driver;
using NUnit.Framework;

namespace Cactus.Mongo.Migration.Test.Integration
{
    [TestFixture]
    public class DbLockTests
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
        public async Task LockReleaseOnEmptyDbTest()
        {
            await _database.DropCollectionAsync(_settings.VersionCollectionName);
            var verCollection = _database.GetCollection<DbVersion>(_settings.VersionCollectionName);
            var dbLock = new MongoDbLock(_settings, _database);
            var ver = await dbLock.ObtainLock(TimeSpan.FromSeconds(5));
            Assert.IsNotNull(ver);
            Assert.IsNotNull(ver.LockerId);
            Assert.IsTrue(ver.IsLocked);

            await dbLock.ReleaseLock();
            var ver2 = (await verCollection.FindAsync(e => e.Id == _settings.VersionDocumentId)).First();
            Assert.IsNotNull(ver2);
            Assert.AreEqual(ver.LockerId, ver2.LockerId);
            Assert.IsFalse(ver2.IsLocked);
            Assert.IsTrue(ver2.AutoUpgradeEnabled);
            Assert.IsNull(ver2.Version);
        }

        [Test]
        public async Task LockOnEmptyDbTwiceTest([Values(3, 5, 8, 12)]int secondsToWait)
        {
            await _database.DropCollectionAsync(_settings.VersionCollectionName);
            var dbLock1 = new MongoDbLock(_settings, _database);
            var ver1 = await dbLock1.ObtainLock(TimeSpan.FromSeconds(0));
            Assert.IsNotNull(ver1);
            Assert.IsNotNull(ver1.LockerId);
            Assert.IsTrue(ver1.IsLocked);

            var dbLock2 = new MongoDbLock(_settings, _database);
            var stopWatch = new Stopwatch();
            stopWatch.Start();
            Assert.ThrowsAsync<MigrationException>(() => dbLock2.ObtainLock(TimeSpan.FromSeconds(secondsToWait)));
            stopWatch.Stop();
            Assert.IsTrue(TimeSpan.FromSeconds(secondsToWait - 1) < stopWatch.Elapsed, $"Elapsed: {stopWatch.Elapsed}");
            Assert.IsTrue(TimeSpan.FromSeconds(secondsToWait + 1) > stopWatch.Elapsed, $"Elapsed: {stopWatch.Elapsed}");
        }

        [Test]
        public async Task LockReleaseLockTest()
        {
            await _database.DropCollectionAsync(_settings.VersionCollectionName);
            var dbLock1 = new MongoDbLock(_settings, _database);
            var ver1 = await dbLock1.ObtainLock(TimeSpan.FromSeconds(0));
            Assert.IsNotNull(ver1);
            Assert.IsNotNull(ver1.LockerId);
            Assert.IsTrue(ver1.IsLocked);

            var dbLock2 = new MongoDbLock(_settings, _database);
            var secondLockTask = dbLock2.ObtainLock(TimeSpan.FromSeconds(5));
            await dbLock1.ReleaseLock();
            var ver2 = await secondLockTask;

            Assert.IsNotNull(ver2);
            Assert.IsNotNull(ver2.LockerId);
            Assert.AreNotEqual(ver1.LockerId, ver2.LockerId);
            Assert.IsTrue(ver1.IsLocked);
        }
    }
}