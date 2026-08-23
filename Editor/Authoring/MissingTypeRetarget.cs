using System;
using System.Collections.Generic;
using System.Linq;
using ArcaneOnyx.GraphCore;
using Unity.VisualScripting;
using Unity.VisualScripting.FullSerializer;
using UnityEditor;
using UnityEngine;

namespace ArcaneOnyx.BehaviorTree.Authoring
{
    /// <summary>
    /// The Editor half of missing-type recovery: choosing a replacement type, showing what choosing it would
    /// cost, and applying it to one node, a tree, or the project.
    ///
    /// <para>
    /// The conversion itself is <see cref="MissingTypeRecovery"/> and lives in the runtime assembly, because
    /// restoring a node whose type came back is not an editing operation. What is here is everything that
    /// needs a person: which types are worth offering, what carries over, and undo.
    /// </para>
    /// </summary>
    public static class MissingTypeRetarget
    {
        /// <summary>What retargeting onto a given type would keep and what it would drop.</summary>
        public sealed class Preview
        {
            public Type Target;
            public string Failure;

            /// <summary>Serialized members of the old node that the target also declares.</summary>
            public readonly List<string> KeptMembers = new List<string>();

            /// <summary>Serialized members of the old node the target has nowhere to put.</summary>
            public readonly List<string> DroppedMembers = new List<string>();

            /// <summary>Ports whose connections land on a real port of the target.</summary>
            public readonly List<string> KeptConnections = new List<string>();

            /// <summary>Ports whose connections survive only as invalid ports, for the author to re-wire.</summary>
            public readonly List<string> StrandedConnections = new List<string>();

            public bool CanApply => Failure == null;
        }

        /// <summary>
        /// The types worth offering for <paramref name="placeholder"/>, best first.
        ///
        /// <para>
        /// A type whose short name matches the one that went missing comes first, because a type that was
        /// renamed or moved between namespaces is the case this feature exists for and it is nearly always
        /// the right answer. After that, name similarity, then the canvas's own menu order — the candidate
        /// set is exactly what the canvas would let you create, so nothing is offered that could not have
        /// been placed by hand.
        /// </para>
        /// </summary>
        public static List<Type> CandidateTypes(MissingType placeholder)
        {
            string shortName = placeholder == null ? string.Empty : placeholder.ShortFormerTypeName;

            return AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(SafeGetTypes)
                .Where(type => type.IsClass && !type.IsAbstract && typeof(BehaviorTreeNode).IsAssignableFrom(type))
                .Where(NodeUtil.NodeOfTypeCanBeCreated)
                .Distinct()
                .OrderByDescending(type => string.Equals(type.Name, shortName, StringComparison.Ordinal))
                .ThenByDescending(type => Similarity(type.Name, shortName))
                .ThenBy(NodeUtil.GetNodeGraphCreateMenu, StringComparer.Ordinal)
                .ToList();
        }

        private static IEnumerable<Type> SafeGetTypes(System.Reflection.Assembly assembly)
        {
            try { return assembly.GetTypes(); }
            catch (System.Reflection.ReflectionTypeLoadException exception) { return exception.Types.Where(t => t != null); }
            catch (Exception) { return Enumerable.Empty<Type>(); }
        }

        /// <summary>
        /// Crude on purpose: this only has to sort a dropdown, and the exact-name case that matters most is
        /// already handled before it is consulted.
        /// </summary>
        private static int Similarity(string candidate, string former)
        {
            if (string.IsNullOrEmpty(former) || string.IsNullOrEmpty(candidate)) return 0;

            int shared = 0;
            int length = Math.Min(candidate.Length, former.Length);

            while (shared < length && char.ToLowerInvariant(candidate[shared]) == char.ToLowerInvariant(former[shared]))
            {
                shared++;
            }

            return shared;
        }

