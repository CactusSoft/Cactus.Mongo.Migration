using System;

namespace Cactus.Mongo.Migration
{
    public class MongoMigrationException : Exception
    {
        public MongoMigrationException(string message) : base(message) { }

        public MongoMigrationException(string message, Exception innerException) : base(message, innerException) { }
    }
}
