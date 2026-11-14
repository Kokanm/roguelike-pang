using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Bubblewright.Tests
{
    public class ReloadTests
    {
        const BindingFlags Instance = BindingFlags.NonPublic | BindingFlags.Instance;

        [Test]
        public void GameplayStateWithoutARunReturnsToTheTitle()
        {
            var go = new GameObject("Reload Game");
            var game = go.AddComponent<Game>();
            try
            {
                // Edit mode does not run Awake. Play mode does, and a later script reload keeps that object.
                typeof(Game).GetMethod("Awake", Instance).Invoke(game, null);

                // A script reload restores the state enum and drops the run, the input and other plain objects.
                var stateType = typeof(Game).GetNestedType("State", Instance);
                Set(game, "state", Enum.Parse(stateType, "Play"));
                Set(game, "run", null);
                Set(game, "input", null);
                Set(game, "generatedSpikes", null);

                // The console reports this from OnGUI, which reads run.MaxHearts. Update hits the same
                // dropped fields first (input, then the run) and does not need a GUI event.
                typeof(Game).GetMethod("Update", Instance).Invoke(game, null);

                Assert.AreEqual("Title", Get(game, "state").ToString());
                Assert.NotNull(Get(game, "input"));
                Assert.NotNull(Get(game, "generatedSpikes"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        static void Set(Game game, string field, object value) =>
            typeof(Game).GetField(field, Instance).SetValue(game, value);

        static object Get(Game game, string field) =>
            typeof(Game).GetField(field, Instance).GetValue(game);
    }
}
