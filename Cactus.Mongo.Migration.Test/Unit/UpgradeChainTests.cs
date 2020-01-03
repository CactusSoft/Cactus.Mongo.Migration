using System;
using System.Collections.Generic;
using System.Linq;
using Cactus.Mongo.Migration.Model;
using NUnit.Framework;

namespace Cactus.Mongo.Migration.Test.Unit
{
    public class UpgradeChainTests
    {
        [Test]
        public void EmptyUpgradeListTest()
        {
            // All these means nothing to upgrade
            var chain = new MigrationChain(Enumerable.Empty<IUpgradeLink>());
            chain.Validate();
            var path = chain.GetUpgradePath(null);
            Assert.IsNotNull(path);
            Assert.AreEqual(0, path.Count());
            Assert.IsNull(chain.Target);
        }

        [Test]
        public void ConsistentUpgradeWithNullListTest([Values(null, "0.0", "0.1", "0.2", "0.3", "0.4", "1.0")]string current)
        {
            var currentVer = current == null ? null : Version.Parse(current);
            var chain = new MigrationChain(new List<IUpgradeLink>
            {
                 new UpgradeStub(null,"0.0"),
                 new UpgradeStub("0.0","0.1"),
                 new UpgradeStub("0.1","0.2"),
                 new UpgradeStub("0.2","0.3"),
            });
            chain.Validate();
            // All these means nothing to upgrade
            var path = chain.GetUpgradePath(currentVer);
            switch (current)
            {
                case null:
                    Assert.AreEqual(4, path.Count());
                    break;
                case "0.0":
                    Assert.AreEqual(3, path.Count());
                    break;
                case "0.1":
                    Assert.AreEqual(2, path.Count());
                    break;
                case "0.2":
                    Assert.AreEqual(1, path.Count());
                    break;
                case "0.3":
                    Assert.AreEqual(0, path.Count());
                    break;
                case "0.4":
                    Assert.AreEqual(0, path.Count());
                    break;
                case "1.0":
                    Assert.AreEqual(0, path.Count());
                    break;
            }
        }

        [Test]
        public void ConsistentUpgradeListTest([Values("0.0", "0.1", "0.2", "0.3", "0.4", "1.0")]string current)
        {
            var currentVer = current == null ? null : Version.Parse(current);
            var chain = new MigrationChain(new List<IUpgradeLink>
            {
                 new UpgradeStub("0.0","0.1"),
                 new UpgradeStub("0.1","0.2"),
                 new UpgradeStub("0.2","0.3"),
            });
            chain.Validate();
            Assert.AreEqual(Version.Parse("0.3"), chain.Target);
            // All these means nothing to upgrade
            var path = chain.GetUpgradePath(currentVer);
            switch (current)
            {
                case "0.0":
                    Assert.AreEqual(3, path.Count());
                    break;
                case "0.1":
                    Assert.AreEqual(2, path.Count());
                    break;
                case "0.2":
                    Assert.AreEqual(1, path.Count());
                    break;
                case "0.3":
                    Assert.AreEqual(0, path.Count());
                    break;
                case "0.4":
                    Assert.AreEqual(0, path.Count());
                    break;
                case "1.0":
                    Assert.AreEqual(0, path.Count());
                    break;
            }
        }

        [Test]
        public void GapInChainTest()
        {
            var chain = new MigrationChain(new List<IUpgradeLink>
            {
                 new UpgradeStub("0.0","0.1"),
                 new UpgradeStub("0.2","0.3"),
                 new UpgradeStub("0.3","0.4"),
            });
            Assert.That(chain.Validate, Throws.InstanceOf<MigrationException>());
            Assert.AreEqual(Version.Parse("0.4"), chain.Target);
        }

        [Test]
        public void UpgradeToVersionIsUniqueTest()
        {
            var chain = new MigrationChain(new List<IUpgradeLink>
            {
                 new UpgradeStub("0.0","0.1"),
                 new UpgradeStub("0.0","0.3"),
                 new UpgradeStub("0.0","0.3"),
            });
            Assert.That(chain.Validate, Throws.InstanceOf<MigrationException>());
        }

        [Test]
        public void ChainOrderTest()
        {
            for (int i = 0; i < 10; i++) //To shuffle list a few times
            {
                var chain = new MigrationChain(new List<IUpgradeLink>
                {
                     new UpgradeStub("0.0","0.1"),
                     new UpgradeStub("0.1","0.2"),
                     new UpgradeStub("0.2","0.3"),
                     new UpgradeStub("0.2","0.4"),
                     new UpgradeStub("0.0","0.6"),
                     new UpgradeStub("0.3","1.6.8"),
                }.Shuffle());

                var res = chain.GetUpgradePath(new Version(0, 0)).ToList();
                Assert.AreEqual(new Version(0, 0), res[0].MinFrom);
                Assert.AreEqual(new Version(0, 1), res[0].UpgradeTo);
                Assert.AreEqual(new Version(0, 1), res[1].MinFrom);
                Assert.AreEqual(new Version(0, 2), res[1].UpgradeTo);
                Assert.AreEqual(new Version(0, 2), res[2].MinFrom);
                Assert.AreEqual(new Version(0, 3), res[2].UpgradeTo);
                Assert.AreEqual(new Version(0, 2), res[3].MinFrom);
                Assert.AreEqual(new Version(0, 4), res[3].UpgradeTo);
                Assert.AreEqual(new Version(0, 0), res[4].MinFrom);
                Assert.AreEqual(new Version(0, 6), res[4].UpgradeTo);
                Assert.AreEqual(new Version(0, 3), res[5].MinFrom);
                Assert.AreEqual(new Version(1, 6, 8), res[5].UpgradeTo);
            }
        }
    }
}