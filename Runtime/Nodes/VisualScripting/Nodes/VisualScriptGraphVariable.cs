using System;
using System.Collections.Generic;
using ArcaneOnyx.GraphCore;
using Unity.VisualScripting;

namespace ArcaneOnyx.BehaviorTree
{
    [GraphCreateMenu("Unity/Visual Scripting/Script Graph Variable")]
    public class VisualScriptGraphVariable : BaseVisualScriptingNode, IDeclaresWatchedKeys
    {
        [Serialize] [Inspectable] private BTScriptGraphVariable ScriptGraphVariable = null;
        public override string NodeName => comment == string.Empty? "Script Graph Variable" : comment;
        public override bool CanBeUsedAsTransitionDestination => false;
        public override string Description => "Returns value from Script Graph";

        [DoNotSerialize]
        public ValueOutput Output { get; private set; }

        /// <summary>The embedded graph this node reads, or null when it reads a Function.</summary>
        [DoNotSerialize]
        public ScriptGraphAsset EmbeddedScriptGraph => ScriptGraphVariable?.ScriptGraphAsset;

        /// <summary>The Function this node reads, or null when it reads an embedded graph.</summary>
        [DoNotSerialize]
        public VisualScriptingExtension.FunctionGraphAsset Function => ScriptGraphVariable?.Function;

        /// <summary>
        /// True when both a Function and an embedded graph are assigned. The Function wins at runtime, so
        /// this is not a crash — it is the worse kind of problem, where the graph an author is editing is not
        /// the one being evaluated. Verification reports it.
        /// </summary>
        [DoNotSerialize]
        public bool HasAmbiguousGraphSource =>
            ScriptGraphVariable != null &&
            ScriptGraphVariable.Function != null &&
            ScriptGraphVariable.ScriptGraphAsset != null;

        /// <summary>
        /// The keys the referenced Function declares, so a guard fed by this node inherits its schedule.
        ///
        /// <para>
        /// Read live from the asset rather than copied onto this node, deliberately. A copy would need its own
        /// refresh verb and its own staleness report — a second drift story on top of the one the caller-side
        /// contract copy genuinely requires — and locked decision 5 of spec 10 says there is only ever one.
        /// The copy exists there because <c>Definition()</c> drops connections to ports that do not resolve
        /// during deserialization; nothing about a key list has that problem, so nothing here needs the cure.
        /// </para>
        ///
        /// <para>
        /// An <b>embedded</b> graph declares nothing and yields nothing: it has no asset-level metadata to
        /// declare with. That is the honest answer rather than a walk of its units, which would report derived
        /// keys where every other implementer reports declared ones and quietly make the two mean the same
        /// thing.
        /// </para>
        /// </summary>
        [DoNotSerialize]
        public IReadOnlyList<string> DeclaredWatchedKeys =>
            Function != null ? Function.WatchedKeys : Array.Empty<string>();

        public override bool DrawInSubTree => false;

        /// <summary>
        /// Points this node at the graph that produces its value. Only useful after the node has been added
        /// to a graph, since <see cref="Definition"/> is what creates the variable this assigns into.
        /// Needed to author a node's Visual Scripting from code.
        /// </summary>
        public void SetScriptGraph(ScriptGraphAsset asset)
        {
            ScriptGraphVariable?.SetScriptGraphAsset(asset);
        }

        /// <summary>
        /// Points this node at a Function — a named, shared, contracted graph — instead of an anonymous
        /// sub-asset. The counterpart of <see cref="SetScriptGraph"/> for the new seam.
        /// </summary>
        public void SetFunction(VisualScriptingExtension.FunctionGraphAsset asset)
        {
            ScriptGraphVariable?.SetFunction(asset);
        }
        
        protected override void Definition()
        {
            base.Definition();

            if (ScriptGraphVariable == null) ScriptGraphVariable = CreateGraphWithOutput(typeof(object));
            Output = ValueOutput<object>(nameof(Output), () =>
            {
                // Announced for the duration of the call so a Set BT Variable unit inside the graph can be
                // attributed to this node rather than to a bare name. Compiles out with the recorder.
                Debugging.BehaviorTreeRecorder.PushScriptGraphOwner(this);

                try
                {
                    runtimeException = null;
                    return ScriptGraphVariable.GetValue<object>(gameObject, ScriptGraphVariables);
                }
                catch (Exception e)
                {
                    LastExecutionStatus = ExecutionStatus.Exception;
                    runtimeException = e;
                    throw;
                }
                finally
                {
                    // A graph that throws must still leave the stack balanced, or every later write in this
                    // agent is attributed to a node that finished long ago.
                    Debugging.BehaviorTreeRecorder.PopScriptGraphOwner();
                }
            });
        }

        public override ExecutionStatus OnUpdate()
        {
            if (runtimeException != null) return ExecutionStatus.Exception;
            return base.OnUpdate();
        }
    }
}