        /// <summary>
        /// What retargeting <paramref name="placeholder"/> onto <paramref name="target"/> would do, worked
        /// out by actually doing it to a throwaway node rather than by predicting it. Predicting means a
        /// second implementation of the carry-over rule, and a preview that can disagree with the thing it
        /// previews is worse than none.
        /// </summary>
        public static Preview PreviewRetarget(MissingType placeholder, Type target)
        {
            var preview = new Preview { Target = target };

            if (placeholder == null || target == null)
            {
                preview.Failure = "nothing selected.";
                return preview;
            }

            var rebuilt = MissingTypeRecovery.Rebuild(placeholder, target, out var failure);
            if (rebuilt == null)
            {
                preview.Failure = failure;
                return preview;
            }

            rebuilt.Define();

            var formerMembers = MemberNames(placeholder.formerValue);
            var targetMembers = MemberNames(Unity.VisualScripting.Serialization.Serialize(rebuilt, true).json);

            foreach (var member in formerMembers)
            {
                if (targetMembers.Contains(member)) preview.KeptMembers.Add(member);
                else preview.DroppedMembers.Add(member);
            }

            var targetPortKeys = new HashSet<string>(rebuilt.validPorts.Select(port => port.key));

            foreach (var port in placeholder.ports)
            {
                if (!port.hasAnyConnection) continue;

                if (targetPortKeys.Contains(port.key)) preview.KeptConnections.Add(port.key);
                else preview.StrandedConnections.Add(port.key);
            }

            // A value typed straight into a port lives in defaultValues, not among the node's members, so
            // without this it is user data the preview never mentions -- an author whose node was all port
            // literals would read "keeps 0 values" and be wrong.
            //
            // Read from the preserved document rather than from the live placeholder, which never has any:
            // Define() clears defaultValues and then restores only the keys the node's own Definition
            // declares, and MissingType declares none. So the placeholder's dictionary is always empty and
            // the only surviving copy of an inline value is the one inside formerValue.
            var targetPorts = rebuilt.valueInputs.ToDictionary(port => port.key, port => port.key);

            foreach (var key in PreservedInlineValueKeys(placeholder))
            {
                if (targetPorts.ContainsKey(key)) preview.KeptMembers.Add(key + " (inline value)");
                else preview.DroppedMembers.Add(key + " (inline value)");
            }

            return preview;
        }

        /// <summary>
        /// The port keys the missing node had an inline value on, read out of the preserved document — the
        /// only place they still exist. See the note in <see cref="PreviewRetarget"/> for why the live
        /// placeholder cannot answer this.
        /// </summary>
        private static IEnumerable<string> PreservedInlineValueKeys(MissingType placeholder)
        {
            if (!placeholder.HasPreservedState) yield break;
            if (!fsJsonParser.Parse(placeholder.formerValue, out var data).Succeeded || !data.IsDictionary) yield break;
            if (!data.AsDictionary.TryGetValue("defaultValues", out var defaults) || !defaults.IsDictionary) yield break;

            foreach (var key in defaults.AsDictionary.Keys) yield return key;
        }

        private static HashSet<string> MemberNames(string json)
        {
            var names = new HashSet<string>();

            if (string.IsNullOrEmpty(json)) return names;
            if (!fsJsonParser.Parse(json, out var data).Succeeded || !data.IsDictionary) return names;

            foreach (var key in data.AsDictionary.Keys)
            {
                if (!MissingTypeSerialization.IsBaseNodeMember(key)) names.Add(key);
            }

            return names;
        }

        /// <summary>
        /// Retargets one node, recording undo against the asset.
        ///
        /// <para>
        /// Undo is registered on the asset by name rather than through <c>UndoUtility.RecordEditedObject</c>,
        /// which resolves its target from an override stack that only exists inside a canvas draw frame.
        /// Every caller here — a dropdown callback, a context menu, a batch over the project — runs outside
        /// one, where that call silently records nothing and the edit is lost on the next domain reload.
        /// </para>
        /// </summary>
        public static bool Apply(
            BehaviorTreeGraphAsset asset, MissingType placeholder, Type target, out string failure)
        {
            failure = null;

            if (asset == null || asset.graph == null) { failure = "no tree."; return false; }

            Undo.RegisterCompleteObjectUndo(asset, "Retarget missing node");

            if (MissingTypeRecovery.Retarget(asset.graph, placeholder, target, out failure) == null) return false;

            EditorUtility.SetDirty(asset);
            return true;
        }

