# The guard seam, and per-agent state in things that are not nodes

**Status:** plan, not yet implemented. Written 2026-08-23, after spec 07 steps 1–2 landed
([Unity-BH3#75](https://github.com/platinio/Unity-BH3/pull/75)). Verify member names against the code
before building — this was written from a read of the guard path, not from running it.

## Why this is the piece with a deadline

Spec 07's seam froze the node lifecycle: `OnEnter(BTContext)` and friends, with `ctx.Memory<T>()` for
per-agent state, enforced by `NodeContextConventionTests`. After it, **the instancing flip is no longer
time-sensitive** — it changes no signature a node author sees, so it can land whenever the numbers justify
it.

Three surfaces were left outside that seam, and they are still time-sensitive for the same reason the
lifecycle hooks were: they are public API, users write against them, and code written against them today is
debt we cannot pay off on a user's behalf.

1. **`Condition.Evaluate()`** and **`ConditionalExecution.Evaluate()`** take no context.
2. **`GuardTrigger`** holds per-agent state and is not a node.
3. **`ScriptGraphVariable`** (the Function slot) holds per-agent state and is not a node.

(2) and (3) are the same problem wearing different hats, and neither is visible to any rule spec 07 added —
`NodeContextConventionTests` only scans types assignable to `BehaviorTreeNode`.

## What actually breaks when the tree is shared

Not all of this state is equal, and the split matters because one category must **not** move.

### Genuinely per-agent — correctness bugs under sharing

| Field | Where | What goes wrong |
|---|---|---|
| `hasCachedResult`, `cachedResult`, `lastEvaluatedAt` | `ReactiveGuard.cs:181-183` | Agent A's cached answer is returned to agent B. A guard that is false for B enters B's branch because A evaluated true. |
| `Evaluations` | `ReactiveGuard.cs:187` | The cost display sums every agent onto one counter. |
| `seenVersions` (`Dictionary<string,int>`) | `GuardTrigger.cs:122` | **The sharpest one.** Versions are recorded per key with no agent in the key, so agent A's write marks the fact "seen" for every agent. Guards stop waking on changes that did happen, and wake on changes that happened to someone else. Silent, and it presents as a branch that intermittently stops firing. |
| `currentInterval` | `GuardTrigger.cs:123` | Defeats the feature's own stated purpose. The comment at `:115-118` says the interval is randomised because *"200 agents sharing a 0.2s timer land on the same frame and produce a spike rather than a load"* — share the trigger and all 200 are back in phase. |

### Agent-keyed caches — not wrong, but they thrash

| Field | Where | What goes wrong |
|---|---|---|
| `cachedAgent`, `cachedWriter` | `GuardTrigger.cs:128-129` | Cached *against* the agent, so correctness holds — but with N agents interleaving, every call misses and re-resolves. The comment at `:125-127` says it is cached because *"this trigger is asked twice per guard evaluation on the path the whole feature exists to keep cheap."* |
| `binding`, `boundAgent`, `mappedPlan`, `argumentIndices` | `ScriptGraphVariable.cs:22-35` | Same shape, worse cost: `BindingFor(agent)` rebuilds the whole `FunctionBinding` on every agent switch. The comment at `:82-85` states the assumption outright — *"One binding per agent, rebuilt only when the agent changes. **The node instance this lives on is already per-agent**, so the binding's lifetime is the node's and nothing has to reap it."* That sentence is the flip's precondition, written down as a guarantee. |

### Graph-derived — must STAY shared

| Field | Where | Why it must not move |
|---|---|---|
| `inheritedResolved` | `ReactiveGuard.cs:137` | |
| `inherited` (keys) | `GuardTrigger.cs:101` | |

These are derived from the graph, not the agent, so they are identical for every agent running the asset.
`EnsureInheritedKeys` exists precisely to keep an allocating walk off the hot path (`ReactiveGuard.cs:139-149`).
Moving them into per-agent memory would run that walk once **per agent** instead of once — turning an
optimisation into a regression. Under sharing they resolve once globally, which is strictly better than
today. **Leave them where they are.**

## Part A — the evaluate seam (the part with the deadline)

Mirror spec 07 exactly: a context-taking overload whose default forwards, the guard builds its own context,
no caller changes.

```csharp
// Condition
public virtual bool Evaluate(BTContext ctx) => Evaluate();
public virtual bool Evaluate() => false;                       // legacy, allowlisted

public sealed override ExecutionStatus OnUpdate(BTContext ctx) =>
    Evaluate(ctx) ? ExecutionStatus.Success : ExecutionStatus.Failure;
```

```csharp
// ConditionalExecution
public bool EvaluateInternal(bool fresh)
{
    bool result = Ask(Context, fresh);        // <- the one dispatch point
    LastExecutionStatus = result ? ExecutionStatus.Success : ExecutionStatus.Failure;
    return result;
}

public virtual bool Ask(BTContext ctx, bool fresh) => Evaluate(ctx);
public virtual bool Evaluate(BTContext ctx) => Evaluate();
public abstract bool Evaluate();              // legacy, allowlisted
```

`BehaviorTreeNode.FirstFailingGuard` also calls `Ask(fresh)` directly for the preemption poll
(`BehaviorTreeNode.cs:589-591`); that call site becomes `Ask(guard.Context, fresh)` — or better, keep a
parameterless `Ask(bool)` shim on the guard that supplies its own `Context`, so the owner never has to know.

### The trap: the context must be threaded through `Ask`, not only `Evaluate`

The obvious move is to attach the context at `Evaluate` and leave `Ask` alone, because `Ask` is "just the
cache". That is wrong, and it is the reason this piece is not a copy-paste of spec 07.

`ReactiveGuard.Ask` (`:193-216`) *is* where the per-agent state lives — `hasCachedResult`, `cachedResult`,
`lastEvaluatedAt`, `Evaluations`. After the flip that state has to come from memory, so `Ask` needs the
context to find it. Attach the context below the cache only, and `Ask` still reads its cache off the shared
node.

Building the context above the cache costs nothing: `BTContext` is a `readonly struct` over the node, so
constructing one on a cache hit is free.

### Triggers must take the agent from the context, not from `Owner`

`ReactiveGuard` currently passes its owner down — `triggers[i].IsDue(Owner, since)` and
`triggers[i].OnEvaluated(Owner)` — and `GuardTrigger` resolves the agent from `owner.gameObject`
(`WriterFor`, `:311`). On a shared node `owner.gameObject` means nothing. Both signatures take the context
instead.

## Part B — per-agent state in things that are not nodes

`ctx.Memory<T>()` is keyed per node and deliberately allows **one type per node**, so a sub-object cannot
call it: two triggers on one guard would collide, and a node with three Function slots would collide three
ways.

**The sub-object becomes pure definition; its state moves into the owning node's memory, index-aligned.**
This is the ECS-like split — the sub-object is the component *definition*, the array entry is the
component *data*:

```csharp
private sealed class ReactiveGuardMemory
{
    public bool  HasCachedResult;
    public bool  CachedResult;
    public float LastEvaluatedAt;
    public int   Evaluations;
    public GuardTriggerState[] Triggers;   // index-aligned with the serialized triggers list
}
```

```csharp
// GuardTrigger becomes stateless; state arrives by ref
public bool IsDue(BTContext ctx, ref GuardTriggerState state, float since)
public void OnEvaluated(BTContext ctx, ref GuardTriggerState state)
```

Index alignment is safe because the `triggers` list is serialized structure — it cannot change at runtime in
a player build, which is the same invariant `FunctionBindingPlan` and `EnsureInheritedKeys` already rely on.

`ScriptGraphVariable` takes the identical treatment: `binding`, `boundAgent`, `mappedPlan` and
`argumentIndices` move into a `FunctionSlotState`, one per slot, held in the owning node's memory. Its public
entry points grow a state parameter:

```csharp
public T GetValue<T>(BTContext ctx, ref FunctionSlotState state, IFunctionArguments arguments)
```

Note this also *removes* a parameter: `GetValue<T>(GameObject gameObject, …)` no longer needs the agent
passed separately, because the context carries it.

## Migration path

1. **Part A first, on its own.** Context overloads on `Condition` and `ConditionalExecution` with forwarding
   defaults, `Ask` threaded, triggers taking the context. Behaviour unchanged; nothing moves off a field yet.
2. **Third allowlist** in `NodeContextConventionTests` — guards still overriding the parameterless
   `Evaluate()`. Same shrink-only rule, same stale-entry guard.
3. **The agent-access rule** (see "Open questions" — it belongs with this work). A convention test banning
   bare `gameObject` / `transform` / `Machine` / `Variables` inside lifecycle and evaluate bodies. Without
   it the seam stays leaky no matter how many overloads exist, because a ctx-style body can still reach
   through `this`.
4. **Part B**, per sub-object type, one at a time: `GuardTrigger` first (it has a correctness bug under
   sharing, not just a cost one), then `ScriptGraphVariable`.
5. Only then is the guard path ready for spec 07 step 4.

Steps 1–3 are the ones with the deadline and are the candidates for the v2 release. Step 4 can wait for the
same measurement that gates the flip.

## Acceptance criteria

1. A guard written entirely against `Evaluate(BTContext)` with its state in the owner's memory behaves
   identically to today's, including under the `fresh` asymmetry at entry.
2. Existing guards overriding `Evaluate()` keep working untouched, and are on a shrinking allowlist.
3. `ReactiveGuard`'s cache economy is unchanged — the existing tests that count `Evaluations` and assert a
   guard with triggers evaluates less often than every tick still pass, and still fail if the cache is
   bypassed.
4. `inherited` / `inheritedResolved` still resolve **once per asset**, not once per agent. A test that
   counts the `InheritedWatchedKeys.Resolve` walk would pin this; there is none today.
5. Two agents running one guard asset each see their own `seenVersions`, their own interval phase, and their
   own cached answer.

## Open questions

- **Does `ctx.Memory<T>()` need a keyed variant after all?** Part B threads state by `ref` from the owning
  node's memory, which keeps the one-type-per-node rule intact but means every node owning sub-objects
  hand-writes an array and keeps it index-aligned. The alternative is `ctx.Memory<T>(slotIndex)`. Threading
  is simpler and allocation-free; the keyed variant is less error-prone for authors. Worth deciding before
  `ScriptGraphVariable` rather than after.
- **Is `Condition.Evaluate()` worth seaming at all**, given `Condition` is an ordinary node whose
  `OnUpdate(BTContext)` is already seamed and could simply pass `ctx` down? Doing it costs almost nothing
  and keeps the two condition families symmetrical for authors, which is the argument for.
- **`GuardTrigger.Seconds`/`Deviation` are `[Serialize]` settings, but `currentInterval` derived from them
  is per-agent.** A reminder that "serialized means shared" is a rule about the *field*, not about the
  concept — and an instance of the `[Serialize]`-evades-the-check gap recorded in spec 07.
