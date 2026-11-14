namespace Bubblewright
{
    /// <summary>The Scorch leaves a flame on the surface under it every time it lands. The smallest size leaves none.</summary>
    public class ScorchBehavior : BubbleBehavior
    {
        public override void OnLanded(Bubble b)
        {
            if (b.Tier >= 3 || Game.Flames == null || Game.Map == null) return;
            Game.Flames.AddUnder(Game.Map, b.Position, b.Radius);
        }
    }
}
