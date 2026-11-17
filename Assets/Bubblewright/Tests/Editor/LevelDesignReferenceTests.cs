using System.IO;
using NUnit.Framework;

namespace Bubblewright.Tests
{
    public class LevelDesignReferenceTests
    {
        [Test]
        public void LevelDesignReferenceIsUpToDate()
        {
            string path = LevelDesignReference.FullPath;
            Assert.IsTrue(File.Exists(path), "Missing " + LevelDesignReference.RelativePath);
            string file = File.ReadAllText(path).Replace("\r\n", "\n");
            string expected = LevelDesignReference.Splice(file);
            Assert.IsNotNull(expected, "The reference file has lost its generated-block markers.");
            Assert.AreEqual(expected, file,
                "The level-design reference quotes numbers that no longer match Tuning. Run Bubblewright > Write Level-Design Reference and commit the file.");
        }

        [Test]
        public void TablesQuoteTheValidatorsLimits()
        {
            string tables = LevelDesignReference.Tables();
            StageValidator.PopBand(4, out int min, out int max);
            StringAssert.Contains($"| D4 pop count | {min} to {max} |", tables);
            StringAssert.Contains(
                $"Widest ground gap allowed | {StageValidator.MaxGapColumns} columns, {StageValidator.MaxSandGapColumns} in World 3 or next to sand",
                tables);
            StringAssert.Contains("| Moss |", tables);
        }
    }
}
