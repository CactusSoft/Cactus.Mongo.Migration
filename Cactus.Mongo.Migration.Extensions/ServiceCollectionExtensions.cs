using System;
using System.Collections.Generic;
using Cactus.Mongo.Migration.Model;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;

namespace Cactus.Mongo.Migration.Extensions
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddMigrations(
            this IServiceCollection services,
            Func<IServiceProvider, IUpgradeSettings> settings,
            Func<IServiceProvider, IMongoDatabase> db,
            Func<IServiceProvider, IUpgrade> initializer,
            Func<IServiceProvider, IEnumerable<IUpgradeLink>> upgrades)
        {
            services.AddScoped(s => db(s).GetCollection<DbVersion>(settings(s).VersionCollectionName));
            services.AddScoped<IDbLock>(s => new MongoDbLock(s.GetRequiredService<IMongoCollection<DbVersion>>()));
            services.AddScoped<IUpgradeChain>(s => new UpgradeChain(upgrades(s)));
            services.AddScoped<IUpgrader>(s => new TransactionalUpgrader(
                db(s),
                s.GetRequiredService<IUpgradeChain>(),
                initializer(s),
                settings(s),
                s.GetRequiredService<ILoggerFactory>()
                ));

            return services;
        }

        public static IServiceCollection AddMigrations(this IServiceCollection services, IUpgrade initializer, IEnumerable<IUpgradeLink> upgrades)
        {
            return services.AddMigrations(s => UpgradeSettings.Default, s => s.GetRequiredService<IMongoDatabase>(), s => initializer, s => upgrades);
        }

        public static IServiceCollection AddMigrations(this IServiceCollection services, IEnumerable<IUpgradeLink> upgrades)
        {
            return services.AddMigrations(s => UpgradeSettings.Default, s => s.GetRequiredService<IMongoDatabase>(), s => null, s => upgrades);
        }

        public static IServiceCollection AddMigrations(this IServiceCollection services, IUpgrade initializer)
        {
            return services.AddMigrations(s => UpgradeSettings.Default, s => s.GetRequiredService<IMongoDatabase>(), s => initializer, s => null);
        }
    }
}
