using System.Collections.Generic;
using Unity.VisualScripting;
using Unity.VisualScripting.FullSerializer;

namespace ArcaneOnyx.BehaviorTree
{
    /// <summary>
    /// Turns a node whose C# type no longer exists into a <see cref="MissingType"/> that <em>keeps what the
    /// node held</em>, so a deleted or renamed type is recoverable rather than merely survivable.
    ///
    /// <para>
    /// This runs before deserialization, on the raw document, because that is the only moment the old data
    /// still exists: the type is gone, so nothing can deserialize the node, and the first time the asset is
    /// written back every member the missing type declared is dropped. What the placeholder does not copy
    /// here is lost for good.
    /// </para>
    ///
    /// <para>
    /// Only the <em>forward</em> direction is document surgery. Putting a node back is done from the
    /// deserialized placeholder by <see cref="MissingTypeRecovery"/>, which needs no JSON at all — see
    /// <see cref="MissingType.formerValue"/> for why the two halves are split that way.
    /// </para>
    /// </summary>
    public static class MissingTypeSerialization
    {
        public const string MissingTypeName = "ArcaneOnyx.BehaviorTree.MissingType";

        private const string TypeKey = "$type";
        private const string IdKey = "$id";
        private const string GuidKey = "guid";
        private const string PositionRectKey = "positionRect";
        private const string DefaultValuesKey = "defaultValues";

        /// <summary>
        /// The members every <see cref="BehaviorTreeNode"/> carries, whatever it is.
        ///
        /// <para>
        /// Declared once because two surfaces answer "which of this node's values are its own" — the
        /// inspector's preserved-state list and the retarget preview's kept/dropped list — and they are read
        /// side by side by someone deciding whether to commit. Two copies drift the moment the base class
        /// gains a member, and the failure is the two of them disagreeing about what carries over, which is
        /// exactly the trust the preview exists to earn.
        /// </para>
        /// </summary>
        public static readonly IReadOnlyCollection<string> BaseNodeMembers = new HashSet<string>
        {
            GuidKey, PositionRectKey, DefaultValuesKey,
            "position", "relations", "LastExecutionStatus", "runtimeException",
            nameof(MissingType.formerType), nameof(MissingType.formerValue), nameof(MissingType.formerObjects),
        };

        /// <summary>
        /// Whether a serialized member belongs to every node rather than to the type that declared it.
        /// Serializer bookkeeping (<c>$type</c>, <c>$id</c>, …) counts, so a caller can pass raw keys.
        /// </summary>
        public static bool IsBaseNodeMember(string key)
        {
            return string.IsNullOrEmpty(key)
                   || key.StartsWith("$", System.StringComparison.Ordinal)
                   || ((HashSet<string>)BaseNodeMembers).Contains(key);
        }

        /// <summary>
        /// Rewrites every node with an unresolvable type in <paramref name="data"/>, and reports which type
        /// names were rewritten. Returns false and leaves <paramref name="data"/> untouched when there is
        /// nothing to do, which is the overwhelmingly common case.
        /// </summary>
        public static bool Convert(ref SerializationData data, out List<string> convertedTypeNames)
        {
            convertedTypeNames = null;

            // The cheap gate. A healthy asset never reaches the parser, so the parse/print round trip costs
            // nothing on the path every asset in the project takes.
            if (!ContainsUnresolvableType(data.json)) return false;

            if (!fsJsonParser.Parse(data.json, out var root).Succeeded) return false;

            convertedTypeNames = new List<string>();
            ConvertNodes(root, convertedTypeNames);

            if (convertedTypeNames.Count == 0)
            {
                convertedTypeNames = null;
                return false;
            }

            // Re-printing reformats the whole document, which is safe precisely because this copy is
            // transient: it is handed straight to the deserializer, and the next save regenerates the
            // document from the live objects rather than from this string.
            data = new SerializationData(fsJsonPrinter.CompressedJson(root), data.objectReferences);
            return true;
        }

