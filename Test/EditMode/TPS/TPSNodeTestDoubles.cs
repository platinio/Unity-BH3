using System.Collections.Generic;
using ArcaneOnyx.Scoring;
using ArcaneOnyx.UnityTacticalPositionSelection;
using ArcaneOnyx.VisualScriptingExtension;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;

namespace ArcaneOnyx.BehaviorTree.Tests
{
    internal static class TPSNodeTestDoubles
    {
        public const string FlankTargetQueryGuid = "65e32a658e111cf468590c3743d64cbc";

        /// <summary>The TPS GeneratorGallery demo's FlankTargetQuery, borrowed as these tests' graph.</summary>
        public static TacticalPositionSelectionQueryItem CreateFlankItem()
        {
            var function = AssetDatabase.LoadAssetAtPath<FunctionGraphAsset>(AssetDatabase.GUIDToAssetPath(FlankTargetQueryGuid));
            NUnit.Framework.Assert.IsNotNull(function,
                "The TPS GeneratorGallery demo's FlankTargetQuery, borrowed as this fixture's query, is missing.");

            var item = ScriptableObject.CreateInstance<TacticalPositionSelectionQueryItem>();
            item.name = "TPS node test query";
            item.Query.SetFunction(function);
            return item;
        }

        /// <summary>A C# source with fixed spots, scored by closeness to the target, counting the queries it built.</summary>
        public sealed class CountingCodeSource : IPositionQuerySource
        {
            private readonly Vector3[] spots;

            public CountingCodeSource(params Vector3[] spots)
            {
                this.spots = spots;
            }

            public int Version => 0;

            public int Builds { get; private set; }

            public IPositionQueryInstance Build(ITacticalAgent agent, IRowOpinions opinions)
            {
                Builds++;
                return CodePositionQuerySource.From(_ => new TacticalPositionSelectionQuery(
                        new FixedSpots(spots),
                        new List<PositionEvaluator> { new ClosenessToTarget() },
                        acceptableScore: 1f, includeQuerierPosition: false))
                    .Build(agent, opinions);
            }
        }

        /// <summary>Adds a node of type <typeparamref name="T"/>; ports only exist once it is in a graph.</summary>
        public static T AddNode<T>(BehaviorTreeGraph graph) where T : BehaviorTreeNode, new()
        {
            var node = new T { Position = new Rect(0f, 0f, 150f, 100f) };
            graph.Nodes.Add(node);
            return node;
        }

        /// <summary>
        /// Adds a node saved the way BH3 1.0.10 to 1.0.14 saved one: <paramref name="preset"/> as the Query port's
        /// inline value.
        /// </summary>
        public static T AddNodeWithInlinePreset<T>(BehaviorTreeGraph graph, TacticalPositionSelectionQueryItem preset)
            where T : BehaviorTreeNode, new()
        {
            var node = new T { Position = new Rect(0f, 0f, 150f, 100f) };
            node.defaultValues["Query"] = preset;
            graph.Nodes.Add(node);
            return node;
        }

        public sealed class StandingAgent : ITacticalAgent
        {
            public Vector3 Position => Vector3.zero;

            public Vector3 Forward => Vector3.forward;
        }

        public sealed class ValueSource<T> : BehaviorTreeNode
        {
            public T Published { get; set; }

            [DoNotSerialize]
            public ValueOutput Value { get; private set; }

            public override string NodeName => $"Test {typeof(T).Name} Source";

            protected override void Definition()
            {
                base.Definition();

                Value = ValueOutput<T>(nameof(Value), () => Published);
            }
        }

        public sealed class ValueReader<T> : BehaviorTreeNode
        {
            [DoNotSerialize]
            public ValueInput Input { get; private set; }

            public override string NodeName => $"Test {typeof(T).Name} Reader";

            public T Read() => Input.GetValue<T>();

            protected override void Definition()
            {
                base.Definition();

                Input = ValueInput<T>(nameof(Input));
            }
        }

        private sealed class FixedSpots : PositionsGenerator
        {
            private readonly Vector3[] spots;

            public FixedSpots(Vector3[] spots)
            {
                this.spots = spots;
            }

            public override void GenerateInto(ITacticalAgent querier, Vector3 center, List<Vector3> appendTo)
                => appendTo.AddRange(spots);
        }

        private sealed class ClosenessToTarget : PositionEvaluator
        {
            public override float Evaluate(ITacticalAgent evaluator, Vector3 targetPosition, Vector3 evaluationPosition)
                => Mathf.Clamp01(1f - Vector3.Distance(targetPosition, evaluationPosition) / 20f);
        }
    }
}
