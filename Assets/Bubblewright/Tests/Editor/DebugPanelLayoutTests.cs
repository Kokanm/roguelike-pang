using NUnit.Framework;

namespace Bubblewright.Tests
{
    /// <summary>
    /// The debug panel used to clip buttons and labels. This lays out the real control labels at the sizes the game uses.
    /// </summary>
    public class DebugPanelLayoutTests
    {
        [Test]
        public void EveryControlFitsItsRowAndTheScreen()
        {
            string error = DebugPanelLayout.ValidateSamples();
            Assert.IsNull(error, error);
        }
    }
}
