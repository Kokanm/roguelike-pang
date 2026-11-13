namespace Bubblewright
{
    /// <summary>What makes a kind more than a recoloured Gloop. One instance per bubble, so it can hold state.</summary>
    public abstract class BubbleBehavior
    {
        /// <summary>Called once, right after the bubble is created. child is true for a bubble born from a pop.</summary>
        public virtual void OnSpawn(Bubble b, bool child) { }
        /// <summary>Called every tick before the normal bounce. Return true when the behaviour moved the bubble itself this tick; the normal bounce is then skipped.</summary>
        public virtual bool Tick(Bubble b, float dt) => false;
        /// <summary>Called when the normal bounce lands on a surface, after the bounce speed was set. The behaviour may change b.Velocity.</summary>
        public virtual void OnLanded(Bubble b) { }
        /// <summary>False while a harpoon should pass through it.</summary>
        public virtual bool CanBeHit(Bubble b) => true;
        /// <summary>False while touching it should not hurt Wick.</summary>
        public virtual bool CanHurt(Bubble b) => true;
        /// <summary>Called on every bubble when the storm starts.</summary>
        public virtual void OnStorm(Bubble b) { }

        /// <summary>The behaviour of a kind, or null for a plain bouncing bubble. Each kind's step adds its line here.</summary>
        public static BubbleBehavior For(BubbleKind kind)
        {
            switch (kind)
            {
                case BubbleKind.Sinker: return new SinkerBehavior();
                default: return null;
            }
        }
    }
}
