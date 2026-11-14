namespace Bubblewright
{
    public partial class Game
    {
        static readonly TempIce ice = new TempIce();

        /// <summary>Ice patches made by Frost bubbles on the current stage.</summary>
        public static TempIce Ice => ice;

        /// <summary>Seconds of play in this stage.</summary>
        public static float StagePlayTime { get; private set; }

        void ResetFrost()
        {
            ice.Clear();
            StagePlayTime = 0f;
        }

        void TickFrost(float dt)
        {
            StagePlayTime += dt;
            ice.Tick(Map, dt);
        }
    }
}