        /// <summary>
        /// Retargets every placeholder in one tree that stands in for <paramref name="formerType"/>. One undo
        /// entry covers the lot, because undoing half of a rename is not something anyone wants.
        /// </summary>
        public static int ApplyToTree(BehaviorTreeGraphAsset asset, string formerType, Type target)
        {
            if (asset == null || asset.graph == null) return 0;

            var placeholders = asset.graph.Nodes.OfType<MissingType>()
                .Where(node => node.formerType == formerType)
                .ToList();

            if (placeholders.Count == 0) return 0;

            Undo.RegisterCompleteObjectUndo(asset, "Retarget missing nodes");

            int converted = 0;

            foreach (var placeholder in placeholders)
            {
                if (MissingTypeRecovery.Retarget(asset.graph, placeholder, target, out var failure) != null)
                {
                    converted++;
                    continue;
                }

                Debug.LogWarning($"[BH3] could not retarget a '{formerType}' placeholder: {failure}", asset);
            }

            if (converted > 0) EditorUtility.SetDirty(asset);

            return converted;
        }

        /// <summary>What a project-wide retarget did, so the caller can report it rather than guess.</summary>
        public struct ProjectResult
        {
            public int Nodes;

            /// <summary>
            /// The trees that changed, by path. Paths rather than a count because this operation writes to
            /// disk across the whole project, and a reviewer's first question is which files moved.
            /// </summary>
            public List<string> Trees;

            public override string ToString()
            {
                int trees = Trees?.Count ?? 0;

                return trees == 0
                    ? "nothing to convert"
                    : $"{Nodes} node(s) in {trees} tree(s): {string.Join(", ", Trees)}";
            }
        }

        /// <summary>
        /// Retargets every placeholder for <paramref name="formerType"/> in every behavior tree in the
        /// project, and saves only the trees that changed.
        /// </summary>
        public static ProjectResult ApplyToProject(string formerType, Type target)
        {
            var result = new ProjectResult { Trees = new List<string>() };
            var guids = AssetDatabase.FindAssets("t:" + nameof(BehaviorTreeGraphAsset));

            // One rename is one decision however many trees it reached, so it is one undo step. Without the
            // group, undoing it means pressing Ctrl+Z once per asset and having no way to know when to stop.
            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();

            try
            {
                for (int index = 0; index < guids.Length; index++)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guids[index]);

                    if (EditorUtility.DisplayCancelableProgressBar(
                            "Retargeting missing nodes", path, (float)index / Mathf.Max(1, guids.Length)))
                    {
                        break;
                    }

                    var asset = AssetDatabase.LoadAssetAtPath<BehaviorTreeGraphAsset>(path);
                    if (asset == null) continue;

                    int converted = ApplyToTree(asset, formerType, target);
                    if (converted == 0) continue;

                    result.Nodes += converted;
                    result.Trees.Add(path);

                    // Only this asset. AssetDatabase.SaveAssets() would flush every dirty asset in the
                    // project, so a retarget would quietly commit a half-finished scene or prefab the author
                    // had deliberately left unsaved -- a side effect nobody asked this command for.
                    AssetDatabase.SaveAssetIfDirty(asset);
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                Undo.CollapseUndoOperations(undoGroup);
            }

            return result;
        }

        /// <summary>
        /// Every placeholder in the project, grouped by the type they stand in for — what the finder window
        /// lists and what makes "this is one rename, not fourteen problems" visible.
        /// </summary>
        public static Dictionary<string, List<string>> FindAcrossProject()
        {
            var byFormerType = new Dictionary<string, List<string>>(StringComparer.Ordinal);

            foreach (var guid in AssetDatabase.FindAssets("t:" + nameof(BehaviorTreeGraphAsset)))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<BehaviorTreeGraphAsset>(path);

                if (asset == null || asset.graph == null) continue;

                foreach (var placeholder in asset.graph.Nodes.OfType<MissingType>())
                {
                    string key = string.IsNullOrEmpty(placeholder.formerType) ? "(unknown)" : placeholder.formerType;

                    if (!byFormerType.TryGetValue(key, out var paths))
                    {
                        paths = new List<string>();
                        byFormerType[key] = paths;
                    }

                    paths.Add(path);
                }
            }

            return byFormerType;
        }
    }
}
