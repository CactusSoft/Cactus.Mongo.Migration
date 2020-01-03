using System;
using System.Collections.Generic;
using System.Linq;
using Cactus.Mongo.Migration.Model;

namespace Cactus.Mongo.Migration
{
    /// <summary>
    /// Chain of upgrades
    /// </summary>
    public interface IMigrationChain
    {
        /// <summary>
        /// Check the chain for a most common mistakes
        /// </summary>
        void Validate();

        /// <summary>
        /// Get a path to upgrade.
        /// Returns a sequence of upgrades that should be applyed one by one in order to archive the latest available version
        /// </summary>
        /// <param name="from">Current DB version. The velue is used to find a start point</param>
        /// <returns>Sequence of upgrades, may be empty</returns>
        IEnumerable<IUpgradeLink> GetUpgradePath(Version from);

        /// <summary>
        /// True if we have any upgrades that can be applied
        /// </summary>
        /// <param name="from"></param>
        /// <returns></returns>
        bool HasAny(Version from);

        /// <summary>
        /// Target version (max version in the upgrade chain
        /// </summary>
        Version Target { get; }
    }

    public class MigrationChain : IMigrationChain
    {
        private readonly IList<IUpgradeLink> _upgradeChain;

        public MigrationChain(IEnumerable<IUpgradeLink> upgrades)
        {
            if (upgrades == null)
            {
                _upgradeChain = new List<IUpgradeLink>(0);
            }
            else
            {
                _upgradeChain = upgrades
                    .Where(e => e != null)
                    .OrderBy(e => e.UpgradeTo)
                    .ThenBy(e => e.MinFrom)
                    .ToList();
            }
        }

        public IEnumerable<IUpgradeLink> GetUpgradePath(Version from)
        {
            if (_upgradeChain.Count == 0)
                yield break;

            if (from == null)
            {
                if (_upgradeChain[0].MinFrom == null)
                {
                    foreach (var upgrade in _upgradeChain)
                        yield return upgrade;
                }
                else
                {
                    throw new MigrationException($"Chain validation failed: unable to find a start point to upgrade from NULL version");
                }
            }
            else
            {
                if (from >= _upgradeChain.Max(e => e.UpgradeTo))
                {
                    //Already has the latest version
                    yield break;
                }

                var startPoint = _upgradeChain.FirstOrDefault(e => e.MinFrom <= from && e.UpgradeTo > from);
                if (startPoint == null)
                {

                    throw new MigrationException($"Chain validation failed: unable to find a start point to upgrade from the current version {from}");
                }

                var startindex = _upgradeChain.IndexOf(startPoint);
                for (; startindex < _upgradeChain.Count; startindex++)
                    yield return _upgradeChain[startindex];
            }
        }

        public bool HasAny(Version from)
        {
            if (from != null)
            {
                return _upgradeChain.Any(e => e.UpgradeTo > from);
            }

            return _upgradeChain.Count > 0;
        }

        public Version Target => _upgradeChain.Max(e => e.UpgradeTo);

        public void Validate()
        {
            var versionSet = new HashSet<Version>();
            foreach (var upgrade in _upgradeChain)
            {
                if (upgrade == null)
                {
                    throw new MigrationException($"Null object found in the chain");
                }

                if (upgrade.MinFrom == upgrade.UpgradeTo)
                {
                    throw new MigrationException($"Chain validation failed: equal from & to versions in the same upgrade version {upgrade.MinFrom}");
                }

                if (upgrade.MinFrom == null)
                {
                    if (_upgradeChain.IndexOf(upgrade) > 0)
                        throw new MigrationException($"Chain validation failed: only the first upgrade in the chain can have From=null");
                }

                if (upgrade.UpgradeTo == null)
                {
                    throw new MigrationException($"Chain validation failed: UpgradeTo could not be null");
                }

                if (upgrade.MinFrom != null && upgrade.MinFrom >= upgrade.UpgradeTo)
                {
                    throw new MigrationException($"Chain validation failed: UpgradeFrom should always be less than UpgradeTo. The issue found in {upgrade.GetType().Name}");
                }

                if (versionSet.Contains(upgrade.UpgradeTo))
                {
                    throw new MigrationException($"Chain validation failed: UpgradeTo must be unique for every upgrade, {upgrade.UpgradeTo} duplicates");
                }
                versionSet.Add(upgrade.UpgradeTo);
            }


            for (int i = 1; i < _upgradeChain.Count; i++)
            {
                var prevUpgrade = _upgradeChain[i - 1];
                var thisUpgrade = _upgradeChain[i];
                if (prevUpgrade.UpgradeTo < thisUpgrade.MinFrom)
                {
                    throw new MigrationException($"Chain validation failed: a gap detected between version {prevUpgrade.UpgradeTo} and {thisUpgrade.MinFrom}");
                }
            }
        }
    }
}