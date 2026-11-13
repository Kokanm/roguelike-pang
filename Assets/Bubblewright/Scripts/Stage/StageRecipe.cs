using System;
using UnityEngine;

namespace Bubblewright
{
    /// <summary>
    /// How a normal stage spends its bubble budget. Later steps append ids at the end; do not reorder these.
    /// </summary>
    public enum RecipeId
    {
        Mixed,
        Giants,
        Swarm,
        RollingFloor,
        HardShell,
        Reinforcements,
    }

    /// <summary>
    /// One recipe as a row of data, in the same spirit as <see cref="BossSpec"/>. The rows live on <see cref="Tuning"/>.
    /// </summary>
    [Serializable]
    public class RecipeDef
    {
        public RecipeId id;
        public string name;
        [Tooltip("The first world that may draw this recipe.")]
        public int fromWorld = 1;
        [Tooltip("Smallest tier index the generator may pick: 0 XL, 1 L, 2 M, 3 S.")]
        public int minTier;
        [Tooltip("Largest tier index the generator may pick.")]
        public int maxTier = 3;
        [Tooltip("Multiplies the usual start-bubble cap. Ignored when Start Cap Max is above 0.")]
        public float startCapScale = 1f;
        [Tooltip("Above 0, the stage starts with at most this many bubbles instead of the usual cap. Giants uses Giants Max Late from Giants Late From World on.")]
        public int startCapMax;
        [Tooltip("Above 0, spend up to this share of the budget on big rolling balls, ignoring the usual roller chance. 0 keeps today's roller rule.")]
        public float rollerShare;
        [Tooltip("Most big rolling balls when Roller Share is above 0.")]
        public int rollerMax;
        [Tooltip("Below 0, ceramic bubbles follow Ceramic Chance and Ceramic From World. 0 or more replaces that chance in every world.")]
        public float ceramicChance = -1f;
        [Tooltip("Every non-steel rolling ball on the stage is ceramic.")]
        public bool allRollersCeramic;
        [Tooltip("Waves of bubbles that drop in from the ceiling after the start. 0 means none.")]
        public int waves;
        [Tooltip("Share of the stage's bubble cost that is on screen at the start. The rest arrives in the waves. Only used when Waves is above 0.")]
        public float startShare = 1f;
        [Tooltip("Multiplies the stage's storm clock.")]
        public float timeScale = 1f;
        [Tooltip("Off: the recipe is never drawn. A debug force can still use it.")]
        public bool inPool = true;
    }
}
