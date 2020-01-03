using MongoDB.Driver;
using System;
using System.Threading.Tasks;
using Cactus.Mongo.Migration.Model;

namespace Cactus.Mongo.Migration
{
    //TODO: IAsyncDisposable
    public interface IDbLock
    {
        Task<IDbLockState> ObtainLock(TimeSpan timeout);
        Task ReleaseLock();
    }

    public class MongoDbLock : IDbLock
    {
        private readonly IMongoCollection<DbVersion> _verCollection;
        private readonly string _docId;
        private readonly string _lockerId;
        private volatile bool _isLockObtained;

        public MongoDbLock(IUpgradeSettings settings, IMongoDatabase db)
        {
            _verCollection = db.GetCollection<DbVersion>(settings.VersionCollectionName);
            _docId = settings.VersionDocumentId;
            _lockerId = Guid.NewGuid().ToString("N");
        }

        public async Task<IDbLockState> ObtainLock(TimeSpan timeout)
        {
            //To ensure that the collection is created before trying to obtain a lock
            await _verCollection.CountDocumentsAsync(e => true);

            //Obtain the lock
            var counter = 0;
            var pause = TimeSpan.FromSeconds(1);
            if (timeout < pause) timeout = pause; //To make a single try even if pause is 0
            for (var waitedFor = TimeSpan.Zero; waitedFor < timeout; waitedFor += timeout >= pause ? pause : timeout)
            {
                counter++;
                try
                {
                    var ver = await _verCollection.FindOneAndUpdateAsync<DbVersion>(
                        e => e.Id == _docId && e.IsLocked == false,
                        Builders<DbVersion>.Update
                            .Set(e => e.IsLocked, true)
                            .Set(e => e.LockerId, _lockerId)
                            .SetOnInsert(e => e.Id, _docId)
                            .SetOnInsert(e => e.AutoUpgradeEnabled, true),
                        new FindOneAndUpdateOptions<DbVersion>
                        { IsUpsert = true, ReturnDocument = ReturnDocument.After });

                    if (ver != null)
                    {
                        _isLockObtained = true;
                        return ver;
                    }
                }
                catch (MongoCommandException ex) when (ex.CodeName == "DuplicateKey")
                {
                    //Insert failed - there's a record with the same id
                    //So, getting lock is failed, continue to retry
                }

                await Task.Delay(pause);
            }

            //Lock is not obtained
            throw new MigrationException($"Lock retrieve failed: timeout {timeout} exceeded, {counter} tries done");
        }

        public async Task ReleaseLock()
        {
            if (_isLockObtained)
            {
                var res = await _verCollection.UpdateOneAsync(e => e.Id == _docId && e.LockerId == _lockerId, Builders<DbVersion>.Update.Set(e => e.IsLocked, false));
                if (res.MatchedCount == 0)
                {
                    throw new MigrationException($"Lock release failed: entity not found for Id:{_docId} and LockerId:{_lockerId}");
                }
                _isLockObtained = false;
            }
        }
    }
}
