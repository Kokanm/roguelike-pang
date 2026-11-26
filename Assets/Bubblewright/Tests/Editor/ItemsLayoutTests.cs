using NUnit.Framework;
using UnityEngine;

namespace Bubblewright.Tests
{
    /// <summary>
    /// The pause screen lists the chosen items in full. These check the item list and that the list fits with every
    /// item taken.
    /// </summary>
    public class ItemsLayoutTests
    {
        [Test]
        public void PauseListFitsWithEveryItemTaken()
        {
            string error = ItemsLayout.ValidateSamples();
            Assert.IsNull(error, error);
        }

        [Test]
        public void BuildItemsListsEachUpgradeOnceWithItsCountAndSkipsHeals()
        {
            var run = new Run(7);
            run.Take(Upgrade.ByName("Swift Boots"));
            run.Take(Upgrade.ByName("Twin Cable"));
            run.Take(Upgrade.ByName("Swift Boots"));
            run.Hearts = 1;
            run.Take(Upgrade.ByName("Relight"));

            var items = run.BuildItems();
            Assert.AreEqual(2, items.Count);
            Assert.AreEqual("Twin Cable", items[0].upgrade.Name, "Upgrade.All order");
            Assert.AreEqual(1, items[0].count);
            Assert.AreEqual("Swift Boots", items[1].upgrade.Name);
            Assert.AreEqual(2, items[1].count);
            Assert.AreEqual("Twin Cable, Swift Boots x2", run.BuildSummary());
        }

    }
}
