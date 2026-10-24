using UnityEngine;

namespace ArcaneOnyx.BH3.Samples.Tests
{
    internal static class TweenRunnerCleanup
    {
        public static void DestroyEveryRunner()
        {
            foreach (TweenRunner runner in Resources.FindObjectsOfTypeAll<TweenRunner>())
            {
                Object.DestroyImmediate(runner.gameObject);
            }
        }
    }
}
