using UnityEditor;
using UnityEngine;

namespace ArcaneOnyx.BehaviorTree
{
    public static class BH3Documentation
    {
        public const string MenuPath = "Tools/BH3/Documentation";
        public const string Url = "https://github.com/platinio/Unity-BH3/blob/main/README.md";

        [MenuItem(MenuPath, priority = 100)]
        public static void Open() => Application.OpenURL(Url);
    }
}
