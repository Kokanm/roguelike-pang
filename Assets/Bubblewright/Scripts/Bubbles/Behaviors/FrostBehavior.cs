namespace Bubblewright
{
    /// <summary>Frost bubble: the floor tile it lands on turns to ice for a few seconds.</summary>
    public class FrostBehavior : BubbleBehavior
    {
        public override void OnLanded(Bubble b)
        {
            // Wick always starts at x = 0 on normal stages.
            Game.Ice?.FreezeUnder(Game.Map, b.Position, b.Radius, Game.StagePlayTime, 0f);
        }
    }
}
