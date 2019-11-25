using System;

namespace Cactus.Mongo.Migration.Model
{
    /// <summary>
    /// An atomic upgrade link in a chain. 
    /// Could be applied only as a part of upgrade chain
    /// </summary>
    public interface IUpgradeLink: IUpgrade
    {
        /// <summary>
        /// The minimal version the update required to start from
        /// </summary>
        Version MinFrom { get; }
    }
}
