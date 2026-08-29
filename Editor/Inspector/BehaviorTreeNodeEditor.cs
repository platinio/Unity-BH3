using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;

namespace ArcaneOnyx.BehaviorTree
{
    /// <summary>
    /// The selected node's inspector: what is wrong with it first, then its fields.
    ///
    /// <para>
    /// The canvas badge names a defect, but only on hover, and the surface a designer goes to in order to
    /// act — this one — used to say nothing about it (Unity-BH3#24). The problems are drawn here from the
    /// same <see cref="Authoring.NodeProblemCache"/> the badge reads, so the two surfaces cannot disagree,
    /// and a problem that carries a <see cref="NodeProblemRepair"/> draws it as a button. The button appears
    /// exactly when the defect is reported, which is what the carried-repair shape exists to guarantee —
    /// the context menus' "(up to date)" contradiction cannot be rebuilt here.
    /// </para>
    /// </summary>
    [Editor(typeof(BehaviorTreeNode))]
    public class BehaviorTreeNodeEditor : Inspector
    {
        public BehaviorTreeNodeEditor(Metadata metadata) : base(metadata) { }

        private static readonly Vector2 Margin = new Vector2(15.0f, 20.0f);

        private static float Row => EditorGUIUtility.singleLineHeight;
        private static float Spacing => EditorGUIUtility.standardVerticalSpacing;

        private BehaviorTreeNode Node => metadata.value as BehaviorTreeNode;

        protected override float GetHeight(float width, GUIContent label)
        {
            // The problems span the width the boxes are actually drawn at, after OnGUI's margin.
            return ProblemsHeight(width - Margin.x)
                   + LudiqGUI.GetInspectorHeight(this, metadata, 250.0f)
                   + EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
        }

        protected override void OnGUI(Rect position, GUIContent label)
        {
            position.size -= Margin;
            position.position += Margin * 0.5f;

            // Fields first, problems under them — measured with the same call GetHeight uses, so the two
            // cannot disagree about where the section starts.
            var fields = LudiqGUI.GetInspectorHeight(this, metadata, 250.0f);
            LudiqGUI.Inspector(metadata, new Rect(position.x, position.y, position.width, fields), GUIContent.none);

            var y = position.y + fields + Spacing;
            DrawProblems(position, ref y);
        }

        // ------------------------------------------------------------------ problems

        /// <summary>
        /// One box per problem, its repair button directly beneath it. Both this and
        /// <see cref="ProblemsHeight"/> walk the cached list the same way, which is what keeps the two
        /// honest — the list itself costs a dictionary lookup after the first ask.
        /// </summary>
        private void DrawProblems(Rect position, ref float y)
        {
            var node = Node;
            if (node == null) return;

            var problems = Authoring.NodeProblemCache.For(node);
            if (problems.Count == 0) return;

            OfferedRepairs.Clear();

            foreach (var problem in problems)
            {
                var text = TextOf(problem);
                var type = TypeOf(problem);

                var box = new Rect(position.x, y, position.width, HelpBoxes.HeightFor(text, position.width, type));
                EditorGUI.HelpBox(box, text, type);
                y = box.yMax + Spacing;

                if (problem.Repair == null || !OfferedRepairs.Add(problem.Repair.Label)) continue;

                if (GUI.Button(new Rect(position.x, y, position.width, Row), problem.Repair.Label))
                {
                    // The owner is only knowable while drawing — GraphContext pushes it for the duration of
                    // this OnGUI — and passing it explicitly is what keeps the undo entry against the right
                    // object even if a repair defers part of its work.
                    if (Authoring.NodeProblemRepairs.Run(node, problem.Repair,
                            LudiqEditorUtility.editedObject.value))
                    {
                        // The repair changed the problem list, and metadata.valueChanged will not notice --
                        // the node is the same instance -- so the cached height has to be dropped by hand.
                        SetHeightDirty();
                        GUI.changed = true;
                    }
                }

                y += Row + Spacing;
            }

            y += Spacing;
        }

        private float ProblemsHeight(float width)
        {
            var node = Node;
            if (node == null) return 0.0f;

            var problems = Authoring.NodeProblemCache.For(node);
            if (problems.Count == 0) return 0.0f;

            OfferedRepairs.Clear();

            var height = 0.0f;

            foreach (var problem in problems)
            {
                height += HelpBoxes.HeightFor(TextOf(problem), width, TypeOf(problem)) + Spacing;

                if (problem.Repair != null && OfferedRepairs.Add(problem.Repair.Label))
                {
                    height += Row + Spacing;
                }
            }

            return height + Spacing;
        }

        private static string TextOf(NodeProblem problem) =>
            problem.Fix == null ? problem.Summary : $"{problem.Summary}\n{problem.Fix}";

        private static MessageType TypeOf(NodeProblem problem) =>
            problem.Severity == NodeProblemSeverity.Error ? MessageType.Error : MessageType.Warning;

        /// <summary>
        /// Repair labels already given a button this pass, so several problems sharing one repair — every
        /// line of contract drift, say — offer it once instead of as a column of identical buttons. Static
        /// and cleared per pass; inspectors draw one at a time on one thread.
        /// </summary>
        private static readonly System.Collections.Generic.HashSet<string> OfferedRepairs = new();
    }
}
