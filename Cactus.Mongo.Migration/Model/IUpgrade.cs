using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;

namespace Cactus.Mongo.Migration.Model
{
    /// <summary>
    /// Database initializer. It calls only first time, when DB is empty and requires some init. 
    /// </summary>
    public interface IUpgrade
    {
        /// <summary>
        /// The db version that should be applied after the upgrade
        /// </summary>
        Version UpgradeTo { get; }

        /// <summary>
        /// Apply upgrade method
        /// </summary>
        /// <param name="session"></param>
        /// <param name="db">Database object</param>
        /// <param name="log"></param>
        /// <returns></returns>
        Task Apply(IClientSessionHandle session, IMongoDatabase db, ILogger log);
    }
}
