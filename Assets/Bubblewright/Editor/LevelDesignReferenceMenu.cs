using System.IO;
using UnityEditor;
using UnityEngine;

namespace Bubblewright.EditorTools
{
    /// <summary>
    /// Bubblewright > Write Level-Design Reference: rewrites the generated number tables in the level-design skill's
    /// reference file from Tuning. Run it after changing a number the skill quotes; LevelDesignReferenceTests fails until then.
    /// </summary>
    public static class LevelDesignReferenceMenu
    {
        [MenuItem("Bubblewright/Write Level-Design Reference")]
        static void Write()
        {
            string path = LevelDesignReference.FullPath;
            if (!File.Exists(path))
            {
                Debug.LogError("Level-design reference not found at " + path);
                return;
            }
            string updated = LevelDesignReference.Splice(File.ReadAllText(path));
            if (updated == null)
            {
                Debug.LogError("The level-design reference has lost its generated-block markers: " + LevelDesignReference.Begin);
                return;
            }
            File.WriteAllText(path, updated);
            Debug.Log("Wrote the level-design reference tables to " + path);
        }
    }
}