        /// <summary>
        /// Whether the document names a type that cannot be resolved. Deliberately the same scan the blanket
        /// rewrite used before this existed, kept because it answers "is there anything to do" without
        /// building an object model of the entire asset.
        /// </summary>
        private static bool ContainsUnresolvableType(string json)
        {
            if (string.IsNullOrEmpty(json)) return false;

            foreach (var startIndex in json.AllIndexesOf("\"$type\":\""))
            {
                int index = startIndex + 9;
                int endIndex = json.IndexOf('"', index);
                if (endIndex < 0) continue;

                string typeName = json.Substring(index, endIndex - index);

                // A constructed generic name carries its arguments in brackets and is resolved as a whole by
                // the serializer; the scan cannot take it apart, so it is left to the serializer as before.
                if (typeName.Contains("[")) continue;

                if (!RuntimeCodebase.TryDeserializeType(typeName, out _)) return true;
            }

            return false;
        }

        private static void ConvertNodes(fsData data, List<string> convertedTypeNames)
        {
            if (data.IsList)
            {
                foreach (var item in data.AsList) ConvertNodes(item, convertedTypeNames);
                return;
            }

            if (!data.IsDictionary) return;

            var dictionary = data.AsDictionary;

            if (TryConvertNode(dictionary, convertedTypeNames)) return;

            foreach (var value in dictionary.Values) ConvertNodes(value, convertedTypeNames);
        }

        /// <summary>
        /// Converts one dictionary if it is a node whose type is gone, and reports whether it did — a
        /// converted node is not descended into, because everything below it has already been copied into
        /// <see cref="MissingType.formerValue"/> verbatim.
        /// </summary>
        private static bool TryConvertNode(Dictionary<string, fsData> dictionary, List<string> convertedTypeNames)
        {
            if (!dictionary.TryGetValue(TypeKey, out var typeData) || !typeData.IsString) return false;

            string typeName = typeData.AsString;
            if (typeName.Contains("[")) return false;
            if (RuntimeCodebase.TryDeserializeType(typeName, out _)) return false;

            // Not every unresolvable type is a node, and a placeholder that is a node cannot stand in for one
            // that is not. Anything else keeps the blanket rewrite it had before, one level up.
            if (!IsNode(dictionary)) return false;

            // Printed before the dictionary is touched, so it is the node exactly as it was written. Object
            // references inside it are indices into the asset's object table, which is why the placeholder
            // has to keep that table -- see MissingType.formerObjects.
            //
            // $id is dropped on the way out. Nothing today would trip over it -- the node is rebuilt as a
            // standalone document rather than spliced back into this one -- but an id is a position in the
            // document that produced it, and this string outlives that document by design. Keeping one would
            // leave a stale identity in the only copy of the node's data, valid solely for as long as
            // recovery avoids the one technique the id would break.
            var preserved = new Dictionary<string, fsData>(dictionary);
            preserved.Remove(IdKey);

            string formerValue = fsJsonPrinter.CompressedJson(new fsData(preserved));

            dictionary[nameof(MissingType.formerType)] = new fsData(typeName);
            dictionary[nameof(MissingType.formerValue)] = new fsData(formerValue);
            dictionary[TypeKey] = new fsData(MissingTypeName);

            convertedTypeNames.Add(typeName);
            return true;
        }

        /// <summary>
        /// Whether a dictionary is a serialized <see cref="BehaviorTreeNode"/>, decided by the members that
        /// class declares. A transition carries a <c>guid</c> too but neither of the other two, and nothing
        /// else in a tree carries all three.
        /// </summary>
        private static bool IsNode(Dictionary<string, fsData> dictionary)
        {
            return dictionary.ContainsKey(GuidKey)
                && dictionary.ContainsKey(PositionRectKey)
                && dictionary.ContainsKey(DefaultValuesKey);
        }
    }
}
