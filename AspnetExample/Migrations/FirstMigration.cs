using System;
using System.Threading.Tasks;
using Cactus.Mongo.Migration.Model;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;

namespace AspnetExample.Migrations
{
    internal class FirstMigration : IUpgradeLink
    {
        public Version MinFrom => new Version(0, 0);
        public Version UpgradeTo => new Version(0, 1);
        public Task Apply(IClientSessionHandle session, IMongoDatabase db, ILogger log)
        {
           log.LogInformation("First upgrade");
           return Task.CompletedTask;
        }
    }
}
