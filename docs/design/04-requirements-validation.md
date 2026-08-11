# Requirements validation — "this behavior can't run on that prefab, here's the fix"

**Status:** design spec for implementation by an AI or engineer with access to the BH3 source.
Written from BH3's design documentation — verify exact type/member names against the code before building.

## Goal

When a designer assigns a behavior to an agent that can't support it, tell them **at edit time, by name,
with a fix button** — not via `MissingValuePortInputException` on the first tick of a playtest. This is
the mechanism that makes "compose an AI from a library" feel safe.

## BH3 context you need

- Branches are `BehaviorTreeGraphAsset`s; agents run a root tree via `BehaviorTreeMachine`.
- Ports read through `GetComponent<T>(ValueInput)` fall back to the machine's own GameObject — those
  reads imply a **component requirement** on the agent.
- Animation nodes take an `Animator` port (no default) and trigger/state names usually fed by string
  literals — extractable from `BehaviorTreeDump.ToJson` output.
- `bt_verify` exists and reloads-then-checks trees; this spec extends it with an `--against <prefab>`
  mode.
- Spec 01 (Facts & Services) defines `[ProvidesFact]` providers; the **AgentFact** requirement kind below
  is its enforcement surface.

## Data model

```
Requirement {
  kind:   Component | AnimatorParameter | AnimatorState | AgentFact | TagOrLayer | Subsystem | Custom
  key:    e.g. "UnityEngine.AI.NavMeshAgent", "Attack" (trigger), "lastKnownTargetPos"
  type:   payload type where relevant (trigger vs bool param; fact type)
  source: Derived | Declared | RolledUp(fromSubTreeAssetId)
  note:   optional human explanation shown in UI
}
```

A branch asset carries a **requirements manifest**: a list of `Requirement`.

## How manifests are populated (three sources, merged)

1. **Derived (automatic, regenerated on save — cannot go stale):** walk the branch's dump:
   - `GetComponent<T>`-style port reads with nothing connected → Component requirement for T on the agent.
   - Animation nodes → Component: Animator, plus AnimatorParameter requirements from literal-fed
     trigger/state name ports (dynamic names are skipped and reported as "unverifiable — declare
     manually").
   - Nodes touching NavMesh (`SetNavAgentPosition`, `WaitUntilReachNavTargetPosition`, …) →
     Component: NavMeshAgent.
   - Known subsystem nodes (e.g. `RequestAttackToken`, TPS query nodes) → Subsystem requirements
     (attack-token manager present, TPS positions source in scene). Maintain the node→subsystem map as an
     attribute on the node class (`[RequiresSubsystem(...)]`) so it lives with the code, not in a table.
2. **Declared (hand-added in the asset inspector):** for what analysis can't see — requirements implied
   inside C# node bodies or visual-scripting graphs. Small list UI on the branch asset.
3. **Rolled up:** a branch's manifest automatically unions the manifests of every sub-tree it calls
   (recursively). Callers never re-declare what a dependency already declares. Cycle-safe (BH3 already
   rejects recursive trees at load).

## Where validation runs (four surfaces, one engine)

Single evaluation function: `Validate(manifest, targetGameObjectOrPrefab) → List<Result{req, pass|fail,
fixAvailable}>`. Checks per kind:

- Component: `GetComponent` (prefab-aware).
- AnimatorParameter/State: inspect the target's `AnimatorController` asset for the named
  parameter/state with the right type.
- AgentFact: target has a component (or the tree a service) whose `[ProvidesFact]` covers the key+type
  (spec 01).
- TagOrLayer: project tag/layer tables.
- Subsystem: scene/singleton presence check, per the subsystem's registered probe.
- Custom: user predicate class implementing `IRequirementProbe`.

Surfaces:
1. **Machine inspector:** assigning a tree to a `BehaviorTreeMachine` renders the checklist —
   green check / red X per requirement, with a Fix button where safe.
2. **Library panel (spec 03):** requirement icons on cards; drag-onto-prefab runs validation immediately.
3. **CLI/CI:** `bt_verify --against <prefabPath>` — same results as JSON; CI can gate "every enemy prefab
   satisfies its tree."
4. **Runtime assert (dev builds):** `BehaviorTreeMachine.Awake` runs the same validation and fails loudly
   naming the requirement — spawn-time, named, instead of first-tick, cryptic.

## Fix buttons

Per-kind auto-fixes, only where unambiguous and reversible:
- Component → AddComponent (with undo).
- AnimatorParameter → add parameter to the controller (with undo).
- AgentFact → offer the list of known provider components for that fact ("Add TargetingSensor").
- Subsystem / AnimatorState / Custom → no auto-fix; button navigates to the thing to edit instead.
Never silently fix on drag — always show the checklist and let the designer click.

## Acceptance criteria

1. A branch using nav movement + an "Attack" trigger, assigned to an empty prefab, reports exactly:
   NavMeshAgent missing (Fix), Animator missing (Fix), trigger "Attack" missing (Fix after Animator),
   each resolvable from the inspector without opening the tree.
2. Derived requirements regenerate on save; hand-edits to derived rows are impossible.
3. A sub-tree's requirements appear in its caller's manifest as RolledUp with the source asset shown.
4. `bt_verify --against` returns nonzero on failures (CI-usable) and its JSON names requirement, kind,
   and consuming node guid.
5. Runtime assert catches a prefab modified after validation (component removed later) and names the
   requirement at spawn.
