using Unity.VisualScripting;

namespace ArcaneOnyx.BehaviorTree
{
    /// <summary>
    /// The variables a node can see, and the chain it searches to find them.
    /// </summary>
    public class BehaviorTreeVariableScope
    {
        private readonly VariableDeclarations declarations;
        private readonly BehaviorTreeVariableScope parent;

        public BehaviorTreeVariableScope(VariableDeclarations declarations, BehaviorTreeVariableScope parent = null)
        {
            this.declarations = declarations;
            this.parent = parent;
        }

        /// <summary>The declarations owned by this scope alone, with nothing inherited.</summary>
        public VariableDeclarations Local => declarations;

        /// <summary>The outermost scope — the root tree's, where the agent's variables were injected.</summary>
        public BehaviorTreeVariableScope Root => parent == null ? this : parent.Root;

        /// <summary>
        /// Looks the name up here first, then outward. Returns false rather than throwing when nothing in the
        /// chain declares it, so a caller can fall back or report instead of dying mid-tick.
        /// </summary>
        public bool TryGet(string name, out object value)
        {
            for (var scope = this; scope != null; scope = scope.parent)
            {
                if (scope.declarations == null || !scope.declarations.IsDefined(name)) continue;

                value = scope.declarations.Get(name);
                return true;
            }

            value = null;
            return false;
        }

        /// <summary>
        /// Reads through the chain, throwing the way <c>VariableDeclarations.Get</c> does when the name is
        /// nowhere to be found — the message names the scope so the failure points at a tree rather than at
        /// a dictionary.
        /// </summary>
        public object Get(string name)
        {
            if (TryGet(name, out var value)) return value;

            throw new System.InvalidOperationException(
                $"Variable '{name}' is not defined in this tree, in any tree that runs it, or on the agent. " +
                "Declare it on the agent's Variables component, give the tree an optional default, or pass it in.");
        }

        public bool IsDefined(string name) => TryGet(name, out _);

        /// <summary>
        /// Writes to this scope only. A sub-tree cannot reach its caller's variables, so a branch reused by
        /// two agents cannot have its scratch state read by whatever else happens to share the name.
        /// </summary>
        public void Set(string name, object value)
        {
            declarations?.Set(name, value);
        }

        /// <summary>
        /// Writes only where the name is already declared, and to the root when it is declared nowhere.
        /// Kept for the paths that still need the old agent-wide behaviour.
        /// </summary>
        public void SetWhereDeclared(string name, object value)
        {
            for (var scope = this; scope != null; scope = scope.parent)
            {
                if (scope.declarations == null || !scope.declarations.IsDefined(name)) continue;

                scope.declarations.Set(name, value);
                return;
            }

            Root.Set(name, value);
        }

        /// <summary>
        /// The chain collapsed into one set of declarations, inner winning over outer.
        /// <para>
        /// For the Visual Scripting entry points, which take a flat <c>VariableDeclarations</c> and use it
        /// strictly as input — <c>ScriptGraphVariableExtension.UpdateInput</c> copies matching names onto the
        /// graph's input ports and never writes back — so nothing is lost by flattening. A root scope has
        /// nothing to merge and hands back its own declarations, which is both free and exactly the object
        /// these paths were given before.
        /// </para>
        /// </summary>
        public VariableDeclarations Flatten()
        {
            if (parent == null || declarations == null) return declarations;

            var flattened = new VariableDeclarations { Kind = declarations.Kind };

            // Outermost first so the innermost scope overwrites it — same precedence a read walks.
            Flatten(flattened);

            return flattened;
        }

        private void Flatten(VariableDeclarations into)
        {
            parent?.Flatten(into);

            if (declarations == null) return;

            foreach (var declaration in declarations)
            {
                into.Set(declaration.name, declaration.value);
            }
        }

        /// <summary>Seeds anything this scope does not already declare. Used for a tree's optional defaults.</summary>
        public void SeedDefaults(VariableDeclarations defaults)
        {
            if (defaults == null || declarations == null) return;

            foreach (var declaration in defaults)
            {
                if (declarations.IsDefined(declaration.name)) continue;

                declarations.Set(declaration.name, declaration.value);
            }
        }
    }
}
