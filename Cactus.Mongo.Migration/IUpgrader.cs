using System.Threading.Tasks;

namespace Cactus.Mongo.Migration
{
    /// <summary>
    /// Upgrader
    /// </summary>
    public interface IUpgrader
    {
        /// <summary>
        /// Perform DB upgrade if necessary 
        /// </summary>
        /// <returns></returns>
        Task UpgradeOrInit();
    }
}
