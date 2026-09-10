using System;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;

namespace ArcaneOnyx.BehaviorTree.Installation
{
    /// <summary>
    /// Makes a project ready to use BH3 in one click: the Visual Scripting setup the manual
    /// <b>Edit → Project Settings → Visual Scripting</b> steps would otherwise be, done in order and with
    /// the entries BH3 and its modules need already merged in.
    /// <para>
    /// Runs on a project in any state. Options the project already has are kept; only what is missing is
    /// added, so a project with its own type options loses nothing. Safe to run twice.
    /// </para>
    /// </summary>
    public static class BH3Installer
    {
        public const string MenuPath = "Tools/BH3/Install";

        /// <summary>
        /// The assemblies the reference project's Visual Scripting settings carry beyond the package
        /// defaults, keeping only those that belong to BH3 or to the modules it depends on. That leaves
        /// these two: VisualScriptingExtension and BlockVariables are not in the reference settings either,
        /// since their units are found without it. Entries from any other module are deliberately absent.
        /// </summary>
        public static readonly IReadOnlyList<string> RequiredAssemblies = new[]
        {
            "ArcaneOnyx.BehaviorTree",
            "ArcaneOnyx.GraphCore",
        };

        /// <summary>The type options the reference project adds for BH3, under the same restriction.</summary>
        public static readonly IReadOnlyList<Type> RequiredTypes = new[]
        {
            typeof(GuardTrigger),
        };

        [MenuItem(MenuPath, priority = 0)]
        public static void InstallFromMenu()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorUtility.DisplayDialog("BH3", "The Editor is still compiling or importing. Run Install once it settles.", "OK");
                return;
            }

            var report = Install();
            EditorUtility.DisplayDialog("BH3", report.ToString(), "OK");
        }

        /// <summary>
        /// The whole installation, for a script or a batch-mode call (<c>-executeMethod</c>). The last
        /// step runs on the next editor update: it writes generated scripts into <c>Assets</c> and
        /// refreshes, so a recompile follows. A batch-mode caller must let that update happen before
        /// quitting.
        /// </summary>
        public static Report Install()
        {
            var report = new Report();

            // 1. What the "Initialize Visual Scripting" button does: marks the project as one that uses
            //    Visual Scripting, which brings the plugin container up and imports the stock units.
            report.InitializedVisualScripting = !VSUsageUtility.isVisualScriptingUsed;
            VSUsageUtility.isVisualScriptingUsed = true;

            // 2. Merge the node library and type options. The lists are the live configuration; saving
            //    goes through the same metadata path the settings page uses, so the asset on disk agrees.
            var configuration = BoltCore.Configuration;

            foreach (var assembly in RequiredAssemblies)
            {
                LooseAssemblyName name = assembly;
                if (configuration.assemblyOptions.Contains(name)) continue;

                configuration.assemblyOptions.Add(name);
                report.AddedAssemblies.Add(assembly);
            }

            foreach (var type in RequiredTypes)
            {
                if (configuration.typeOptions.Contains(type)) continue;

                configuration.typeOptions.Add(type);
                report.AddedTypes.Add(type.FullName);
            }

            configuration.Save();
            configuration.SaveProjectSettingsAsset(immediately: true);
            Codebase.UpdateSettings();

            // 3. "Regenerate Nodes" under Node Library: rebuilds the unit options database from the
            //    assemblies above, which is what makes BH3's units show up in the finder.
            UnitBase.Rebuild();
            report.RegeneratedNodes = true;

            // 4. "Generate" under Custom Inspector Properties. Visual Scripting defers the settings save
            //    above to the next editor update, and generating refreshes the asset database, whose domain
            //    reload would drop that pending save. Queued behind it, so the save lands first.
            EditorApplication.delayCall += GenerateInspectorProperties;
            report.QueuedInspectorProperties = true;

            Debug.Log($"[BH3Installer] {report}");

            return report;
        }

        private static void GenerateInspectorProperties()
        {
            SerializedPropertyProviderProvider.instance.GenerateProviderScripts();
            Debug.Log("[BH3Installer] Generated custom inspector properties.");
        }

        /// <summary>What one run did, step by step, in the words the dialog and the log show.</summary>
        public sealed class Report
        {
            public bool InitializedVisualScripting;
            public readonly List<string> AddedAssemblies = new List<string>();
            public readonly List<string> AddedTypes = new List<string>();
            public bool RegeneratedNodes;
            public bool QueuedInspectorProperties;

            public override string ToString()
            {
                var lines = new List<string>
                {
                    InitializedVisualScripting
                        ? "Initialized Visual Scripting."
                        : "Visual Scripting was already initialized.",
                    AddedAssemblies.Count > 0
                        ? $"Added to the node library: {string.Join(", ", AddedAssemblies)}."
                        : "Node library already had every BH3 assembly.",
                    AddedTypes.Count > 0
                        ? $"Added type options: {string.Join(", ", AddedTypes)}."
                        : "Type options already had every BH3 type.",
                };

                if (RegeneratedNodes) lines.Add("Regenerated nodes.");
                if (QueuedInspectorProperties) lines.Add("Custom inspector properties generate next; expect one recompile.");

                lines.Add("BH3 is ready: Create → Visual Scripting → Behavior Tree.");

                return string.Join("\n", lines);
            }
        }
    }
}
