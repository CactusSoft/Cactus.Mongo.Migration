using System;
using System.Threading.Tasks;
using Cactus.Mongo.Migration.Model;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;

namespace AspnetExample.Migrations
{
    internal class SecondMigration : IUpgradeLink
    {
        public Version MinFrom => new Version(0, 0);
        public Version UpgradeTo => new Version(2, 0);
        public Task Apply(IClientSessionHandle session, IMongoDatabase db, ILogger log)
        {
           log.LogInformation("Second upgrade");
           return Task.CompletedTask;
        }
    }
}
