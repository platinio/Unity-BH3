# Demos and tests

Where to find a working example of each feature. The feature demos live in the reference project, one scene
per feature; this repository holds only the tests, with the trees they verify as fixtures.

---

## In the reference project: `BH3Demos/`

The reference project [platinio/bh3-development](https://github.com/platinio/bh3-development) has one small
scene per feature under `Assets/ArcaneOnyx/BH3Demos/`. Each is built so that the thing it demonstrates is
visible within a minute of pressing Play, and most have a `README.md` that says what to look at.

| Demo | Shows | Documented in |
|---|---|---|
| [ExecutionOrder](https://github.com/platinio/bh3-development/tree/main/Assets/ArcaneOnyx/BH3Demos/ExecutionOrder) | A tree whose canvas layout deliberately disagrees with its stored priority, so the badges read amber. Drag a child and watch them renumber with no change in behaviour | [Execution order](../2-building-trees/05-execution-order.md) |
| [ReactiveGuards](https://github.com/platinio/bh3-development/tree/main/Assets/ArcaneOnyx/BH3Demos/ReactiveGuards) | Three agents on the same facts, differing only in their guards: reactive, reactive with **Stops Its Own Branch** off, and plain Conditional Executions that never leave Idle | [Guards](../2-building-trees/06-guards.md) |
| [FpsTeams](https://github.com/platinio/bh3-development/tree/main/Assets/ArcaneOnyx/BH3Demos/FpsTeams) | Six agents on one tree and six reusable branch assets, with Required and Optional parameters fed differently per team, Functions as guard conditions, and two custom nodes (`FireWeapon`, `ReloadWeapon`) | [Sub-trees](../2-building-trees/07-sub-trees.md), [Custom nodes](../4-extending-with-csharp/01-custom-nodes.md) |
| [FunctionPicker](https://github.com/platinio/bh3-development/tree/main/Assets/ArcaneOnyx/BH3Demos/FunctionPicker) | An editor window (**Tools → BH3 Demos → Function Picker**) with five Script Graph Variable nodes feeding different ports, showing which Functions each is offered and why one is offered none | [Functions](../2-building-trees/08-functions.md) |
| [FunctionPorts](https://github.com/platinio/bh3-development/tree/main/Assets/ArcaneOnyx/BH3Demos/FunctionPorts) | Three agents running one tree and one Function, differing only in the argument fed at the call site; a fourth left unfed to show the error it reports | [Functions](../2-building-trees/08-functions.md) |
| [FunctionGraphs](https://github.com/platinio/bh3-development/tree/main/Assets/ArcaneOnyx/BH3Demos/FunctionGraphs) | One Function evaluated by several agents, its contract read off the asset, and a deliberately stale contract beside a fresh one | [Functions](../2-building-trees/08-functions.md) |
| [FunctionCalls](https://github.com/platinio/bh3-development/tree/main/Assets/ArcaneOnyx/BH3Demos/FunctionCalls) | A Function invoked from a plain MonoBehaviour, without a behavior tree | Visual Scripting Extension's [Functions guide](https://github.com/platinio/visual-scripting-extension/blob/main/docs/functions.md) |
| [WatchedKeyInheritance](https://github.com/platinio/bh3-development/tree/main/Assets/ArcaneOnyx/BH3Demos/WatchedKeyInheritance) | A guard waking on the facts its Function declares, one that stays asleep because the fact written is not declared, and a control with no trigger | [Guards](../2-building-trees/06-guards.md#watched-keys-and-functions) |
| [MissingTypeRecovery](https://github.com/platinio/bh3-development/tree/main/Assets/ArcaneOnyx/BH3Demos/MissingTypeRecovery) | A tree that is genuinely broken on disk, with a node whose type no longer exists, to practise **Replace missing type…** on. Do not add a `DemoWanderNode` class, or the tree heals and there is nothing to see | [Renaming and deleting node types](../4-extending-with-csharp/03-renaming-and-deleting-node-types.md) |
| [TimelineScrubber](https://github.com/platinio/bh3-development/tree/main/Assets/ArcaneOnyx/BH3Demos/TimelineScrubber) | Three guarded branches on deliberately non-harmonic timers, so the timeline shows aborts, sub-tree boundaries and selector fall-through | [Timeline](../3-debugging/02-timeline.md) |
| [WhyInspector](https://github.com/platinio/bh3-development/tree/main/Assets/ArcaneOnyx/BH3Demos/WhyInspector) | The classic bug, a guard reading a fact a sensor flickers, explained clause by clause. Lower **Interval Seconds** on the agent below about 0.4 to see the *Oscillating* clause | [Why panel](../3-debugging/03-why-panel.md) |
| [VariableWatch](https://github.com/platinio/bh3-development/tree/main/Assets/ArcaneOnyx/BH3Demos/VariableWatch) | A sensor publishing three facts, one branch running at two call sites with separate scratch, and a node writing agent state from inside a branch | [Variable watch](../3-debugging/04-variable-watch.md) |
| [Breakpoints](https://github.com/platinio/bh3-development/tree/main/Assets/ArcaneOnyx/BH3Demos/Breakpoints) | Six breakpoints: four that fire, one per kind of match, and two that are deliberately wrong so you can see how a breakpoint reports a question it cannot answer | [Breakpoints](../3-debugging/05-breakpoints.md) |
| [TpsQueryFunctions](https://github.com/platinio/bh3-development/tree/main/Assets/ArcaneOnyx/BH3Demos/TpsQueryFunctions) | A Tactical Position Selection query authored as a Function, beside two that are refused and why | [Tactical Position Selection](https://github.com/platinio/Unity-TacticalPositionSelection) |
| [RepositoryRemoval](https://github.com/platinio/bh3-development/tree/main/Assets/ArcaneOnyx/BH3Demos/RepositoryRemoval) | Historical: confirms the old project-wide graph ledger is gone and graphs are ordinary assets | [Migrating older trees](02-migrating-older-trees.md) |

> Some demo READMEs mention a **Build demo scene** menu item for regenerating the scene. Those builder scripts
> are not in the repository; open the committed scene instead.

---

## Tests

Contributors add tests beside the code they change:

| Assembly | Folder | Runs in |
|---|---|---|
| `ArcaneOnyx.BH3.Tests` | `Test/EditMode/` | The editor, no play mode. Node semantics, verification, authoring, serialization |
| `ArcaneOnyx.BH3.PlayTests` | `Test/PlayMode/` | Play mode. Machine lifecycle, timing, the recorder against a live agent |

Both are gated on `UNITY_INCLUDE_TESTS` and are not auto-referenced, so they never ship in a build. Run them
from **Window → General → Test Runner**.

`Test/EditMode/SampleTrees/` holds the trees `SampleTreeVerificationTests.cs` runs `bt_verify` over: five
trees with their Functions and script graphs, kept from the FPS and Flight Recorder samples that once shipped
here. They are fixtures, not something to open and play; the test holds their findings to a recorded count so
that a change which breaks real content fails loudly.

---

## Next

- [Migrating older trees](02-migrating-older-trees.md)
- [Glossary](03-glossary.md)
