using System;
using System.Collections.Generic;
using Unity.VisualScripting;

namespace ArcaneOnyx.BehaviorTree
{
    public class BaseVisualScriptingNode : GameplayNode
    {
        [Serialize] private List<BTScriptGraphVariable> scriptGraphVariables = new List<BTScriptGraphVariable>();
        [Serialize] [Inspectable] protected string comment = "";

        /// <summary>
        /// Every graph slot this node holds.
        ///
        /// <para>
        /// A slot holds a <b>Function</b> — a named project asset. It used to be able to hold an embedded
        /// graph instead: an anonymous sub-asset of the owning tree, which is why this class once also
        /// projected the list down to <c>scriptGraphAssets</c> for the machinery that had to find and delete
        /// them. Nothing embeds any more, so a slot is just a reference and there is nothing to collect.
        /// </para>
        /// </summary>
        public IReadOnlyList<BTScriptGraphVariable> GraphSlots => scriptGraphVariables;

        /// <summary>
        /// The Functions this node reads, skipping empty slots.
        ///
        /// <para>
        /// The replacement for <c>scriptGraphAssets</c>, which every debugging surface used to ask for the
        /// graphs a node owned — the dump, the why panel, the guard trace. Those graphs are Functions now,
        /// so the surfaces ask here. Had they been left on the old seam they would have answered empty
        /// forever and simply shown nothing, which is the failure mode a debugger can least afford.
        /// </para>
        /// </summary>
        public IEnumerable<VisualScriptingExtension.FunctionGraphAsset> Functions
        {
            get
            {
                foreach (var slot in scriptGraphVariables)
                {
                    var function = slot?.Function;
                    if (function != null) yield return function;
                }
            }
        }

        // Copy, cut and duplicate were all refused here, because two copies of a node sharing one embedded
        // sub-asset meant deleting either copy destroyed the graph the other was still using. A Function is
        // a project asset that both copies simply reference, which is what sharing is -- so the restriction
        // went with the thing it was protecting.

        protected BTScriptGraphVariable CreateRunnableScriptGraphVariable()
        {
            var scriptGraphVariable = new BTScriptGraphVariable();
            scriptGraphVariables.Add(scriptGraphVariable);
            return scriptGraphVariable;
        }

        protected BTScriptGraphVariable CreateGraphWithOutput(Type returnType)
        {
            var scriptGraphVariable = new BTScriptGraphVariable(returnType);
            scriptGraphVariables.Add(scriptGraphVariable);
            return scriptGraphVariable;
        }
    }
}
