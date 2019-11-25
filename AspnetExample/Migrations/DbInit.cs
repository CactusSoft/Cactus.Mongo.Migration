using System;
using System.Threading.Tasks;
using Cactus.Mongo.Migration.Model;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;

namespace AspnetExample.Migrations
{
    internal class DbInit : IUpgrade
    {
        public Version UpgradeTo => new Version(0, 0);

        public Task Apply(IClientSessionHandle session, IMongoDatabase db, ILogger log)
        {
            log.LogInformation("DB initialized");
            return Task.CompletedTask;
        }
    }
}
