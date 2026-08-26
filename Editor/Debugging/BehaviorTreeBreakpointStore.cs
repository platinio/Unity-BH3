using System;
using System.Collections.Generic;
using System.IO;
using ArcaneOnyx.BehaviorTree.Debugging;
using UnityEditor;
using UnityEngine;

namespace ArcaneOnyx.BehaviorTree
{
    /// <summary>
    /// Where armed breakpoints are kept between domain reloads, and the one API the editor UI arms through.
    ///
    /// <para>
    /// <b>Per user, never committed.</b> The file is <c>UserSettings/BH3Breakpoints.json</c>, which this
    /// project already gitignores. That is the same conclusion Unreal reached and then acted on: Blueprint
    /// breakpoints lived on the <c>UBlueprint</c> asset itself through UE 5.0, and in 5.1 Epic moved them out
    /// to per-user project settings keyed by asset plus node <c>FGuid</c>, because a breakpoint on a shared
    /// asset dirties a file everyone owns and pauses editors belonging to people who did not set it. A
    /// breakpoint is a statement about what you are debugging this afternoon, not a property of the tree —
    /// nobody commits their code editor's breakpoints either. Sharing one is copying the file, deliberately.
    /// </para>
    ///
    /// <para>
    /// Arming goes through here rather than through <see cref="BehaviorTreeBreakpoints"/> directly, so that
    /// persisting cannot be forgotten at a call site. The runtime store stays the thing that matches; this is
    /// the thing that remembers.
    /// </para>
    /// </summary>
    [InitializeOnLoad]
    public static class BehaviorTreeBreakpointStore
    {
        /// <summary>
        /// 2 added <c>compare</c> and <c>breakOnHit</c>. Version 1 files still load: a missing operator reads
        /// as equality when a value was given and "any write" when none was, which is what those files meant.
        /// </summary>
        private const int Version = 2;
        private const string FileName = "BH3Breakpoints.json";

