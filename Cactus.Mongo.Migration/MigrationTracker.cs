using System;
using System.Threading.Tasks;
using Cactus.Mongo.Migration.Model;
using MongoDB.Driver;

namespace Cactus.Mongo.Migration
{
    public interface IMigrationTracker
    {
        Task<IMigrationState> GetState();
        Task TrackSuccessUpgrade(IClientSessionHandle session, Version ver);
        Task TrackFail(string error);
    }

    public class MongoMigrationTracker : IMigrationTracker
    {
        private readonly IUpgradeSettings _settings;
        private readonly IMongoCollection<DbVersion> _verCollection;

        public MongoMigrationTracker(IUpgradeSettings settings, IMongoDatabase db)
        {
            _settings = settings;
            _verCollection = db.GetCollection<DbVersion>(settings.VersionCollectionName);
        }

        public async Task<IMigrationState> GetState()
        {
            var ver = (await _verCollection.FindAsync(e => e.Id == _settings.VersionDocumentId)).FirstOrDefault();
            if (ver == null)
            {
                ver = new DbVersion
                {
                    Id = _settings.VersionDocumentId,
                    AutoUpgradeEnabled = true
                };
                await _verCollection.InsertOneAsync(ver);
            }
            return ver;
        }

        public Task TrackSuccessUpgrade(IClientSessionHandle session, Version ver)
        {
            return _verCollection.UpdateOneAsync(session, e => e.Id == _settings.VersionDocumentId, Builders<DbVersion>.Update.Set(e => e.Version, ver).Set(e => e.AutoUpgradeEnabled, true));
        }

        public Task TrackFail(string error)
        {
            return _verCollection.UpdateOneAsync(e => e.Id == _settings.VersionDocumentId, Builders<DbVersion>.Update.Set(e => e.LastUpgradeError, error).Set(e => e.AutoUpgradeEnabled, !_settings.PreventUpgradesOnError));
        }
    }
}
