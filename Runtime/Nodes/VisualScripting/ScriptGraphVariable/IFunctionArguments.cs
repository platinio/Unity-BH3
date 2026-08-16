namespace ArcaneOnyx.BehaviorTree
{
    /// <summary>
    /// A call site's argument list for a Function, read positionally and by name.
    ///
    /// <para>
    /// This exists so that <see cref="ScriptGraphVariable"/> can own the binding while the node owns the
    /// arguments. The node is the only thing that knows what its ports are; the variable is the only thing
    /// that knows which agent it is bound to and which plan that binding resolved against. Handing ports
    /// down would put Visual Scripting port types into a helper that has no business knowing about them,
    /// and handing the binding up would let a caller stage arguments against a plan that has since been
    /// rebuilt.
    /// </para>
    ///
    /// <para>
    /// Read positionally rather than as a dictionary or a list of pairs deliberately: an implementer is a
    /// node reading its own ports, and every allocation on this path is one per evaluation. The names are
    /// resolved to plan indices once and cached — see <c>ScriptGraphVariable.StageArguments</c> — so
    /// <see cref="NameAt"/> is a resolve-time call, not a per-evaluation one.
    /// </para>
    /// </summary>
    public interface IFunctionArguments
    {
        /// <summary>How many arguments this call site supplies.</summary>
        int Count { get; }

        /// <summary>The declared input this argument is for. Read at resolve time only.</summary>
        string NameAt(int index);

        /// <summary>
        /// The argument's value. Called once per argument per evaluation, so an implementer must not
        /// allocate here — reading a port is fine, building a list is not.
        /// </summary>
        object ValueAt(int index);
    }
}
