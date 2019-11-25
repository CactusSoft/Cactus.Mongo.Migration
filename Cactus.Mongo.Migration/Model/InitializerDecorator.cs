using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;

namespace Cactus.Mongo.Migration.Model
{
    internal class InitializerDecorator : IUpgrade
    {
        private readonly IUpgrade _target;

        public InitializerDecorator(IUpgrade target, Version upgradeTo)
        {
            _target = target;
            UpgradeTo = upgradeTo;
        }

        public Version UpgradeTo { get; }

        public Task Apply(IClientSessionHandle session, IMongoDatabase db, ILogger log)
        {
            if (_target == null)
            {
                log.LogInformation("Empty initializer, just set {0} as a db version", UpgradeTo);
            }
            return _target == null ? Task.CompletedTask : _target.Apply(session, db, log);
        }
    }
}
