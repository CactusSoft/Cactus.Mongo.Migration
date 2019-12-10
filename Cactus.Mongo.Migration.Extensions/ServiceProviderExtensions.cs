using System;
using Microsoft.Extensions.DependencyInjection;

namespace Cactus.Mongo.Migration.Extensions
{
    public static class ServiceProviderExtensions
    {
        public static void UpgradeMongo(this IServiceProvider serviceProvider)
        {
            using (var scope = serviceProvider.CreateScope())
            {
                scope.ServiceProvider
                    .GetRequiredService<IUpgrader>()
                    .UpgradeOrInit()
                    .GetAwaiter().GetResult();
            }
        }
    }
}
