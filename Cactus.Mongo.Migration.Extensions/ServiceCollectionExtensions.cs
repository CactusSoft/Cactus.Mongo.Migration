using System.Collections.Generic;
using Cactus.Mongo.Migration.Model;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;

namespace Cactus.Mongo.Migration.Extensions
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddMigrations(this IServiceCollection services, IUpgradeSettings settings, IUpgrade initializer, IEnumerable<IUpgradeLink> upgrades)
        {
            services.AddTransient(s => s.GetRequiredService<IMongoDatabase>().GetCollection<DbVersion>(settings.VersionCollectionName));
            services.AddTransient<IDbLock>(s => new MongoDbLock(s.GetRequiredService<IMongoCollection<DbVersion>>()));
            services.AddTransient<IUpgradeChain>(s => new UpgradeChain(upgrades));
            services.AddTransient<IUpgrader>(s => new TransactionalUpgrader(
                s.GetRequiredService<IMongoDatabase>(),
                s.GetRequiredService<IUpgradeChain>(),
                initializer,
                settings,
                s.GetRequiredService<ILoggerFactory>()
                ));

            return services;
        }

        public static IServiceCollection AddMigrations(this IServiceCollection services, IUpgrade initializer, IEnumerable<IUpgradeLink> upgrades)
        {
            return services.AddMigrations(new UpgradeSettings(), initializer, upgrades);
        }

        public static IServiceCollection AddMigrations(this IServiceCollection services, IEnumerable<IUpgradeLink> upgrades)
        {
            return services.AddMigrations(new UpgradeSettings(), null, upgrades);
        }
    }
}
