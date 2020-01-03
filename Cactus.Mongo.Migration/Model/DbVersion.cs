using System;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Cactus.Mongo.Migration.Model
{
    public interface IMigrationState
    {
        bool AutoUpgradeEnabled { get; }

        string LastUpgradeError { get; }

        Version Version { get; }

    }

    public interface IDbLockState
    {
        bool IsLocked { get; }

        string LockerId { get; }
    }

    public class DbVersion : IMigrationState, IDbLockState
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; }

        public bool AutoUpgradeEnabled { get; set; }

        public string LastUpgradeError { get; set; }

        public Version Version { get; set; }

        public bool IsLocked { get; set; }

        public string LockerId { get; set; }
    }
}