        static BehaviorTreeBreakpointStore()
        {
            Load();

            // A hit count is about one run. Carrying last session's into a fresh one would put "12 hits" beside
            // a breakpoint that has not fired yet, which is the sort of confident wrong answer this whole
            // component exists to remove.
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.EnteredPlayMode) BehaviorTreeBreakpoints.ResetHitCounts();
            };
        }

        /// <summary>
        /// Reads and writes somewhere other than the per-user file. Null means the real location.
        ///
        /// <para>
        /// This exists for the tests, and it is not merely a convenience there: without it, running the suite
        /// would overwrite whatever breakpoints the developer had armed, which is a test that damages the
        /// thing it is testing. It is also the seam a "load a colleague's breakpoint file" command would use.
        /// </para>
        /// </summary>
        public static string OverridePath { get; set; }

        /// <summary>
        /// Where the file lives. Beside Unity's own <c>EditorUserSettings.asset</c>, which is the convention
        /// for per-user editor state in a Unity project and is why the folder is already ignored.
        /// </summary>
        public static string FilePath
        {
            get
            {
                if (!string.IsNullOrEmpty(OverridePath)) return OverridePath;

                var projectRoot = Directory.GetParent(Application.dataPath);

                return projectRoot == null
                    ? Path.Combine("UserSettings", FileName)
                    : Path.Combine(projectRoot.FullName, "UserSettings", FileName);
            }
        }

        #region Arming

        /// <summary>Arms or edits the breakpoint on a node, labels it, and writes the file.</summary>
        public static BehaviorTreeBreakpoint SetNode(
            BehaviorTreeNode node, BehaviorTreeNodeBreakEvents events, GraphCore.IGraphContext context = null)
        {
            if (node == null) return null;

            var breakpoint = BehaviorTreeBreakpoints.SetNode(node.guid, events);
            Label(breakpoint, node.NodeName, context);
            Save();

            return breakpoint;
        }

        /// <summary>Adds one moment to a node's breakpoint, or removes it if it was already armed.</summary>
        public static void ToggleNodeEvent(
            BehaviorTreeNode node, BehaviorTreeNodeBreakEvents moment, GraphCore.IGraphContext context = null)
        {
            if (node == null) return;

            var current = BehaviorTreeBreakpoints.ForNode(node.guid);
            var events = current?.Events ?? BehaviorTreeNodeBreakEvents.None;

            SetNode(node, events.Toggle(moment), context);
        }

        /// <summary>
        /// Arms or edits the breakpoint on a guard.
        ///
        /// <para>
        /// Labelled by what the guard reads rather than by its own name. Every
        /// <see cref="BooleanConditionalExecution"/> in the project is called "Boolean Conditional Execution"
        /// (Finding 9), so a panel listing four guards by name lists the same string four times — and the log
        /// line on a hit would be "a guard turned false", which is the question restated.
        /// </para>
        /// </summary>
        public static BehaviorTreeBreakpoint SetGuard(
            ConditionalExecution guard, BehaviorTreeGuardBreakOn breakOn, GraphCore.IGraphContext context = null)
        {
            if (guard == null) return null;

            var breakpoint = BehaviorTreeBreakpoints.SetGuard(guard.guid, breakOn);
            Label(breakpoint, GuardLabel(guard), context);
            Save();

            return breakpoint;
        }

        /// <summary>Arms or edits the breakpoint on a variable. Null expected value means any write.</summary>
        public static BehaviorTreeBreakpoint SetVariable(
            string key,
            string expectedValue = null,
            BehaviorTreeVariableCompare compare = BehaviorTreeVariableCompare.Equals)
        {
            var breakpoint = BehaviorTreeBreakpoints.SetVariable(key, expectedValue, compare);

            if (breakpoint != null) BehaviorTreeBreakpoints.Describe(breakpoint, key, null);

            Save();

            return breakpoint;
        }

        /// <summary>Sets which matching occurrence stops the editor, 1-based.</summary>
        public static void SetBreakOnHit(BehaviorTreeBreakpoint breakpoint, int occurrence)
        {
            BehaviorTreeBreakpoints.SetBreakOnHit(breakpoint, occurrence);
            Save();
        }

        public static void Remove(BehaviorTreeBreakpoint breakpoint)
        {
            BehaviorTreeBreakpoints.Remove(breakpoint);
            Save();
        }

        public static void SetEnabled(BehaviorTreeBreakpoint breakpoint, bool enabled)
        {
            BehaviorTreeBreakpoints.SetEnabled(breakpoint, enabled);
            Save();
        }

        public static void Clear()
        {
            BehaviorTreeBreakpoints.Clear();
            Save();
        }

        /// <summary>
        /// The name a guard should be listed under: what its value input ultimately reads, in brackets.
        ///
        /// <para>
        /// Asks <see cref="BehaviorTreeGraphTopology"/> rather than walking the ports again. That walk already
        /// exists there, is already the rule the why-inspector's sentences use, and already handles the case
        /// that makes a naive version wrong — it follows past a <c>Not</c> to the thing being negated instead
        /// of naming the guard "Not". Two implementations of "what is this guard called" that could disagree
        /// is exactly the failure the debugger's shared-resolution rule exists to prevent.
        /// </para>
        /// </summary>
        private static string GuardLabel(ConditionalExecution guard)
        {
            if (guard.graph is not BehaviorTreeGraph graph) return guard.NodeName;

            var topology = BehaviorTreeGraphTopology.From(graph);

            if (!topology.TryGetNode(guard.guid, out var info) || string.IsNullOrEmpty(info.DisplayName))
            {
                return guard.NodeName;
            }

            // The topology returns the guard's own name when it could not resolve a source, and bracketing
            // that would dress up a failure as an answer.
            return info.DisplayName == guard.NodeName ? guard.NodeName : $"[{info.DisplayName}]";
        }

        /// <summary>
        /// Records what to call a breakpoint and which tree it came from.
        ///
        /// <para>
        /// The tree is read from the canvas rather than from the node, because a node's <c>graph</c> is the
        /// graph and not the asset that holds it, and the asset is what has a name. Both are cosmetic: a
        /// breakpoint matches on node guid alone, so a null here costs a nicer panel row and nothing else.
        /// </para>
        /// </summary>
        private static void Label(BehaviorTreeBreakpoint breakpoint, string label, GraphCore.IGraphContext context)
        {
            if (breakpoint == null) return;

            var asset = context?.reference?.root as UnityEngine.Object;

            BehaviorTreeBreakpoints.Describe(breakpoint, label, asset == null ? null : asset.name);
        }

        #endregion

        #region File

        public static void Save()
        {
            var file = new BreakpointFile { version = Version };

            foreach (var breakpoint in BehaviorTreeBreakpoints.All)
            {
                file.breakpoints.Add(new BreakpointEntry
                {
                    kind = breakpoint.Kind.ToString(),
                    target = breakpoint.TargetGuid == Guid.Empty ? string.Empty : breakpoint.TargetGuid.ToString(),
                    variableKey = breakpoint.VariableKey,
                    events = breakpoint.Events.ToString(),
                    guardBreakOn = breakpoint.GuardBreakOn.ToString(),
                    expectedValue = breakpoint.ExpectedValue,
                    compare = breakpoint.Compare.ToString(),
                    breakOnHit = breakpoint.BreakOnHit,
                    enabled = breakpoint.Enabled,
                    label = breakpoint.Label,
                    treeName = breakpoint.TreeName,
                });
            }

            try
            {
                var path = FilePath;
                var folder = Path.GetDirectoryName(path);

                if (!string.IsNullOrEmpty(folder)) Directory.CreateDirectory(folder);

                File.WriteAllText(path, JsonUtility.ToJson(file, true));
            }
            catch (Exception e)
            {
                // A debugging aid that can break the thing it watches is worse than no debugging aid, and that
                // goes double for one that throws out of a right-click. Losing breakpoints across a reload is
                // an annoyance; an exception mid-repaint is a broken editor.
                Debug.LogWarning($"BH3: could not save breakpoints to {FilePath}: {e.Message}");
            }
        }

        public static void Load()
        {
            BehaviorTreeBreakpoints.Clear();

            string text;

            try
            {
                var path = FilePath;
                if (!File.Exists(path)) return;

                text = File.ReadAllText(path);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"BH3: could not read breakpoints from {FilePath}: {e.Message}");
                return;
            }

            BreakpointFile file;

            try
            {
                file = JsonUtility.FromJson<BreakpointFile>(text);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"BH3: {FilePath} is not a breakpoint file: {e.Message}");
                return;
            }

            if (file?.breakpoints == null) return;

            foreach (var entry in file.breakpoints)
            {
                var breakpoint = Rebuild(entry);
                if (breakpoint == null) continue;

                BehaviorTreeBreakpoints.Add(breakpoint);
                BehaviorTreeBreakpoints.Describe(breakpoint, entry.label, entry.treeName);
                BehaviorTreeBreakpoints.SetEnabled(breakpoint, entry.enabled);

                // Absent in a version-1 file, where it reads as 0 and clamps to "the first one" — which is what
                // every breakpoint written before this field existed meant.
                BehaviorTreeBreakpoints.SetBreakOnHit(breakpoint, entry.breakOnHit);
            }
        }

        /// <summary>
        /// One saved entry back into a breakpoint, or null if it cannot be read.
        ///
        /// <para>
        /// Skips rather than throws on anything it does not recognise. The file is per-user and hand-editable
        /// by design, so a stale enum name from an older build is a thing that will happen, and dropping one
        /// row beats refusing to load the other eleven.
        /// </para>
        /// </summary>
        private static BehaviorTreeBreakpoint Rebuild(BreakpointEntry entry)
        {
            if (entry == null) return null;
            if (!Enum.TryParse<BehaviorTreeBreakpointKind>(entry.kind, out var kind)) return null;

            if (kind == BehaviorTreeBreakpointKind.Variable)
            {
                if (string.IsNullOrEmpty(entry.variableKey)) return null;

                // A version-1 file has no `compare`, and every one of those meant exactly this: any write when
                // no value was given, equality when one was. Reading the absent field as Changed regardless
                // would silently turn "break when ammo is 0" into "break on every ammo write".
                var compare = Enum.TryParse<BehaviorTreeVariableCompare>(entry.compare, out var parsed)
                    ? parsed
                    : BehaviorTreeVariableCompare.Equals;

                return BehaviorTreeBreakpoint.ForVariable(entry.variableKey, entry.expectedValue, compare);
            }

            if (!Guid.TryParse(entry.target, out var target) || target == Guid.Empty) return null;

            if (kind == BehaviorTreeBreakpointKind.Guard)
            {
                return BehaviorTreeBreakpoint.ForGuard(
                    target,
                    Enum.TryParse<BehaviorTreeGuardBreakOn>(entry.guardBreakOn, out var breakOn)
                        ? breakOn
                        : BehaviorTreeGuardBreakOn.EitherWay);
            }

            if (!Enum.TryParse<BehaviorTreeNodeBreakEvents>(entry.events, out var events) ||
                events == BehaviorTreeNodeBreakEvents.None)
            {
                return null;
            }

            return BehaviorTreeBreakpoint.ForNode(target, events);
        }

        // Enums are written as names rather than numbers because this is a file someone may open, hand-edit or
        // send to a colleague, and "Enter, Aborted" survives a renumbering that "5" does not.
        [Serializable]
        private class BreakpointEntry
        {
            public string kind;
            public string target;
            public string variableKey;
            public string events;
            public string guardBreakOn;
            public string expectedValue;
            public string compare;
            public int breakOnHit;
            public bool enabled;
            public string label;
            public string treeName;
        }

        [Serializable]
        private class BreakpointFile
        {
            public int version;
            public List<BreakpointEntry> breakpoints = new();
        }

        #endregion
    }
}
