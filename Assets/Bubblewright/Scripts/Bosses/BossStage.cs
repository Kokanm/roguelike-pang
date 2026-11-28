using System;
using UnityEngine;

namespace Bubblewright
{
    /// <summary>
    /// What a boss's phases may ask of the stage they fight in: the map and arena events, where Wick is, how many
    /// bubbles are alive, firing patterns, telling an attack before it lands and shaking the screen. Game implements
    /// it (Game.Boss.cs) and a test supplies a fake, so a phase's moves can be run without a scene. A boss gets one
    /// from Boss.Stage; a phase never reaches for Game itself.
    /// </summary>
    public interface IBossStage
    {
        StageMap Map { get; }
        ArenaEvents Arena { get; }
        /// <summary>Wick's hitbox right now.</summary>
        Rect WickBox { get; }
        /// <summary>Bubbles in play; a boss releases a balloon only below Tuning.bossMaxBubbles.</summary>
        int BubblesAlive { get; }
        /// <summary>Fires every launch of a pattern now (a launch with a delay leaves later).</summary>
        void Fire(Launch[] launches);
        /// <summary>Shows a tell over area and runs attack when it ends, never sooner than bossTellMin seconds later. Returns the lead used.</summary>
        float Telegraph(TellKind kind, Rect area, float lead, Action attack);
        /// <summary>A heavy landing or stomp at this point: screen shake, dust and a thud. strength is 1 for a stomp.</summary>
        void Impact(Vector2 at, float strength);
        /// <summary>A puff of smoke or dust at this point (a pipe breathing, a foot scuffing). Looks only.</summary>
        void Puff(Vector2 at);
    }
}
