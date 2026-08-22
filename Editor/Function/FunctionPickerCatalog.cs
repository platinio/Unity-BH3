using System.Collections.Generic;
using ArcaneOnyx.VisualScriptingExtension;

namespace ArcaneOnyx.BehaviorTree
{
    /// <summary>One offered Function, described the way the picker shows it.</summary>
    public sealed class FunctionPickerEntry
    {
        public FunctionGraphAsset Function { get; }

        /// <summary>The asset name, which is what an author knows the Function by.</summary>
        public string Name { get; }

        /// <summary>
        /// The flavor as <see cref="Authoring.FunctionGraphAuthoring.DescribeFlavor"/> states it. Read
        /// rather than recomputed: the library panel of spec 03 classifies from the same call, and two
        /// surfaces disagreeing about what counts as a predicate is exactly what one derived
        /// classification exists to prevent.
        /// </summary>
        public string Flavor { get; }

        /// <summary>The heading this entry sits under, derived from <see cref="Flavor"/>.</summary>
        public string Group { get; }

        /// <summary>Declared inputs with no default — what the caller will owe this Function.</summary>
        public IReadOnlyList<string> RequiredInputs { get; }

        /// <summary>
        /// What distinguishes this entry from another of the same name, or null when the name is already
        /// unique. Only set when it is needed — see <see cref="FunctionPickerCatalog.Offer"/>.
        /// </summary>
        public string Qualifier { get; }

        /// <summary>
        /// The row text: the name, whatever it takes to tell it apart, and what it needs. The flavor is not
        /// repeated here because the group heading already states it, and the dropdown's search matches this
        /// string — so the inputs being in it makes "Agent" a usable query.
        /// </summary>
        public string Label { get; }

        public FunctionPickerEntry(
            FunctionGraphAsset function,
            string name,
            string flavor,
            string group,
            IReadOnlyList<string> requiredInputs,
            string qualifier = null)
        {
            Function = function;
            Name = name;
            Flavor = flavor;
            Group = group;
            RequiredInputs = requiredInputs;
            Qualifier = qualifier;

            var label = string.IsNullOrEmpty(qualifier) ? name : $"{name} ({qualifier})";

            Label = requiredInputs.Count == 0
                ? label
                : $"{label} — needs {string.Join(", ", requiredInputs)}";
        }
    }

    /// <summary>
    /// Which Functions may legally fill a port, and how each one reads in the dropdown.
    ///
    /// <para>
    /// Separated from the inspector that draws it so the rule is testable without an IMGUI event loop. The
    /// interesting part of this feature is the filter, and a filter that can only be exercised by clicking
    /// is a filter nobody checks.
    /// </para>
    /// </summary>
    public static class FunctionPickerCatalog
    {
        public const string PredicateGroup = "Predicates (bool)";
        public const string QueryGroup = "Queries";
        public const string ValueGroup = "Values";
        public const string NoResultGroup = "No result";

        /// <summary>Every Function in the project that can legally fill the constrained port.</summary>
        public static List<FunctionPickerEntry> Offer(FunctionPortConstraint constraint) =>
            Offer(constraint, Authoring.FunctionGraphAuthoring.FindFunctions());

        /// <summary>
        /// The filtering rule, over a supplied set rather than the whole project so it can be tested
        /// against assets a test built itself.
        /// </summary>
        public static List<FunctionPickerEntry> Offer(
            FunctionPortConstraint constraint, IEnumerable<FunctionGraphAsset> functions)
        {
            var survivors = new List<FunctionGraphAsset>();
            if (functions == null) return new List<FunctionPickerEntry>();

            foreach (var function in functions)
            {
                if (function == null) continue;
                if (!constraint.Satisfies(function.ResultType)) continue;

                survivors.Add(function);
            }

            // Disambiguated against the survivors rather than the whole project, deliberately: a name is
            // only ambiguous among the rows actually shown, and qualifying a row because of a Function the
            // author cannot see would be noise justified by something invisible.
            var offered = DescribeAll(survivors);

            // Grouped first so the dropdown's sections come out in a stable order, then by name inside each,
            // because a project with fifty Functions is the case this feature exists for.
            offered.Sort((left, right) =>
            {
                var byGroup = GroupRank(left.Group).CompareTo(GroupRank(right.Group));
                if (byGroup != 0) return byGroup;

                var byName = string.Compare(left.Name, right.Name, System.StringComparison.OrdinalIgnoreCase);

                // Two Functions can share a name -- that is what Disambiguate exists for -- and List.Sort is
                // not stable, so tying on name alone would let the two rows swap places between openings of
                // the same dropdown.
                return byName != 0
                    ? byName
                    : string.Compare(left.Label, right.Label, System.StringComparison.OrdinalIgnoreCase);
            });

            return offered;
        }

        /// <summary>
        /// Describes a set of Functions as picker entries, qualifying any whose names collide within that
        /// set. Unfiltered and unsorted — for a caller that has already decided what to show and only needs
        /// each one's row text, such as a list of what was <em>refused</em>.
        /// </summary>
        public static List<FunctionPickerEntry> DescribeAll(IEnumerable<FunctionGraphAsset> functions)
        {
            var entries = new List<FunctionPickerEntry>();
            if (functions == null) return entries;

            foreach (var function in functions)
            {
                if (function == null) continue;
                entries.Add(Describe(function));
            }

            Disambiguate(entries);

            return entries;
        }

