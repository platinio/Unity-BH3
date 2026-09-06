using Unity.VisualScripting;
using UnityEngine.SceneManagement;

namespace ArcaneOnyx.BehaviorTree
{
    /// <summary>
    /// Base action node for behavior trees
    /// </summary>
    public class GameplayNode : BehaviorTreeNode
    {
        protected void SaveVariable(string key, BehaviorTreeVariableKind variableKind, object value)
        {
            // Before the recording, not after. A write with no store never happens, so recording one would
            // put a value into the variable watch that nothing ever held -- a phantom row in the one tool
            // whose whole job is to be believed. Every write funnels through here, so this is the guard that
            // covers nodes which have no reason to resolve the kind themselves.
            variableKind = VariableKindField.Resolve(variableKind, NodeName);

            // Recorded before the write, because afterwards the previous value is gone and "what did it change
            // from" is half of what makes a write worth recording. The call and everything inside its
            // arguments — the read included — are removed by the compiler outside the editor and dev builds,
            // so the extra lookup does not exist in a shipped build.
            Debugging.BehaviorTreeRecorder.VariableWrite(
                this, key, variableKind, ReadVariable(key, variableKind), value);

            switch (variableKind)
            {
                // Writes land in this node's own scope and go no further. A branch cannot reach its caller's
                // variables, so scratch state cannot leak sideways into a sibling which is what makes the
                // same branch safe to reuse across unrelated agents. State that genuinely belongs to the whole
                // agent has a home already: BehaviorTreeVariableKind.Object, on the agent's Variables
                // component.
                case BehaviorTreeVariableKind.Graph:
                    VariableScope?.Set(key, value);
                    break;
                case BehaviorTreeVariableKind.Object:
                    // Set and version bump together, so the bump cannot be forgotten here or by the next
                    // writer added -- see AgentVariableWriter.SetAgentVariable. Get-or-add is right at a
                    // write site: a fact was just published, so guards need something to read versions from.
                    AgentVariableWriter.On(gameObject).SetAgentVariable(key, value);
                    break;
                case BehaviorTreeVariableKind.Scene:
                    SceneVariables.Instance(SceneManager.GetActiveScene()).variables.declarations.Set(key, value);
                    break;
                case BehaviorTreeVariableKind.Application:
                    ApplicationVariables.current.Set(key, value);
                    break;
                case BehaviorTreeVariableKind.Saved:
                    SavedVariables.current.Set(key, value);
                    break;
            }
        }

        /// <summary>
        /// Undefines a variable, routed and recorded exactly like a write — because that is what it is.
        /// </summary>
        /// <remarks>
        /// A removal is a change to null, and every consumer of the recording treats it as one: the watch
        /// wants a history entry saying the value went away, a variable breakpoint should fire, and a
        /// reactive guard has to notice. Agent scope therefore goes through
        /// <see cref="AgentVariableWriter"/> like every other agent write, so the version bump guards
        /// compare against rides along; the alternative is the silent failure that type's summary describes.
        /// <para>
        /// A key that is already gone is the asked-for state, so it is not an error and is not recorded —
        /// nothing changed. Having nowhere to remove <em>from</em> is different, and the caller is told.
        /// </para>
        /// <para>
        /// Removes from one store and no further, which for <see cref="BehaviorTreeVariableKind.Graph"/>
        /// means the node's own scope — the same rule <see cref="SaveVariable"/> follows, and deliberately
        /// not the outward walk a read does. A branch cannot delete a name out from under its caller.
        /// </para>
        /// </remarks>
        /// <returns>Whether there was a store to remove from.</returns>
        protected bool EraseVariable(string key, BehaviorTreeVariableKind variableKind)
        {
            var declarations = DeclarationsFor(variableKind);
            if (declarations == null) return false;

            if (!declarations.IsDefined(key)) return true;

            // Recorded before the removal, while the value still exists, for the reason SaveVariable gives:
            // afterwards there is nothing to say what it was. Compiles out with the rest of the facade.
            //
            // Read from the store being mutated rather than through ReadVariable. The two agree today --
            // the early return above means this store holds the key, and a scope read checks its own
            // declarations before walking outward -- but that is two facts holding hands, and only one of
            // them is in this method. Reading the object being changed needs neither.
            Debugging.BehaviorTreeRecorder.VariableWrite(
                this, key, variableKind, Read(declarations, key), null);

            if (variableKind == BehaviorTreeVariableKind.Object)
            {
                AgentVariableWriter.On(gameObject).RemoveAgentVariable(key);
            }
            else
            {
                declarations.Undefine(key);
            }

            return true;
        }

        /// <summary>
        /// The one store a write or a removal of <paramref name="variableKind"/> acts on, or null when there
        /// is none to reach yet.
        /// </summary>
        /// <remarks>
        /// <see cref="BehaviorTreeVariableKind.Graph"/> resolves to the node's own scope and no further,
        /// which is the same rule <see cref="SaveVariable"/> applies and deliberately not the rule a
        /// <em>read</em> follows — a read walks the calling chain outward. Anything that must see a caller's
        /// value has to go through <see cref="VariableScope"/> rather than through here.
        /// </remarks>
        protected VariableDeclarations DeclarationsFor(BehaviorTreeVariableKind variableKind)
        {
            switch (variableKind)
            {
                case BehaviorTreeVariableKind.Graph:
                    return VariableScope?.Local;
                case BehaviorTreeVariableKind.Object:
                    return BehaviorTreeMachine != null ? BehaviorTreeMachine.Variables?.declarations : null;
                case BehaviorTreeVariableKind.Scene:
                    // Not `?.` -- that skips Unity's destroyed check on a MonoBehaviour. Instance
                    // get-or-creates, so this cannot be null today; the shape should not have to be
                    // re-derived to stay safe if that ever changes.
                    var scene = SceneVariables.Instance(SceneManager.GetActiveScene());
                    return scene != null ? scene.variables?.declarations : null;
                case BehaviorTreeVariableKind.Application:
                    return ApplicationVariables.current;
                case BehaviorTreeVariableKind.Saved:
                    return SavedVariables.current;
                default:
                    return null;
            }
        }

        /// <summary>
        /// The value a write is about to replace, or null when there isn't one yet.
        /// <para>
        /// Never throws. Every store here throws on an undefined name, and a debugging read has no business
        /// turning a first write into an exception — so an undeclared variable reads as null, which is what a
        /// recording should say about a value that did not exist.
        /// </para>
        /// </summary>
        private object ReadVariable(string key, BehaviorTreeVariableKind variableKind)
        {
            if (string.IsNullOrEmpty(key)) return null;

            // Graph is the one kind that is not a single store: a read sees the branch's own values first
            // and its caller's underneath, which is exactly what DeclarationsFor does not do.
            if (variableKind == BehaviorTreeVariableKind.Graph)
            {
                return VariableScope != null && VariableScope.TryGet(key, out var scoped) ? scoped : null;
            }

            return Read(DeclarationsFor(variableKind), key);
        }

        private static object Read(VariableDeclarations declarations, string key)
        {
            return declarations != null && declarations.IsDefined(key) ? declarations.Get(key) : null;
        }
    }
}
