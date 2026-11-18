using NUnit.Framework;
using UnityEngine;

namespace Bubblewright.Tests
{
    /// <summary>
    /// The footer is gone: the chosen items show as a see-through strip in the top right corner during play and in
    /// full on the pause screen. These check the item list and that both layouts fit with every item taken.
    /// </summary>
    public class ItemsLayoutTests
    {
        [Test]
        public void StripAndPauseListFitWithEveryItemTaken()
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

        [Test]
        public void EveryUpgradeHasASharpHalfSizeIcon()
        {
            foreach (var u in Upgrade.All)
            {
                var icon = PlaceholderArt.I.UpgradeMiniIcon(u.Name);
                Assert.AreEqual(PlaceholderArt.UpgradeMiniIconSize, icon.width, u.Name);
                Assert.AreEqual(PlaceholderArt.UpgradeMiniIconSize, icon.height, u.Name);
                Assert.AreEqual(FilterMode.Point, icon.filterMode, $"{u.Name} mini icon must stay pixel sharp");
                int painted = 0;
                foreach (var p in icon.GetPixels32()) if (p.a > 0) painted++;
                Assert.Greater(painted, 10, $"{u.Name} mini icon is nearly empty");
            }
        }
    }
}
