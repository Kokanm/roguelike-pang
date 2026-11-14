using System.Collections.Generic;
using UnityEngine;

namespace Bubblewright
{
    public partial class Game
    {
        List<StageMap.SpikeStrip> generatedSpikes = new List<StageMap.SpikeStrip>();
        Spikes spikes;

        /// <summary>Remember the stage's own strips, then add Lava placement when the debug toggle is on and the stage has none.</summary>
        void SetupSpikes()
        {
            generatedSpikes.Clear();
            if (Map != null) generatedSpikes.AddRange(Map.spikes);
            if (debug.spikes && Map != null && Map.spikes.Count == 0 && run != null)
                WorldRules.PlaceSpikes(Map, Mathf.Clamp(run.StageInWorld, 1, 5), run.StageRng(8));
            ReplaceSpikes();
        }

        void ClearSpikes()
        {
            generatedSpikes.Clear();
            if (spikes == null) return;
            spikes.gameObject.SetActive(false);
            Destroy(spikes.gameObject);
            spikes = null;
        }

        void SetDebugSpikes(bool on)
        {
            debug.spikes = on;
            if (Map == null) return;
            Map.spikes.Clear();
            Map.spikes.AddRange(generatedSpikes);
            if (on && Map.spikes.Count == 0 && run != null)
                WorldRules.PlaceSpikes(Map, Mathf.Clamp(run.StageInWorld, 1, 5), run.StageRng(8));
            if (spikes != null) spikes.Rebuild(Map);
            else ReplaceSpikes();
        }

        void ReplaceSpikes()
        {
            if (spikes != null)
            {
                spikes.gameObject.SetActive(false);
                Destroy(spikes.gameObject);
                spikes = null;
            }
            if (world != null && Map != null)
                spikes = Spikes.Create(world, Map);
        }

        /// <summary>Shared by bubble hits and spike hits: freeze, shake, the hurt sound, and the game-over path.</summary>
        void HurtWick(float fromX, int damage)
        {
            wick.Hurt(fromX, damage);
            AfterWickHurt();
        }

        /// <summary>Everything after Wick loses hearts, whatever took them (a hit, spikes, or a gap).</summary>
        void AfterWickHurt()
        {
            freeze = T.hurtFreeze;
            shake = T.shakeTime;
            Sfx.Play(Sound.Hurt);
            if (wick.Dead)
            {
                HideHarpoons();
                run.Hearts = 0;
                SaveGame.Delete();
                RecordScore();
                SetState(State.Over);
                Sfx.Play(Sound.Over);
            }
            else
            {
                run.Hearts = wick.Hearts;
                SaveGame.UpdateHearts(wick.Hearts);
            }
        }

        void DrawSpikeHurtBox(Rect box) => DrawWorldRect(box, Palette.SteelLight);
    }
}