        /// <summary>How one Function reads, whether or not it is currently offered.</summary>
        public static FunctionPickerEntry Describe(FunctionGraphAsset function, string qualifier = null)
        {
            if (function == null) return null;

            var flavor = Authoring.FunctionGraphAuthoring.DescribeFlavor(function);
            var required = new List<string>();

            foreach (var input in function.Inputs)
            {
                // hasDefaultValue is what makes an input optional, so its absence is what the caller owes.
                if (input.hasDefaultValue) continue;
                required.Add(input.key);
            }

            return new FunctionPickerEntry(
                function, function.name, flavor, GroupFor(flavor), required, qualifier);
        }

        /// <summary>
        /// Qualifies entries whose name is not unique, and only those, with the shortest folder suffix that
        /// actually tells them apart.
        ///
        /// <para>
        /// The immediate parent folder is not enough on its own. Under the layout spec 10 step 7 proposes,
        /// <c>Soldier/Functions/IsLowHealth</c> and <c>Archer/Functions/IsLowHealth</c> would both qualify as
        /// <c>(Functions)</c> - two identical rows again, which is the whole problem this exists to solve,
        /// and identical rows also tie the sort comparator's last tie-break so they can swap places between
        /// openings.
        /// </para>
        ///
        /// <para>
        /// Qualifying every entry instead would be the easy version and the wrong one: it lengthens the
        /// common row to solve a problem the common row does not have. So the qualifier grows only for the
        /// entries that need it, and only until it distinguishes them.
        /// </para>
        /// </summary>
        private static void Disambiguate(List<FunctionPickerEntry> entries)
        {
            var byName = new Dictionary<string, List<int>>();

            for (var i = 0; i < entries.Count; i++)
            {
                if (!byName.TryGetValue(entries[i].Name, out var indices))
                {
                    indices = new List<int>();
                    byName.Add(entries[i].Name, indices);
                }

                indices.Add(i);
            }

            foreach (var group in byName.Values)
            {
                if (group.Count < 2) continue;

                var qualifiers = ShortestDistinguishingFolders(entries, group);

                for (var i = 0; i < group.Count; i++)
                {
                    entries[group[i]] = Describe(entries[group[i]].Function, qualifiers[i]);
                }
            }
        }

        /// <summary>
        /// The fewest trailing folder segments that make every entry in the group distinct, or the whole
        /// folder path when even that does not (two assets cannot share a path, so that is unreachable in
        /// practice and answered rather than asserted).
        /// </summary>
        private static List<string> ShortestDistinguishingFolders(
            List<FunctionPickerEntry> entries, List<int> group)
        {
            var folders = new List<string[]>(group.Count);
            var deepest = 1;

            foreach (var index in group)
            {
                var segments = FolderSegmentsOf(entries[index].Function);
                folders.Add(segments);

                if (segments.Length > deepest) deepest = segments.Length;
            }

            for (var depth = 1; depth <= deepest; depth++)
            {
                var candidates = new List<string>(group.Count);
                var distinct = new HashSet<string>();

                foreach (var segments in folders)
                {
                    var qualifier = LastSegments(segments, depth);
                    candidates.Add(qualifier);
                    distinct.Add(qualifier);
                }

                if (distinct.Count == candidates.Count) return candidates;
            }

            var full = new List<string>(group.Count);
            foreach (var segments in folders) full.Add(LastSegments(segments, segments.Length));

            return full;
        }

        /// <summary>
        /// The folder a Function lives in, split into segments. Unity asset paths are always
        /// forward-slashed regardless of platform, so there is no separator normalisation to do here and
        /// none is attempted.
        /// </summary>
        private static string[] FolderSegmentsOf(FunctionGraphAsset function)
        {
            var path = UnityEditor.AssetDatabase.GetAssetPath(function);
            if (string.IsNullOrEmpty(path)) return System.Array.Empty<string>();

            var lastSlash = path.LastIndexOf('/');
            if (lastSlash < 0) return System.Array.Empty<string>();

            return path.Substring(0, lastSlash).Split('/');
        }

        private static string LastSegments(string[] segments, int count)
        {
            if (segments.Length == 0) return string.Empty;

            if (count >= segments.Length) return string.Join("/", segments);

            return string.Join("/", segments, segments.Length - count, count);
        }

        /// <summary>
        /// The heading a flavor belongs under. Several value flavors -- value (Single), value (Vector3) --
        /// share one group deliberately: the type is already in the flavor, and a heading per concrete type
        /// would make the dropdown a list of one-entry sections.
        /// </summary>
        public static string GroupFor(string flavor)
        {
            if (flavor == "predicate") return PredicateGroup;
            if (flavor == "query") return QueryGroup;
            if (flavor == "no result") return NoResultGroup;

            return ValueGroup;
        }

        private static int GroupRank(string group)
        {
            if (group == PredicateGroup) return 0;
            if (group == QueryGroup) return 1;
            if (group == ValueGroup) return 2;

            return 3;
        }
    }
}
