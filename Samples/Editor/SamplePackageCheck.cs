using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEngine;

namespace ArcaneOnyx.BH3.Samples
{
    [InitializeOnLoad]
    public static class SamplePackageCheck
    {
        public static readonly string[] RequiredPackages = { "com.unity.ai.navigation", "com.unity.inputsystem" };

        private const string WasAskedThisSession = "ArcaneOnyx.BH3.Samples.WasAskedForPackages";

        static SamplePackageCheck()
        {
            EditorApplication.delayCall += OfferToInstallMissingPackages;
        }

        public static string[] MissingPackages(IEnumerable<string> installedPackages) =>
            RequiredPackages.Except(installedPackages).ToArray();

        private static void OfferToInstallMissingPackages()
        {
            var missing = MissingPackages(InstalledPackages());
            if (missing.Length == 0 || SessionState.GetBool(WasAskedThisSession, false)) return;

            SessionState.SetBool(WasAskedThisSession, true);
            var packageList = string.Join(", ", missing);
            Debug.LogWarning($"The BH3 samples need {packageList}. Without them the sample scenes have no NavMesh.");

            if (Application.isBatchMode) return;

            bool install = EditorUtility.DisplayDialog("BH3 samples",
                $"The samples need {packageList}. Install now? Reopen the sample scene once the install finishes.",
                "Install", "Not now");
            if (install) Client.AddAndRemove(missing);
        }

        private static IEnumerable<string> InstalledPackages() =>
            UnityEditor.PackageManager.PackageInfo.GetAllRegisteredPackages().Select(package => package.name);
    }
}
