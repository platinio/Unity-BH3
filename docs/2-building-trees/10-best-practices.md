# Best practices

Patterns that keep behavior trees reusable and easy for designers to work with.

---

## Fetching information and acting on it are two separate pieces

The traditional node does both: it knows a blackboard key, fetches the value, then acts. In pseudo-code,
not BH3 API:

```csharp
// The shape most behavior tree libraries use. Not BH3 code.
public class SetNavAgentPosition : SomeNodeBase
{
    public string targetPositionKey;

    public override Status Update()
    {
        Vector3 targetPosition = blackboard.Get<Vector3>(targetPositionKey);   // fetch
        navAgent.SetDestination(targetPosition);                               // act
        return Status.Success;
    }
}
```

This node is hard to reuse. Something else must keep the blackboard updated before it can run, and a
designer cannot change what it reads without a programmer.

Split the two responsibilities. One node that **fetches**:

```csharp
[GraphCreateMenu("Custom/Get Escape Position")]
public class GetEscapePosition : GameplayNode
{
    [DoNotSerialize] public ValueOutput Value { get; private set; }

    public override string NodeName => "Get Escape Position";

    protected override void Definition()
    {
        base.Definition();
        Value = ValueOutput<Vector3>(nameof(Value), () => CalculateEscapePosition());
    }

    private Vector3 CalculateEscapePosition()
    {
        return transform.position - transform.forward * 10f;   // whatever your game does
    }
}
```

And one node that **acts**:

```csharp
[GraphCreateMenu("Custom/Set Nav Agent Position")]
public class SetNavAgentPosition : GameplayNode
{
    [DoNotSerialize] public ValueInput NavPosition { get; private set; }

    public override string NodeName => "Set Nav Agent Position";

    protected override void Definition()
    {
        base.Definition();
        NavPosition = ValueInput<Vector3>(nameof(NavPosition));
    }

    public override ExecutionStatus OnUpdate(BTContext ctx)
    {
        ctx.GetComponent<NavMeshAgent>().SetDestination(ctx.GetValue<Vector3>(NavPosition));
        return ExecutionStatus.Success;
    }
}
```

Now each node does one thing. `SetNavAgentPosition` works with any `Vector3` source, and `GetEscapePosition`
can feed any node that needs a position. Designers mix and match without touching code.

---

## Use reactive guards for interruptions, not Condition nodes

A **Condition** node is only checked when execution *reaches* it. If Idle is already running, a Condition in
front of it is not asked again until the tree loops back around, so the agent keeps idling while an enemy
walks up.

A **Reactive Guard** attached to a node keeps watching while the node runs:

- If it is false when the node is about to enter, the node is skipped.
- If it turns false *while* the node runs, the branch is aborted and every node under it exits, on that tick.
- If it turns true while a lower-priority sibling runs, it takes that sibling's slot, on that tick.

Put `hasTarget` on Chase and `targetInRange` on Attack, and Idle needs no guard at all. Each branch states
only its own precondition, and the Selector above them does the rest.

A plain **Conditional Execution** is asked once, at entry, and never again. That is the right tool for a
random roll, an expensive one-shot query, or a config flag, and the wrong tool for anything that should react
to the world changing.

> Use Condition nodes for decisions made at one point in the flow. Use Reactive Guards when a branch should
> stop being valid the instant the world changes. Use Conditional Executions for a gate that should be
> decided once. See [Guards](06-guards.md).

Guards on the same node are ANDed, so there is no need for an `And` node.

---

## Think of each branch as a function that does one thing

![A behavior tree with five colour-grouped branches labelled Update Blackboard, Move to Safe Position, Face Target, Shooting, and Patrol, each a self-contained cluster of nodes under a shared root](../images/single-responsibility-branches.png)

Each branch does exactly one job and knows nothing about the others. Patrol does not care about enemies;
the guard at its call site decides when to interrupt it.

Once branches are that focused, share them across agents with [sub-trees](07-sub-trees.md). Designers then
build new enemies by picking existing branches, passing arguments, and assigning priorities, which creates
the illusion of unique behaviour from reusable pieces.

---

## Give each branch its own tree asset

Two features working together are where BH3 earns its keep:

- **Run Behavior Tree Graph** lets a tree run another, so a branch can be its own asset.
- **A guard on that node** decides whether the branch may run, and when it must stop.

Together they mean a branch never needs to know what else exists. A Zombie's Idle sub-tree does not check
for enemies; the guard on its call site handles that. Every branch is a separate asset that knows nothing
about its siblings, and the only thing that knows about all of them is the Zombie tree that assembles them.

Prefer one asset per branch. If you keep a branch inline, have a reason.

---

## Facts come from sensors, not from branches

A branch is conditional by construction, so anything only a branch produces is unreliable by definition: it
stops being updated the moment the branch stops running. Facts about the agent, `hasTarget`,
`lastKnownPosition`, `heardSomething`, should come from something that runs unconditionally, which is what a
sensor is.

Publish them through `AgentVariableWriter` so guards wake on them and the debugger can name the writer. See
[Variables and scope](04-variables-and-scope.md#writing-facts-from-a-sensor).

---

## Don't write a node for every small thing

Before adding a C# node, decide which of these it is:

| Situation | Reach for |
|---|---|
| Genuinely reusable across agents | A C# node |
| Specific to one tree or one situation | A **Script Graph Variable** running a Function |
| Specific, but the graph would be large and unreadable | A C# node after all |

A pile of one-off node types is as much of a maintenance burden as a pile of one-off scripts.

---

## Read the badge, then lay the tree out to match it

Priority is the number on the badge, and dragging a child sideways renumbers the badges. Lay branches out
left to right in the order you want them tried, so the picture and the priority agree, and use sticky notes
to say what that order means. If a badge turns amber, the picture is lying; drag the children until it
stops. See [Execution order](05-execution-order.md).

---

## Next

- [Debugging](../3-debugging/01-overview.md) — for when a branch does not run and the reason is not obvious
- [Custom nodes](../4-extending-with-csharp/01-custom-nodes.md)
