# BH3 runtime debugger — the missing pieces

**Status:** design spec for implementation by an AI or engineer with access to the BH3 source.
Written from BH3's design documentation — verify exact type/member names against the code before building.

**Components 1, 2, 3, 4 and 6 are built and green.** Components 5 and 7 are still spec. See
[Component 1 — as built](#component-1--as-built) for the recording API,
[Component 3 — as built](#component-3--as-built) for the explanation engine everything else can reuse,
[Component 2 — as built](#component-2--as-built) for the timeline model and the canvas-ghosting seam,
[Component 4 — as built](#component-4--as-built) for the variable model and the shared debug session, and
[Component 6 — as built](#component-6--as-built) for breakpoints and the one hook they all ride on, and
read [Findings that change the rest of this spec](#findings-that-change-the-rest-of-this-spec) before
starting any of them — several assumptions in the original text do not survive contact with the code.

## What already exists (do not rebuild)

The behavior window can be opened, an entity selected, and its tree watched **live** — current active
branch visible in real time. That covers "what is it doing *now*." Everything below covers "what did it do
and **why**," which is what's missing.

## BH3 context you need

- Trees are `BehaviorTreeGraphAsset`s; `BehaviorTreeMachine.Awake` instantiates the graph per agent.
- Sub-trees run via `RunBehaviorTreeGraphNode`, instantiated per call site, each with its own variable
  scope; reads walk outward, `VariableKind.Graph` writes stay local, `VariableKind.Object` lives on the
  agent's Variables component.
- Guards (`ConditionalExecution` nodes) attach to an owner and are re-evaluated **every tick** while the
  owner runs; turning false aborts the owner's branch immediately. This means branch changes are caused by
  either a guard flip, a child's returned status, or selector fall-through — a small, recordable set of
  causes.
- Every node has a stable `guid`, already emitted by `BehaviorTreeDump.ToJson` / `bt_describe_tree`.
  **Address everything in the debugger by guid**, never by position or name. ⚠️ *A guid alone is not
  unique within one agent — see [Findings](#findings-that-change-the-rest-of-this-spec).*

## Component 1: Flight recorder (foundation for everything else)

A per-agent ring buffer recording events, not snapshots:

- `NodeEnter(nodeGuid, tick)`
- `NodeExit(nodeGuid, tick, ExecutionStatus)` — Success/Failure/Aborted with the aborting guard's guid
- `GuardEval(guardGuid, tick, bool)` — **record on change only**, not every tick (guards evaluate every
  tick; storing every eval explodes the buffer; transitions are the information)
- `VarWrite(scopeId, key, oldValue, newValue, writerNodeGuid, tick)` — values stringified with a length
  cap; scopeId distinguishes agent Object-vars from each branch instance's Graph scope
- `TreePushed / TreePopped(runNodeGuid, subTreeAssetId, tick)` — sub-tree boundaries
- Config: buffer length (default ~2000 ticks), on/off per agent, global kill switch. When disabled the
  recorder must be a no-op sink with near-zero cost (guard the calls, or compile out in release).

Implementation note: the recorder is a separate component/service observing the machine — do not weave
recording calls through node logic where avoidable; hook the machine's tick, the guard evaluation site,
and the variable-scope write path (single choke points each).

## Component 1 — as built

`Assets/ArcaneOnyx/BH3/Runtime/Debugging/FlightRecorder/`, namespace `ArcaneOnyx.BehaviorTree.Debugging`.
Everything below is what Components 2–7 read from.

| Type | What it is |
|---|---|
| `BehaviorTreeFlightRecorder` | One agent's black box. `Events`, `CallSites`, `Tick`, `Enabled`, `IsRecording`, `Clear()`. |
| `BehaviorTreeEventRing` | Fixed-size, allocated once, oldest-first enumeration. `Count`, `Capacity`, `Dropped`. |
| `BehaviorTreeEvent` | Flat readonly struct. `Kind, Tick, Sequence, Frame, Time, CallSiteId, NodeGuid, RelatedGuid, Status, Flag, Key, OldValue, NewValue, Writer`. **Several fields mean different things per `Kind` — the type's own doc comment carries the table, and `GuardEval` inverts the usual guard/owner roles.** |
| `BehaviorTreeEventKind` | `NodeEnter, NodeExit, NodeAborted, NodeSkipped, GuardEval, VariableWrite, TreePushed, TreePopped, ServiceTick`. |
| `BehaviorTreeCallSite` | `Id, ParentId, RunNodeGuid, AssetName`. Root is id 0. |
| `BehaviorTreeFlightRecorders` | Static registry + `GloballyEnabled` kill switch + `DefaultCapacity`. **This is the multi-agent view's data source (Component 7).** |
| `BehaviorTreeRecorder` | The `[Conditional]` facade the runtime calls. Do not call a recorder directly from runtime code. |
| `BehaviorTreeRecordingDump.ToJson(recorder)` | The recording as JSON. Guids match `bt_describe_tree`. |
| `FlightRecorderWindow` (editor) | **Tools > BH3 > Flight Recorder.** Reads a live recording back in English: agent picker, per-kind filter, fact toggles, Copy JSON. Not the scrubber — see below. |
| `FlightRecorderDemoBuilder` (editor) | **Tools > BH3 > Flight Recorder > Build demo scene.** Generates a two-tree agent built to exercise the cases that are easy to get wrong. |

Reach an agent's recorder through `BehaviorTreeMachine.FlightRecorder`.

**Gating.** `BehaviorTreeRecorder`'s methods carry `[Conditional("UNITY_EDITOR")]` **and**
`[Conditional("BH3_DEV_TOOLS")]`, which is an OR. So recording is on in the editor with no project setup,
and in a player build only when `BH3_DEV_TOOLS` is defined — the compiler removes the call sites and their
argument evaluation entirely otherwise. `BH3_DEV_TOOLS` is deliberately not recorder-specific: the
spawn-time asserts specs 02 and 04 both want in `BehaviorTreeMachine.Awake` belong behind the same switch.

**Where the hooks live** (four choke points, no recording woven through node logic):

- `BehaviorTreeMachine.Awake` — builds and registers the recorder, binds it across the graph, binds the
  root scope. `Update` calls `BeginTick`. `OnDestroy` unregisters.
- `BehaviorTreeNode.OnNodeEnter` / `OnUpdateInternal` / `OnNodeExit` — entry, guard evaluation, aborts,
  skips, exits. The first two were already `sealed override`; the third became one.
- `RunBehaviorTreeGraphNode.SetVariableScope` / `SetFlightRecorder` — registers the call site, reaches the
  branch's nodes.
- `GameplayNode.SaveVariable` — variable writes, recorded before the write so the old value still exists.

**Guid resolution is the next component's real problem, and the window is where it currently lives.** The
recorder stores guids and never formats anything, which is what keeps it allocation-free — so a recording is
unreadable until something maps guids to names, and at runtime the only place that mapping exists is the
machine's own instantiated graph (descend into every `RunBehaviorTreeGraphNode` instance). Two extra rules
the window learned the hard way and Components 2–3 will need:

- **A guard's own name is worthless.** Every one is called "Boolean Conditional Execution". What a designer
  recognises is the condition, so name a guard by walking its `Value` input back to the terminal source node
  — that yields `[hasTarget]`, `[not hasNoise]`. Do not special-case `Not`; walking to the end handles it.
- **`SetComment` only works on `BaseVisualScriptingNode` and `Literal`.** It logs a warning and does nothing
  on anything else, so `WaitTime` and `RunBehaviorTreeGraphNode` cannot be renamed that way. Those are told
  apart by their call site and their guard instead.

**Verified by** `BehaviorTreeFlightRecorderTests` (edit mode, 20 tests) and `FlightRecorderEndToEndTests`
(play mode, 2 tests, real `BehaviorTreeMachine` on a real GameObject ticked by the player loop). The play-mode
one is the only place the machine wiring is checked at all, because edit mode drives nodes directly and
never stands a machine up.

## Component 2: Timeline scrubber

A horizontal track under the existing live view:

- One lane per depth level of the active path; colored segments = which node was active, segment
  boundaries = enter/exit events. Markers for aborts (with guard guid) and sub-tree push/pop.
- Scrub → the canvas renders the tree state **at that tick** (ghosted styling to distinguish from live).
- Controls: play/pause, step-forward-one-tick, jump-to-previous/next branch change. Works while the
  editor is paused and from a loaded recording (Component 7).

## Component 2 — as built

Engine in `Assets/ArcaneOnyx/BH3/Runtime/Debugging/Timeline/`, namespace `ArcaneOnyx.BehaviorTree.Debugging`;
UI in `Assets/ArcaneOnyx/BH3/Editor/Debugging/`. Like Component 3, **the engine is pure C# over an
`IBehaviorTreeRecording`** — no Unity types, no editor types, no machine — so an exported recording scrubs
as well as a live agent and the model is testable without standing up a scene.

| Type | What it is |
|---|---|
| `BehaviorTreeTimeline.Build(recording, topology?)` | The whole model: `Lanes`, `Markers`, `ChangeTicks`, `FirstTick`, `LastTick`, `Dropped`. Topology is optional and supplies names only. |
| `BehaviorTreeTimelineLane` | One horizontal track of non-overlapping segments. `Depth` is **not unique** — a second lane at the same depth is how concurrency is drawn honestly. |
| `BehaviorTreeTimelineSegment` | One node active over a span: `CallSiteId`, `NodeGuid`, `Name`, `EnterTick`, `ExitTick`, `Outcome`, `GuardGuid`, `Depth`. `IsOpen` means still running at the end of the buffer. |
| `BehaviorTreeTimelineMarker` | A pin: `Abort` (carries the guard), `TreePushed`, `TreePopped`. Things that happen *at* a tick rather than over a span. |
| `BehaviorTreeTreeState.At(recording, [timeline,] tick)` | Every node's status at one tick. **This is what the canvas is ghosted from.** Also `GuardResult`, `WasAborted`, `RunningCount`. |
| `BehaviorTreeScrubOverride` (Editor) | The read-only redirect the canvas widgets consult. `StatusOf(node, live)` / `IsRunning(node, live)` return the live value when nothing is scrubbing, so there is one code path. |
| `BehaviorTreeTimelinePanel` (Editor) | The renderer: lanes, pins, ruler, transport, `Load…`, plus `RequestScrub(tick)` for other panels to move the playhead. Implements `ISidebarPanelContent`, so either dock can host it. |
| `BehaviorTreeDebugTarget` (Editor) | Which agent the debugging panels are about: canvas reference → hierarchy selection → the only agent recording. Both panels call it. |
| `BottomDock` (GraphCore) | A full-width strip under the canvas with a draggable top edge. Generic — it takes `ISidebarPanelContent` and knows nothing about behavior trees. |

**Where the hooks live.** Three reads of live state drive canvas rendering, and all three now go through
`BehaviorTreeScrubOverride`:

- `BehaviorTreeNodeElementWidget.DrawLastExecutionIcon` — the status icon.
- `BehaviorTreeGraphDrawer.DrawTransition` — the green transition colour.
- `BehaviorTreeTransitionWidget.DrawOverlay` — whether the highlighted transition is drawn at all.

**Nothing writes to a node.** Finding 16 was about Visual Scripting debug data, but the rule generalises: the
agent keeps running while you study the past, so written-back history would be overwritten within a frame and
anything that survived would sit on the canvas after the panel closed looking live.

**Which agent is resolved once, in `BehaviorTreeDebugTarget`, and both panels call it.** The why-inspector
established the rule (canvas reference → hierarchy selection → the only agent recording, and say so when
none of those answer); the scrubber follows it because the failure is worse here — ghosting paints the
chosen agent's history onto whatever nodes are on screen. Sharing the resolution rather than copying it is
the point: two independent answers could disagree, and then the explanation describes one agent while the
ghosted canvas shows another. Switching agents returns the scrubber to live, since a tick number means
nothing in a different recording.

**GraphCore gained a bottom dock** — `IGraphContext.bottomPanels` + `GraphContext.BottomPanels()` +
`BottomDock`, mirroring the sidebar. Component 3 needed no GraphCore change; this one does, because a sidebar
is a tall narrow column and a timeline's axis is time. The strip is reserved after the canvas row inside the
same vertical layout, so the canvas — which expands — gives up exactly that height and nothing needs telling.

**Everything on screen answers for the playhead, not for the end of the buffer.** Three places had to learn
this and each was wrong in a different way, so treat it as the rule for Components 4–7 rather than as three
bug fixes:

- Bars are filled in two parts, split at the playhead — what was true *then*, then the future dimmed. A bar
  painted with its segment's final outcome asserts an ending that has not happened yet (Finding 22).
- `BehaviorTreeWhyPanel` passes the scrubbed tick to `Explain`, so the sentence describes the moment the
  canvas is ghosted to. The `⏱` links move the scrubber the other way, closing the loop.
- The tick comes from `BehaviorTreeScrubOverride.TickFor(recording)`, which returns it **only for the
  recording it was taken from**. A tick number means nothing outside its own recording, so a live agent's
  tick applied to a loaded file would produce a confident answer about a moment that never existed.

**Verified by** `BehaviorTreeTimelineTests` (edit mode, 24 tests), including a real guarded abort driven
through the real recorder, a JSON round trip proving acceptance criterion 3, and a test asserting the
timeline and the ghosted state agree at every change tick. Suite total is 232, 230 passing — the two
known-unrelated failures and nothing else. Demo in `Assets/ArcaneOnyx/BH3Demos/TimelineScrubber/` — see its
README.

### Findings from building Component 2

**17. A sub-tree's nodes record no exit when their branch is aborted, and nothing else closes them.** The
abort is recorded against the `RunBehaviorTreeGraphNode` in the *caller's* graph; the instance's own nodes
are simply abandoned. A replay that waits for exits therefore leaves them open for the rest of the recording
— which drew a 1.5s `Wait` as a bar thousands of ticks long, and, because depth was a count of everything
open, pushed every later branch one lane further down. A real recording produced **34 lanes for a 7-level
tree**. The fix is `CloseCallSitesUnder`: a call site cannot outlive the node running it, and
`recording.CallSites` already carries `ParentId` + `RunNodeGuid`, so the teardown is derivable from data the
recorder already emits. **Any component that replays the buffer needs this rule** — it is Finding 8's
sibling, and like Finding 8 it was found by running the demo rather than by reading the code.

*Since resolved at the source:* the node audit (`make node call exit when interrupting a branch`) made
interrupted nodes record their exits, so fresh recordings no longer abandon them. `CloseCallSitesUnder` is
now a safety net rather than load-bearing — keep it, because recordings exported before that change still
need it and nothing else bounds an unclosed segment.

**18. Depth has to be per scope, not global.** Even with abandoned nodes closed, `open.Count` is wrong: a
tree whose `Entry` and `Repeater` stay entered forever makes every later node look deeper than it is.
Depth is `scopeDepth[scope] + openInScope[scope].Count`, where a call site's base depth is the depth of the
node that opened it, plus one. That reproduces the tree's real shape — 7 lanes for the demo — without
needing a topology, which matters because topology is optional by design.

**19. There must be one source of truth for "what was running at tick N".** `BehaviorTreeTreeState`
originally replayed the events itself, which meant it did not inherit Finding 17 and disagreed with the
timeline: the bar said a node stopped at tick 457, the ghosted canvas lit it up at tick 2357. State is now
derived from the timeline's segments, and a test asserts they agree at every change tick. A debugger that
contradicts itself is worse than one that says less.

**20. A handoff is not an overlap.** Lane packing originally required a segment to start strictly after the
previous one ended, so a selector failing one branch and entering the next on the same tick spilled into a
second lane — doubling the lane count on every real recording. Segments may now start exactly where the
previous one ended.

**21. Ludiq `[Serialize]` members need the *component* dirtied, not the GameObject.** Building the demo
scene from code, `machine.nest.SwitchToMacro(tree)` held in memory and vanished on save until
`EditorUtility.SetDirty(machine)` replaced `SetDirty(agent)` — the nest lives in the component's `_data`
blob, and `OnBeforeSerialize` only runs for a dirty component. Worth knowing for any generated scene.

**22. A span coloured by its final outcome is wrong at every tick before its end.** Parked at tick 0, a
Selector that would not exit until tick 18 read `Succeeded` — with empty lanes beneath it, because its
children had not been chosen yet, so it looked like a Selector that succeeded on its own. The model was
right the whole time (`BehaviorTreeTreeState` reports Running before the exit tick), so the *bar* and the
*ghosted canvas* disagreed. The "one source of truth" test could not catch it: it asserts the model agrees
with itself and says nothing about what the painter does with it. **Component 5's guard lane will walk into
exactly this** — any lane-style view must colour by what was true at the playhead.

**23. "Never ran" is only true when the vantage point is the end of the recording.** `ExplainNeverRan`
guards the clipped- and empty-buffer cases but assumed "nothing at or before now" meant "nothing at all" —
which held until the why panel started following the scrubber, at which point it told the reader a branch
never ran while the recording had it entering two ticks later. It now looks forward for the node's first
lifecycle event after the vantage point and says "has not run yet as of tick N; the first is at tick M",
with a tick link to jump there. It reports only *that* something happens later, never *what* — leaking the
outcome would undo the point of explaining at a tick.

**24. Ticks are not frames, and the gap is where bugs hide.** Two ticks can share a frame: the first frame
carries both the spawn-time descent and the first `Update`, and a sensor whose `Update` runs before the
machine's lands its write in the *previous* tick's bucket. So `tick 22 → frame 23` is normal, not a
recording error. Separately, the timeline showed composites taking several ticks to reach a leaf — that one
was a node-logic bug, found *because* the lanes made the gap visible, and fixed by an audit of the nodes.
Do not read a multi-tick descent as expected behaviour.

## Component 3: The "why" inspector (highest designer value — build this even if you cut others)

Click any node → a causal sentence assembled from recorder events, not inferred:

- "Aborted at tick 412: guard 'no enemy in range' flipped false (hasTarget wrote true at tick 412 by
  node VisionService)."
- "Never entered: parent Selector chose 'Combat' (priority 1) at tick 300; this branch is priority 2."
- "Exited Failure at tick 520: child 'WaitUntilReachNavTargetPosition' returned Failure."

Every clause links: clicking the guard highlights it on canvas; clicking the tick moves the scrubber.
This turns debugging from inference into reading, and it's the feature that lets designers self-serve
instead of filing programmer tickets.

## Component 3 — as built

Engine in `Assets/ArcaneOnyx/BH3/Runtime/Debugging/Why/`, namespace `ArcaneOnyx.BehaviorTree.Debugging`;
UI in `Assets/ArcaneOnyx/BH3/Editor/Debugging/BehaviorTreeWhyPanel.cs`.

**The engine is pure C# and takes a recording, not a live agent.** No Unity types, no editor types, no
machine. That is what lets the sidebar panel, an exported recording opened a week later, a test, a CLI and
an authoring agent reading a replay all get the same answer, and it is why Components 2, 4, 5 and 7 should
call it rather than re-derive causes from the buffer themselves.

| Type | What it is |
|---|---|
| `BehaviorTreeExplainer.Explain(recording, scopeId, nodeGuid, topology, atTick)` | The whole engine. `atTick` negative means "end of recording"; pass a tick and it explains that moment without leaking the future, which is what the scrubber needs. |
| `BehaviorTreeExplanation` | `Outcome`, `Headline`, `Clauses`, `SubjectName`, `CallSitePath`, `AtTick`. `ToString()` renders the lot as text. |
| `BehaviorTreeExplanationClause` | `Role` (Cause / Evidence / Context / Caveat), `Text`, `Link`. |
| `BehaviorTreeExplanationLink` | Structured target: `Node`, `Guard`, `Tick`, `Variable` — kept separate from the words so a UI gets click targets without parsing sentences. `Tick` links move the scrubber (Component 2). |
| `BehaviorTreeOutcome` | `NoRecord, Running, Succeeded, Failed, Aborted, Skipped`. |
| `IBehaviorTreeRecording` | What the engine reads. `BehaviorTreeFlightRecorder` implements it, so a live agent needs no adapter. |
| `BehaviorTreeRecordingSnapshot` | A recording as plain data — what an import produces and what a test writes by hand. `From(recorder)` freezes a live one. |
| `BehaviorTreeRecordingImport.FromJson / TryFromJson` | Reads back `BehaviorTreeRecordingDump.ToJson`. Round trip is covered by a test. |
| `IBehaviorTreeTopology` / `BehaviorTreeNodeInfo` | The structural half: names, parent, children in priority order, and which variables a guard reads. **Optional** — every sentence the buffer alone justifies is produced without it. |
| `BehaviorTreeGraphTopology.From(machine \| graph)` | Builds a topology from a real tree, walking into sub-tree instances that have already been entered. |
| `BehaviorTreeExplainer.CallSitesFor(recording, guid)` | Which call sites a guid ran in. The canvas knows the node clicked, not which copy of a shared branch. |
| `AgentVariableWriter` (`Runtime/Variables/`) | Component sensors write through. `Write(this, key, value)` writes to the agent's Variables **and** reports the change by name, so facts get writer attribution. A component rather than a base class — a sensor usually already derives from something, and a debugging concern should not spend its one inheritance slot. |
| `BehaviorTreeRecorder.ExternalVariableWrite(machine, writerName, …)` | The gated facade for the above. Sensors should call this, not a recorder directly. |
| `JsonReader` / `JsonValue` (UnityExtensions) | Minimal JSON parser, the counterpart to the existing `JsonWriter`. Reusable by any dump this project writes. |

**Guard traces** (`Runtime/Debugging/Traces/`) answer *why* a guard was false rather than only that it was.
Most branches in a real tree are guarded by a Visual Scripting graph, so without this the commonest
explanation ends at "it returned false", which is the question restated.

| Type | What it is |
|---|---|
| `GuardTrace` | One guard transition: `Tick`, `Sequence`, `CallSiteId`, `GuardGuid`, `OwnerGuid`, `Result`, `Chain`, `Snapshots`. `Describe()` renders the chain on one line. |
| `GuardTraceNode` | One node of the behavior-tree-side chain: guid, name, type, value, `Depth`, `ParentIndex`, `SnapshotIndex`. Flattened depth-first so it survives a JSON round trip. |
| `GuardGraphSnapshot` / `GuardWireValue` | A Visual Scripting graph's interior at that instant: per connection, the guids (matching `FlowGraphDump`), the value, and `WasEvaluated`. |
| `GuardTraceRing` | Side ring, default 64, `Find(tick, sequence)`. Separate from the event ring because a trace is variable-sized and the event struct's flatness is what keeps recording allocation-free. |
| `GuardTraceCapture.Capture(...)` | Builds one. Called from `BehaviorTreeFlightRecorder.GuardEval` on transitions only. |
| `recorder.TracingEnabled` / `BehaviorTreeFlightRecorders.TracingGloballyEnabled` | Toggles independent of recording, because a trace costs far more than an event. |
| `BehaviorTreeExplanation.Trace` | The trace attached to the flip being explained, or null. |
| `BehaviorTreeGuardSnapshotWindow` | Read-only ghosted canvas of a snapshot. |

Capture has two halves because the two sides keep history in different places. Behavior tree ports keep
none, so the chain is recovered by **re-pulling each `ValueOutput`** in the same instant — safe because
those are pure fetch nodes, which is already a rule of the project. Visual Scripting has already recorded
every wire it pushed a value down, so its half is **harvested** from
`ValueConnection.DebugData.assignedLastValue` rather than recomputed.

**The UI is a sidebar panel**, registered in `BehaviorTreeGraphContext.SidebarPanels()` next to *Blackboard*
and *Graph Inspector* — **no GraphCore changes were needed**. It reads the canvas selection, picks an agent
(or a loaded file), and renders clauses; clauses pointing at a node on the open canvas get a `→` button that
selects it. Export and Load buttons round-trip a recording to JSON.

**What it can say.** Aborted (naming the guard, when it flipped, what the guard was reading, and the write
that flipped it); skipped
("never entered", worded differently from an abort on purpose); exited Success/Failure (naming the child
that caused it, only when the topology confirms the parentage); running (how long, which guards hold);
never ran (which higher-priority sibling won); and a guard's own history. Plus an oscillation clause when a
node entered and aborted repeatedly, which is Component 5's signal available early because the buffer
already contains it.

**Verified by** `BehaviorTreeWhyInspectorTests` (edit mode, 31 tests), including several that drive real
nodes through the real recorder so the engine and Component 1 cannot drift apart, trace capture against a
real graph, ring eviction, and JSON round trips for both events and traces. Demo in
`Assets/ArcaneOnyx/BH3Demos/WhyInspector/` — see its README.

### Findings from building Component 3

**8. An abort is followed by its own exit in the same tick, and the exit is recorded second.** Taking the
last lifecycle event at face value reported `Exited Failure` and dropped the guard — losing the answer in
exactly the case the panel exists for. `PreferAbortOverItsOwnExit` handles it, and a test pins it. **Any
component that reads the buffer for "what happened to this node" needs the same rule.** Found by running the
demo, not by reading the code.

**9. A guard's name is useless and its inputs' names are not.** Every `BooleanConditionalExecution` is
called "Boolean Conditional Execution", so a tree with four guards names them all identically. The topology
names a guard after the value source it ultimately reads — "hasTarget", "not hasTarget", the labels
`GuardOnVariable` already writes — which is the difference between a sentence a designer can act on and one
they cannot.

**10. Guard inputs resolve through Visual Scripting, and had to.** `GuardOnVariable` — the documented guard
pattern — feeds the guard from a `ScriptGraphAsset`, not a behavior tree node. A walk that only followed
node ports would resolve nothing on the majority of real trees and hedge every sentence. The topology reads
`node.scriptGraphAssets` (the same seam the tree dump uses) and pulls variable names out of the
`Unity.VisualScripting.GetVariable` units. **This is what makes a write a `Cause` rather than an
`Evidence` + caveat** — the engine deliberately downgrades its wording when it cannot prove the link.

**11. The machine stops ticking when the root tree returns Success or Failure.** A Selector whose branches
all fail does exactly that, so an agent tree without a `Repeater` under Entry runs once and goes quiet —
and the recording then shows every later sensor write piled onto the final tick. Worth knowing before
concluding a recording is broken.

**12. `ExternalVariableWrite` now has one caller** (`AgentVariableWriter`), which closes Finding 5 enough to
demonstrate the flagship sentence. It is deliberately a seam and not a fact system: no registry, no
`[ProvidesFact]`, no validation. Spec 02 should build on it rather than around it.

**13. Guard trace capture re-pulls unconditionally, and that is the obvious place to make this faster.**
Every guard transition walks the guard's input DAG calling `ValueOutput.GetPortValue()` on each node, and
because pulling a behavior tree node transitively pulls whatever feeds it, a chain ending in a
`VisualScriptGraphVariable` **executes that script graph a second time**. It is correct — those are pure
fetch nodes by project rule, and re-pulling is the only way to recover the intermediate values, since
behavior tree ports keep no history — but it is not cheap, and it is paid per transition per guard.

Three improvements, cheapest first, none of them needed yet:

- **Stop at the Visual Scripting boundary.** The graph's own result is already on the `ScriptGraphOutput`'s
  `Result` connection in debug data, so it need not be recomputed. What that costs is the behavior tree
  nodes *above* the boundary — a `Not` between the guard and the graph would have to be inferred rather
  than observed, which is exactly the guessing this tool exists to remove. Worth it only if profiling says so.
- **Skip graphs that can mutate.** Scan a guard graph once for units that write (`SetVariable`, event
  triggers) and refuse to re-pull those, falling back to debug data. This is a *correctness* guardrail as
  much as a performance one: the fetch/act separation is a rule, not something enforced, and a non-pure
  guard graph currently fires its side effects twice per transition in the editor.
- **Own the evaluator.** If native value graphs ever replace Visual Scripting here, wire values can be
  recorded directly during evaluation and both the re-pull and the debug-data harvest disappear. Nothing
  built here digs that hole deeper — the capture is one class behind one call site.

**14. Wire capture is editor-only, and that is structural.** `fetchRootDebugDataBinding` is installed by
Unity's editor assembly, so `stack.hasDebugData` is false in any player build. A `BH3_DEV_TOOLS` build
still gets the behavior-tree chain and the graph's final output — just not the interior wires. The capture
reports an empty snapshot rather than pretending there was nothing to see.

**15. A snapshot is values overlaid on an asset, not a copy of the graph.** Edit a guard graph after
recording and old traces can name wires that no longer exist. The viewer lists those as orphaned rather
than failing, because a partially readable account of a bug that already happened still beats none. The
same is true of layout: the window lays units out using the asset's *current* positions.

**16. Do not render a snapshot by writing values back into live debug data.** It was the cheap option and
it is wrong twice: that data is what the next capture reads, so showing a snapshot would corrupt the
following one, and the values persist on the real canvas after the window closes looking exactly like live
values. `BehaviorTreeGuardSnapshotWindow` draws its own ghosted canvas for this reason.

### Open, designed but deliberately not built

Two follow-ups were investigated in depth and stopped before implementation. Both are wanted; neither is
blocked by anything except a decision. The code facts below were verified against the packages, not
recalled — start from them rather than re-deriving.

#### A. Inspecting object and collection values in a trace

**The gap.** Every value in a trace — chain node and wire alike — is a string produced by
`BehaviorTreeEvent.Describe`, which is `value.ToString()` capped at 64 characters. Primitives, strings,
`Vector3` and `UnityEngine.Object` all read well. **Collections and plain C# classes do not**: a
`List<GameEntity>` renders as `System.Collections.Generic.List``1[ArcaneOnyx.AIEntit…` — the type name,
truncated. So "which targets was this guard evaluating" currently has no answer, which is the case a TPS
query makes routine.

Note this ceiling is not one Unity's own renderer lifts: VS draws a wire value with `ToShortString()`, a
single short string, and clicking a unit shows its *configuration* rather than runtime values. Structured
capture is the only route to element-level inspection, whichever canvas draws it.

**Why it is like that, and why that reasoning does not transfer.** `Describe` was written for the event
ring, where it is right: 2048 events covering every variable write, so holding the `object` would box every
struct on the hot path and let a reference type keep mutating after recording — the export would then show
the value at export time rather than at write time. Guard traces have a completely different budget:
transitions only, 64 per agent, already walking a DAG and harvesting a graph. The event ring's rule was
applied to traces without re-deriving it, and for collections that is the wrong trade.

**Design as far as it got.** Keep `"value"` a string holding a *summary* (`List<TacticalPosition> (12)`),
and add an **optional** sibling `"detail"` object. Additive on purpose: the existing importer and any other
reader of the dump keep working and simply see a better string.

```json
{ "value": "List<TacticalPosition> (12)",
  "detail": { "kind": "list", "count": 12, "shown": 3,
              "items": ["pos (3.5, 0.0, -2.1) score 0.82", "…"] } }
```

It lands in the same exported recording file — there is no second file. Surfaces: the Why panel's GUARD
CHAIN rows become foldouts, and the snapshot window gains a detail pane shown when a wire is clicked (a
wire label has no room for a tree).

**Two hazards that are more important than the cost.**

- **Never enumerate an arbitrary `IEnumerable`.** It can be infinite, expensive, or side-effecting, and a
  debugging aid that drains someone's iterator has changed the program. Expand **only `ICollection`** —
  O(1) `Count`, safe to walk. A LINQ query object should show its summary and no detail.
- **If object expansion is ever added, fields only — never properties.** A property getter runs arbitrary
  code during a debug capture. A static `Type → FieldInfo[]` cache makes the reflection itself cheap; the
  getters are the trap.

**Cost.** Scalars are unaffected — expansion triggers on collection types only. A fully expanded value at a
cap of 8 elements is roughly 350 bytes, so realistically about +1 KB per trace and **~60 KB per agent** on
top of the existing ~200 KB event ring. Caps are what bound it: a query passing 200 candidates must cost
the same as one passing 12. `BehaviorTreeFlightRecorders.TracingGloballyEnabled` already exists as the
switch for a crowded scene.

**Recommendation: collections at depth 1; defer field expansion.** The gap that leaves is a
`List<SomeStruct>` whose struct has no `ToString` override — each element then reads as a type name again.
Unity-typed elements (`GameEntity`, anything deriving `UnityEngine.Object`) read fine. **So the open
question is whether the values worth inspecting here are Unity objects or plain structs** — if the latter,
one level of fields-only expansion is what makes them readable. Second open question: the element cap
(8 was proposed) and whether it belongs beside the other knobs on `BehaviorTreeFlightRecorders`.

#### B. Rendering a snapshot in Unity's real Visual Scripting graph window

**Wanted for** real port inspection, unit inspectors, hover, and the familiar layout, instead of the
purpose-built canvas in `BehaviorTreeGuardSnapshotWindow`.

**It is possible, and there is a safe route.** `GraphDebugDataProvider` keys debug data per root:

```csharp
private static Dictionary<IGraphRoot, IGraphDebugData> rootDatas
```

So **clone the `ScriptGraphAsset`**, write the recorded values into the *clone's* debug data, and open
VS's own window on it (`GraphWindow.OpenTab(reference)` / `OpenActive(reference)`). A clone is a different
`IGraphRoot` and gets its own bucket, so the live asset's data is never touched — which is what makes this
safe where writing back into the live graph is not (see Finding 16).

**The hard blocker.** `ValueConnectionWidget.DrawForeground` gates on:

```csharp
var showLastValue = EditorApplication.isPlaying && ConnectionDebugData.assignedLastValue;
var showPredictedvalue = BoltFlow.Configuration.predictConnectionValues && !EditorApplication.isPlaying && …;
```

Recorded values are drawn **only while play mode is running**. Pausing is fine — `isPlaying` stays true.
But once play mode *stops* it does not go blank: it falls through to `Flow.Predict`, which **re-evaluates
the graph live** and draws those numbers in the same place with the same styling. A snapshot opened after
stopping would therefore show confident, wrong values indistinguishable from recorded ones. That is inside
Unity's widget and cannot be reached from here.

Mitigations: restrict the real-graph view to `EditorApplication.isPlaying` and fall back to the built-in
canvas otherwise; and/or force `BoltFlow.Configuration.predictConnectionValues` off while a snapshot tab is
open, saving and restoring it.

**Other edges.** The clone is a live, fully editable `ScriptGraphAsset` — someone will edit a snapshot
believing it is the real graph and lose the edit, so it needs an unmistakable name. It also leaks a
`ScriptableObject` per snapshot viewed unless destroyed when the tab closes.

**Do not** take the other apparent route — swapping `GraphPointer.fetchRootDebugDataBinding`. It is a
public static, but the *running* flow writes through the same binding, so while installed it would redirect
live recording into the snapshot.

**Recommendation: hybrid.** Real VS graph via clone while `isPlaying` (pausing is how anyone would actually
study this), the built-in canvas when stopped, and the window states which one is on screen.

## Component 4: Variable watch with write attribution

Table of every scope visible from the selected agent's active path (agent Object vars + each active
branch's Graph scope, labeled by branch):

- Columns: scope, key, current value, last-write tick, writer node (guid → click to highlight).
- Row click → jump scrubber to the write; history popover shows the last N writes from the buffer.

## Component 4 — as built

**Status: done and green.** Engine in `Assets/ArcaneOnyx/BH3/Runtime/Debugging/Variables/`, namespace
`ArcaneOnyx.BehaviorTree.Debugging`; UI in `Assets/ArcaneOnyx/BH3/Editor/Debugging/`. Like Components 2 and
3, **the engine is pure C# over an `IBehaviorTreeRecording`**, so an exported recording reads as well as a
live agent and the model is testable without a scene.

| Type | What it is |
|---|---|
| `BehaviorTreeVariableWatch.At(recording, tick, topology?, historyLimit?)` | The whole model: `Scopes`, `Tick`, `IsEmpty`. Negative tick means end-of-recording, matching `Explain`. Topology is optional and supplies writer names only. |
| `BehaviorTreeVariableWatchScope` | One store: `Kind`, `CallSiteId`, `Label`, `Rows`. Grouped by `VariableKind` first, by call site only within `Graph`. |
| `BehaviorTreeVariableWatchRow` | One variable at the tick: `Key`, `Value`, `LastWriteTick`, `History`, `WriteCount`, `HistoryClipped`. |
| `BehaviorTreeVariableWatchWrite` | One change: tick, sequence, old, new, `WriterGuid` + `WriterName`, `HasLocatableWriter`. |
| `BehaviorTreeDebugSession` (Editor) | What the debugger is looking at — recording, playhead, `IsScrubbing` — published by the timeline, read by everyone else. `TickFor(recording)` returns the tick only for its own recording. |
| `BehaviorTreeVariableWatchPanel` (Editor) | The renderer. Sidebar panel, opens on the **right**. |
| `IAnchoredSidebarPanelContent` (GraphCore) | Lets a panel state a preferred anchor, applied once when the panel is first created. |
| `AgentVariableWriter` (`Runtime/Variables/`) | The component a sensor writes through, replacing `AgentFactPublisher`. `Write(this, key, value)` — the caller names itself, so several components can share one writer. |

**`VariableKind` is now recorded on every write** (`BehaviorTreeEvent.VariableKind`, emitted as
`"variableKind"` in the dump — it was `"scope"` when this was written, see Finding 30). It had to be: `CallSiteId` is where a write came *from*, not where the value lives, so a node
inside Combat writing agent state would otherwise be filed as Combat's private scratch. `Flow` is the
not-applicable slot on every other event kind — BH3 rejects Flow variables outright, so it cannot collide
with a real write. Recordings exported before this import as `Object` rather than as the enum's default.

**The table answers for the playhead, never for the present**, which is the rule Component 2 established and
the reason there is no live-value column. A variable that was declared but never written therefore has no
row at all — the alternative, merging live declarations in, would put rows meaning "now" beside rows meaning
"at tick 400" and silently change which you were reading depending on whether the scrubber was parked. Live
values already have two homes: the Blackboard panel and the Flight Recorder window.

**One source of truth for which recording is up.** The panel has no agent picker and no Load button; the
timeline publishes what it is showing through `BehaviorTreeDebugSession` and the watch reads it, falling back
to `BehaviorTreeDebugTarget` when the timeline has never been opened. Two independent answers is how one
panel ends up describing a recording the ghosted canvas is not showing.

**Verified by** `BehaviorTreeVariableWatchTests` (edit mode, 24 tests), including writes driven through a
real `GameplayNode` and the real recorder so the model and Component 1 cannot drift apart, a sensor writing
through `AgentVariableWriter`, a JSON round trip, and a hand-written legacy recording carrying neither
property. Suite is 282 EditMode tests, 281 passing — the known TPS failure and nothing else. Demo in
`Assets/ArcaneOnyx/BH3Demos/VariableWatch/` — see its README.

**What the panel had to learn from being used**, all three of which are the same mistake in different
clothes — the panel knew something the reader could not see:

- A row's `→` was disabled in every case it existed for, until it learned to cross assets (finding 28b).
- An unlabelled filter box in a table of unlabelled columns is a control you have to experiment with. It now
  carries a placeholder and a clear button.
- "No variable writes recorded" is true and useless when the real reason is that the debugger is pointed at
  a different agent. The empty state now names the agent and says how many others are recording.

### Findings from building Component 4

**25. The write's kind was the missing half of "where does this value live".** `GameplayNode.SaveVariable`
had it and threw it away before the recorder saw it. Everything else in the table was already derivable;
this one field was not derivable from anything.

**26. Outside the `ArcaneOnyx.BehaviorTree` namespace, node type names collide with Visual Scripting too.**
BREAK-1 in the authoring skill lists five port types. It is wider than that: `Sequence`, `SetVariable` and
`GetVariable` are all ambiguous in a file that is not in BH3's own namespace — which is every demo builder,
since those live in `ArcaneOnyx.BH3Demos`. Inside BH3's namespace the BH3 type wins silently, so this only
bites from outside. Aliases (`using BTSequence = ArcaneOnyx.BehaviorTree.Sequence;`) are tidier than
qualifying each use.

**27. `BehaviorTreeAuthoring.SetComment` does nothing on `GetVariable`, not only on non-Literals.** The
documented limit is "`BaseVisualScriptingNode` and `Literal`", but `GetVariable` derives from `Literal` and
still warns `no field 'comment' on GetVariable`. It writes a private `comment` field that not every Literal
subclass has. Check for the warning rather than trusting the base class.

**28b. "Select the writer on the canvas" is disabled in every case it exists for, unless it crosses assets.**
The why-inspector's rule — only offer the jump when the node is on the open canvas — quietly makes the
button dead in a variable watch. A sensor is not a node at all, and a node that writes agent state does it
from inside a branch, whose asset is a different canvas; on the demo's agent tree *every* arrow was grey.
The fix needed the write's `CallSiteId` kept on the row (the scope cannot supply it — an Object write from
inside Combat is filed under `agent`), then resolving the call site to its asset via the
`RunBehaviorTreeGraphNode` that pushed it, opening it and selecting there. Component 3's `→` buttons have
the same limitation and could reuse this.

**28. A demo builder that re-runs leaves dead entries in `ScriptGraphAssetsRepository.asset`.** `CreateTree`
replaces the asset, but the guard graphs its previous run registered stay in the repository as
`{fileID: 0}` rows — eight of them after three rebuilds, all of which would have been committed. Revert that
asset and rebuild once before committing a generated demo.

**29. Playing a demo dirties its tree asset with `executionIndex` churn.** Every guard's `ScriptGraphOutput`
has its `executionIndex` written back into the asset's `_json` at runtime — `0` becomes `22117` and stays
there. It is a runtime counter, not authored content, so `git checkout` the tree asset after running a demo
rather than committing the diff. It reappears on every play session, so it will keep turning up in
`git status` and is not a sign anything is wrong.

**30. Two naming corrections, both worth the churn while only one recording format exists.** The dump wrote
the variable's store as `"scope"`, which is too generic and does not match the C# field; it is now
`"variableKind"`, matching Unity's enum and `BehaviorTreeEvent.VariableKind`. It could not simply be `"kind"`
because the same JSON object already uses that for the *event* kind. The importer reads both, so recordings
exported in the interim still group correctly. Separately, `AgentFactPublisher` became the
`AgentVariableWriter` **component**: a base class spends the one inheritance slot a sensor usually needs for
something else, and attribution now comes from the caller (`Write(this, key, value)`) rather than from the
writer's own type, so several components can share one writer and stay individually named.


## Component 5: Guard lane + oscillation detection

- Logic-analyzer strip per guard on the active path: boolean over time (from GuardEval transitions).
- Flickering guards are the classic BT bug (branch enters, aborts, re-enters every few ticks —
  "selector thrash"). Detect: N enter/abort cycles of the same node within M ticks → badge the node on
  canvas ("oscillating: 7 aborts in 40 ticks") and surface it in the multi-agent view.

## Component 6: Breakpoints

- **Node breakpoint:** pause the editor (`Debug.Break()`) when a chosen node enters / exits / aborts.
- **Variable breakpoint:** pause when a key is written, or written with a chosen value.
- **Guard-flip breakpoint:** pause when a chosen guard transitions.
- UI: right-click on node/guard/variable row → toggle; breakpoint dot rendered on canvas.
- All breakpoints keyed by guid so they survive domain reload and live on the asset-side debug config,
  not the instance.

## Component 6 — as built

**Status: done and green.** Engine in `Assets/ArcaneOnyx/BH3/Runtime/Debugging/Breakpoints/`, namespace
`ArcaneOnyx.BehaviorTree.Debugging`; UI and persistence in `Assets/ArcaneOnyx/BH3/Editor/Debugging/`. Like
Components 2, 3 and 4 the engine is pure C# with no editor types, so what fires and what a hit *means* are
two separable things — and the second one is the only part that could not exist in a player build.

| Type | What it is |
|---|---|
| `BehaviorTreeBreakpoints` | The store and the matcher. `All`, `SetNode/SetGuard/SetVariable`, `Remove`, `Clear`, `SetEnabled`, `ResetHitCounts`, `GloballyEnabled`, and the `Hit` event. `Evaluate(recording, event)` is called by the recorder and nothing else. |
| `BehaviorTreeBreakpoint` | One armed breakpoint. `Kind`, `TargetGuid`, `VariableKey`, `Events`, `GuardBreakOn`, `ExpectedValue`, `Compare`, `BreakOnHit`, `Enabled`, `HitCount`, `MatchCount`, `Diagnostic`, `Label`. One per target — arming a second moment on a node edits the mask rather than adding a row. |
| `BehaviorTreeBreakpointKind` | `Node`, `Guard`, `Variable`. |
| `BehaviorTreeNodeBreakEvents` | Flags: `Enter, Exit, Aborted, Skipped`. |
| `BehaviorTreeGuardBreakOn` | `EitherWay`, `BecameTrue`, `BecameFalse`. |
| `BehaviorTreeVariableCompare` | `Changed`, `Equals`, `NotEquals`, `LessThan`, `LessOrEqual`, `GreaterThan`, `GreaterOrEqual`, `Contains`. |
| `BehaviorTreeBreakpointHit` | What fired, on which agent, at which tick, carrying the whole causing event. `SubjectGuid` is the node to select — **not** `Cause.NodeGuid`, see below. |
| `BehaviorTreeBreakpointStore` (Editor) | Persistence and the API the UI arms through, so saving cannot be forgotten at a call site. `OverridePath` exists so the suite cannot overwrite the developer's own file. |
| `BehaviorTreeBreakpointResponder` (Editor) | What a hit does: pause, log, select, scrub, ring the node. |
| `BehaviorTreeBreakpointGizmos` (Editor) | The armed dot and the stopped-on ring, drawn procedurally. |
| `BehaviorTreeBreakpointsPanel` (Editor) | Sidebar list: enable, hit counts, jump-to-node, delete, Clear All, and the only place to type a variable name. |

**One hook, and it is one that already existed.** The plan was four choke points mirroring Component 1's.
It turned out to need one: every event the recorder keeps already funnels through
`BehaviorTreeFlightRecorder.Add`, and by the time it arrives it carries everything a breakpoint matches on —
kind, call site, subject guid, a guard's new answer, a variable's name and new value. **`BehaviorTreeNode`
is untouched by this component.** The functional diff in the recorder is three lines.

The free win is guard filtering. `GuardEval` is only reached when a guard's answer *changed*, so a
guard breakpoint inherits transition semantics without asking for them — and the naive alternative, matching
every evaluation, would pause the editor on the frame you armed it and on every frame after.

**The consequence is real and is stated on screen, not just here: breakpoints fire only while the agent is
recording.** Turning a recorder off, or `BehaviorTreeFlightRecorders.GloballyEnabled` off, turns breakpoints
off with it. That is the right trade rather than an oversight — everything the editor does on a hit reads the
recording — but it is exactly the kind of silent nothing-happens that wastes an afternoon, so the panel says
so in a warning box when it applies, and a test pins it.

**Keyed by node guid alone — the one place the debugger deliberately breaks its own addressing rule.**
Finding 1 says a guid is not unique within an agent and everything must be addressed as
`(CallSiteId, NodeGuid)`. A breakpoint is the exception, on purpose and in two ways that agree: the spec puts
it on the asset-side config rather than the instance, and right-clicking a node in Attack is a statement
about *that node*, not about the copy of Attack that Combat happens to be running. So one breakpoint fires at
every call site, and the hit reports which one it was in. `ABreakpointFiresAtEveryCallSiteOfASharedBranch`
pins both halves.

**A hit selects `SubjectGuid`, not the event's subject.** A `VariableWrite` leaves `NodeGuid` empty and puts
the writer in `RelatedGuid`, so selecting the subject naively would select nothing on precisely the
breakpoint whose whole purpose is "who wrote this".

**Variable breakpoints compare the live value, not its rendering.** `BehaviorTreeVariableCompare` offers the
operator set a code debugger offers, and getting `ammo < 5` to work needed the typed object rather than
`Describe`'s string. It was already there for free: the value reaches `GameplayNode.SaveVariable` typed as
`object`, so it is boxed either way, and it is threaded through `Add` as one more optional parameter to the
matcher — **the event ring still only ever holds the string**, which is the rule that keeps a recording
honest about what a value was at write time. The single choke point survives.

That fixed two bugs that shipped in the first cut, both of which failed the same silent way. `bool.ToString()`
is `"True"`, so a designer typing `true` armed a breakpoint that could never fire; and `float.ToString()`
follows the machine's culture, so `3.5` never matched on any machine that renders it `3,5`. Numbers now
compare numerically and booleans parse case-insensitively; the expected text is parsed invariant-first and
then in the editor's culture, because the watch panel renders in the current culture and that is what
someone copies from.

**Ordering operators are numeric only, and say so.** `<` on a `Vector3` or a `GameObject` has no meaning.
Falling back to lexicographic order is defensible — it is what `CompareOrdinal` does — and it was rejected:
"Zombie < Skeleton" is a confident answer to a question nobody asked. Instead a non-numeric value sets
`Diagnostic` ("`target` held Zombie, which is not a number") which the panel shows on the row, and the
variable-watch menu greys the ordering entries out when the value in front of you is not a number. **A
breakpoint that cannot fire must not look like a breakpoint the program never reached** — that is the same
standard the rest of this page holds the debugger to, applied to the debugger itself.

**`BreakOnHit` is the other half, and applies to every kind.** Break on the Nth match and after, with
`MatchCount` counted separately from `HitCount` so the panel can show "0/12" rather than a bare "0" that
looks like a breakpoint that does not work. This is the answer to the oscillating guard — the classic bug
this whole debugger was specified to catch — where the interesting occurrence is the thirtieth and pressing
Play twenty-nine times is the alternative.

**No Unreal precedent for any of this.** Blueprint breakpoints are unconditional; the accepted workaround is
a Branch node with the breakpoint on its true pin, putting the condition in the asset rather than in the
debugger. Watch values show a variable at a breakpoint but never trigger one. So the model followed here is a
code debugger's, which is also where hit-count conditions come from.

**Breakpoints are armed on the asset and filtered to one agent.** Those are two separate decisions and only
the first was in the spec. A node breakpoint keyed by guid fires for every agent running that tree, so with
forty zombies it stops on whichever one gets there first — rarely the one being debugged. Unreal has the
same split and resolves it the same way: a Blueprint breakpoint lives on the asset, and the editor carries a
debug-object filter narrowing it to one instance.

**The filter is not a control.** It is pushed from `BehaviorTreeDebugTarget` — the same resolution the
ghosted canvas, the why-inspector and the variable watch already share — by a throttled
`EditorApplication.update` in the responder. Pushed rather than pulled because a breakpoint fires while the
tree ticks and nothing guarantees a panel was drawn that frame; a filter updated on repaint would be stale
exactly when it is read. A picker was considered and rejected for the reason the shared resolution exists at
all: you would be able to select one agent in the inspector and watch another in the debugger, and the
failure would be at its worst here — the editor stops, and every panel describes a different agent than the
one that stopped it. Null (nothing resolved) fires for any agent, because filtering to nothing would make
breakpoints silently stop working whenever no tree happened to be open. The panel says which agent it is
watching so a breakpoint that did not fire is never a mystery.

**Breakpoints cannot fire while the editor is paused**, which is what makes scrubbing safe. Not by a guard:
structurally, they are evaluated inside the recorder, the recorder is only written to while the tree ticks,
and ticks come from `Update`, which Unity does not run while paused. Stepping one frame does tick, and can
hit — correctly. `ReadingARecordingBackNeverFiresABreakpoint` pins the load-bearing half of that, since a
future scrubbing path that re-entered the recorder would break it silently and the symptom would be an editor
that pauses while you are studying why it paused.

**Where they live: `UserSettings/BH3Breakpoints.json`, per developer, gitignored.** This follows Unreal
rather than merely resembling it. Blueprint breakpoints lived on the `UBlueprint` asset through UE 5.0; in
5.1 Epic moved them to per-user project settings keyed by asset plus node `FGuid`, because a breakpoint on a
shared asset dirties a file everyone owns and pauses editors belonging to people who did not set it. Unity's
analogue of `EditorPerProjectUserSettings` is the `UserSettings/` folder, already gitignored at the repo
root. Nobody commits their code editor's breakpoints; sharing one means copying the file, deliberately.
There are no Export/Import buttons for that reason — the file *is* the export.

**A hit does four things beyond pausing**, because pausing alone tells you that something happened and
nothing about what: one log line naming the breakpoint, the node selected on whichever open canvas has it,
the timeline parked on the hit tick, and an amber ring on the node. The scrub is what does the real work —
the why-inspector and the variable watch both answer for the playhead, so moving it once makes all three
describe the same moment. `Debug.Break()` pauses at end of frame rather than at the call, so the responder
takes only the *first* hit per pause; without that the last breakpoint to fire would decide what you were
looking at.

**Verified by** `BehaviorTreeBreakpointTests` (edit mode, 46 tests), almost all driven through a real
`BehaviorTreeFlightRecorder` with real nodes rather than hand-built events — including a real guarded abort,
the transition-dedup property, the two-call-site case, every comparison operator, a culture test that sets
`de-DE` and asserts a comma-decimal expected value still matches, the non-numeric diagnostic, break-on-Nth,
the replay-is-a-read rule, and persistence round trips covering every kind plus a deliberately corrupt entry
and a version-1 file. Suite is 326 EditMode tests, 325 passing: the known TPS failure and nothing else.
PlayMode is unchanged at 7/4 with its three known TPS failures. Demo in
`Assets/ArcaneOnyx/BH3Demos/Breakpoints/` — see its README.

**Not covered by tests, and why:** the responder, the gizmos and the two panels. Pausing an editor,
drawing a disc and populating a `GenericMenu` are not assertable without a running editor, and the parts that
*are* — matching, arming, persistence — are behind them and fully covered. The responder is deliberately
thin for that reason.

### Findings from building Component 6

**31. The namespace collision family is wider again, and this one bites from inside BH3.** BREAK-1 lists five
port types; Finding 26 adds `Sequence`, `SetVariable` and `GetVariable` from outside BH3's namespace.
`IGraphContext` is ambiguous between `ArcaneOnyx.GraphCore` and `Unity.VisualScripting` **inside**
`ArcaneOnyx.BehaviorTree` as well, because it is a GraphCore type rather than a BH3 one — so BH3's namespace
winning does not help. Any editor file with both usings has to write `GraphCore.IGraphContext`, which is what
the existing panels already do.

**32. `RunBehaviorTreeGraphNode.SetFlightRecorder` instantiates the sub-tree asset.** It reads
`BehaviorTreeGraphAssetInstance` to reach the branch's nodes, and that getter `Object.Instantiate`s on first
access — the exact thing the authoring rules say never to touch while inspecting. Harmless in a scene, but on
a bare run node with no asset assigned it throws `ArgumentException: The Object you want to instantiate is
null`. A test does not need it anyway: the recorder does not require a node to hold a reference back to it,
and `NodeEnter` reads the plain serialized asset rather than the instance.

**33. Canvas decoration belongs in `DrawOverlay`, never in `DrawForeground`.** This was learned twice, the
second time from a bug report. There are **three** foreground draw paths on this canvas, not one:
`BehaviorTreeNodeElementWidget`, and then `ConditionalExecutionWidget` and
`RunBehaviorTreeNodeElementWidget`, both of which replace the whole body and never call base. So a dot added
to the foreground appeared on ordinary nodes, had to be copy-pasted into the guard widget, and **silently
never appeared on a sub-tree node at all**. Worse, guards take their owner's `zIndex` plus one, so even where
the dot did draw it landed *underneath* the conditional executions stacked above it.

GraphCore runs `DrawWidgetsOverlay` after **every** widget's foreground. One `DrawOverlay` override on the
base widget covers all three paths, draws above every guard, needs nothing repeated in a subclass, and is
strictly less code than the version it replaced. Any future canvas marker goes there.

**34. The dot is not ghosted while scrubbing, and that is a rule rather than an oversight.** Everything else
on the canvas dims when the playhead is parked in the past, because it is showing history. A breakpoint is
armed *now*; fading it would make it a claim about the recording instead of about the tree.

**35. The demo's most useful breakpoints are the two that are wrong.** `stance > 5` and
`alertLevel contains 1` are armed on purpose, because what decides whether a debugger is trusted is not
whether its happy path works but what it does when asked something that cannot be answered. They also cover
the two *shapes* of failure, which are not equally dangerous: ordering on a string cannot match and is at
least quiet, while `contains` on a number matches the rendered text and therefore fires on 10 and 11 as well
as 1. **A breakpoint that fires more often than expected is much harder to notice than one that never
fires**, so that one gets the warning even though it behaves exactly as defined. They live on their own
variables because the store holds one breakpoint per key.

**36. Comparing rendered values instead of real ones fails silently and in the designer's favour twice.**
`bool.ToString()` is `"True"` and `float.ToString()` is culture-dependent, so `true` and `3.5` were both
breakpoints that could never fire on a value that plainly matched. Neither would have been found by reading
the code — the string compare is obviously correct until you ask what produced the string. The general rule:
**when a debugger compares, it should compare the thing, not the thing's rendering**, and the moment of the
write is the only place the thing still exists.

## Component 7: Multi-agent triage + recording export

- Overview panel: every `BehaviorTreeMachine` in play mode — agent name, current top-level branch, ticks
  in that branch, oscillation badges. Sort/filter. Click → select agent, open its live view. This is how
  "40 zombies are idle when they should chase" gets triaged without clicking 40 zombies.
- Export: dump an agent's ring buffer to JSON (guids match `bt_describe_tree`, so a recording + a tree
  dump is a complete offline bug report). CLI: `bt_debug_dump --agent <name>`; import back into the
  scrubber. This also lets authoring agents (AI tooling) read replays and diagnose trees they generated —
  the same honesty loop as `bt_verify`, extended to runtime.

## Performance constraints

- Recording on: target < 0.05 ms/agent/tick at typical tree sizes; no per-tick allocations after warmup
  (pre-allocated ring buffer, pooled event structs, no string formatting until display/export time).
- Recording off: unmeasurable.

## Findings that change the rest of this spec

Discovered while building Component 1. The first two contradict the text above; the rest is context that
will save the next person a day.

**1. A node guid is not unique within one agent.** `RunBehaviorTreeGraphNode` does
`Object.Instantiate(asset)` per call site and `guid` is `[Serialize]`d, so the clone keeps the *original*
guids. Two call sites running the same shared branch emit events whose node guids are identical — address
by guid alone and one Attack appears to be in two states at once. **Address as `(CallSiteId, NodeGuid)`.** The
variable scope chain is already exactly one scope per call site, so the recorder numbers those; the
`CallSites` table gives you the parent link to render "Attack, under Combat".

**2. Ticks are not enough; order within the tick is the causal claim.** "The guard flipped, then the node
aborted" cannot be expressed by a tick number, and both happen on the same tick. Every event carries
`Sequence`, reset each `BeginTick`. This is also what will make spec 02's load-bearing rule — services tick
before guards — checkable rather than merely documented; `ServiceTick` is already in the enum so the
schema will not shift when services land.

**3. Aborted and skipped are different events, and getting this wrong is easy.** A composite ticks a child
on the *same frame* it declined to enter it, so `OnUpdateInternal` sees a false guard both for nodes being
killed mid-run and for nodes that never started. The first build conflated them and reported every declined
branch as interrupted. Only a node where `IsRunning` holds can abort; the decline was already recorded by
`OnNodeEnter` as `NodeSkipped`. The why-inspector (Component 3) must word these differently — "never
entered: its guard was false" versus "aborted at tick N".

**3b. A node can carry N guards, and the loop short-circuits — so a recording is deliberately incomplete
about them.** Guards are ANDed by iterating `conditionalExecutions` and returning on the first false. Each
guard gets its own `GuardEval` stream, keyed by `(callSite, guardGuid)`, so N guards stay independently
readable. But three consequences bind Components 3 and 5:

- **Guards after the failing one are not evaluated that tick**, so they emit nothing and their last recorded
  value goes stale. A guard lane must render this as *not evaluated*, never as "still true". Showing a stale
  value as current is the one way this feature can actively mislead someone.
- **Only the first false guard is blamed.** If two go false on the same tick, `NodeAborted` names one and is
  silent about the other. The why-inspector should say "guard X was false" rather than implying it was the
  only reason.
- **"First" means `graph.Nodes` order, not canvas order.** `AddConditionalExecutionNodes` walks the node
  collection, so the guard that gets blamed is not the one a designer would predict from looking at the
  tree. If that turns out to confuse people, sorting the guard list by canvas X at `OnAwake` would make
  blame predictable — a runtime change, not a recorder one, and it would change execution order, so it
  needs its own decision.

**4. Variable writes from inside Visual Scripting graphs cannot be intercepted.** *(Still true. The variable
watch therefore shows only what the recorder can see, and Component 4 changed nothing here.)* Unity's
`VariableDeclarations.OnVariableChanged` is `internal` and carries no name, old or new value. BT-side
writes (`GameplayNode.SaveVariable`) are recorded with full attribution; a write made by a `SetVariable`
unit inside an embedded script graph is invisible. Options if this becomes painful: per-tick diff of the
scopes on the active path (complete, no attribution, costs a walk per agent per tick) or forking
`com.unity.visualscripting`. Neither was worth it yet.

**5. Facts come from sensors, not branches — so writer attribution needs a name, not a guid.** Per spec 02,
`lastKnownTargetPos` and friends are produced by always-on MonoBehaviour sensors outside the tree. Without a
seam for them, the flagship why-inspector sentence has no answer in the commonest case.
`recorder.ExternalVariableWrite(writerName, key, old, new)` exists for this and **nothing calls it yet** —
wiring `[ProvidesFact]` sensors to it is a natural part of spec 02. *(Since resolved: `AgentVariableWriter`
is the component that calls it.)*

**6. `BehaviorTreeGraphAsset.name` can be empty.** A tree built at runtime has no asset name, so the export
shows `"tree": ""`. Harmless for real assets; do not assume the field is populated.

**7. Memory.** ~100 bytes per event, default 2048 events, so roughly 200 KB per recorded agent, allocated
once. Default is on in the editor for every agent — for a 200-agent scene use
`BehaviorTreeFlightRecorders.GloballyEnabled` or lower `DefaultCapacity` before agents spawn. Existing rings
are never resized, which is what keeps recording allocation-free.

## Where to pick this up

Component 3 is finished and green. The two threads in
[Open, designed but deliberately not built](#a-inspecting-object-and-collection-values-in-a-trace) are the
natural next work on it and are blocked only on the decisions named there — neither needs new
investigation. **A (collection values) is the higher value of the two**: it removes a ceiling that Unity's
own renderer does not lift, and it is the difference between seeing that a guard evaluated a list and
seeing what was in it.

Everything else on this page is a different component.

**Component 4 (variable watch) is done** — see [Component 4 — as built](#component-4--as-built). It went to
the right sidebar rather than the bottom dock in the end: the dock shows one tab at a time, so a variable
table docked beside the timeline would hide the scrubber it jumps. Two things it leaves for whoever is next.
`BehaviorTreeDebugSession` is the seam any further panel should read rather than resolving its own agent, and
the event schema now carries `VariableKind`, so spec 09's `NodeTakenOver` and per-guard counters can be added
the same way — one field, one dump property, one import fallback.

**Component 5 (guard lane) is closer still.** `BehaviorTreeTimeline` already reads `GuardEval` transitions,
and `BehaviorTreeTreeState.GuardResult(scope, guard)` already answers "was this guard true at tick N". A
guard lane is another row type in the panel rather than new engine work, and the explainer already detects
oscillation.

**Component 6 (breakpoints) is done** — see [Component 6 — as built](#component-6--as-built). Two things it
leaves for whoever is next. `BehaviorTreeBreakpoints.Evaluate` is the seam for any new breakpoint kind: add a
case to `MatchOf` and it inherits the hot-path guard, the global switch and the hit plumbing, with no new
call site anywhere. And when Component 5's guard lane lands, a right-click on a guard row should arm a guard
breakpoint the way a variable-watch row already arms a variable one — `BehaviorTreeBreakpointStore.SetGuard`
is already the whole call.

## Suggested build order

1. ~~Flight recorder (everything reads from it)~~ — **done**
2. ~~Why inspector (biggest value per effort)~~ — **done**
3. ~~Timeline scrubber~~ — **done**
4. ~~Variable watch~~ — **done**. The events needed one more field (`VariableKind`); everything else was
   already in the buffer, and `RequestScrub` was the jump-to-the-write hook as predicted
5. Guard lane + oscillation badges — `GuardResult` and the segment model already exist; this is a row type
6. ~~Breakpoints~~ — **done**. Needed one hook, not four: every event already funnels through
   `BehaviorTreeFlightRecorder.Add` carrying everything a breakpoint matches on, and guard transition
   filtering came free with it
7. Multi-agent view + export — import/export already exist, `bt_debug_dump` does not

## Working notes for the next agent

- **Work in the main checkout**, `C:\Users\Admin\Documents\UnityGit\bh3-development`. Worktrees under
  `.claude/worktrees/` have *empty* submodule directories, so `Assets/ArcaneOnyx/BH3` has no source there
  and Unity cannot compile it.
- **Four repos are in play.** The outer project, plus submodules `BH3`, `GraphCore` and `UnityExtensions`.
  Component 1 touched all four; each has a `runtime-debugger-flight-recorder` branch. Changing a lifecycle
  hook usually means a GraphCore commit, and the dump writers live in UnityExtensions.
- **The Unity CLI pipeline server does not start while the project has compile errors.** `Library/Pipeline/`
  stays empty and `unity command` finds nothing, which reads like a broken CLI but is a red project. Fix
  compilation first.
- **Running tests.** With the Editor open: `unity command run_tests --mode EditMode --filter ...`; PlayMode
  needs `--async_tests` then poll `test_status`, because entering play mode triggers a domain reload that
  drops the HTTP request. With the Editor closed, `-batchmode -runTests` works and exit code 2 means tests
  ran and some failed.
- **One test fails for reasons unrelated to the debugger**, so do not chase it:
  `TpsArchitectureTests.All_concrete_PositionEvaluators_are_marked_Serializable` (two TPS evaluators missing
  `[System.Serializable]`). `DecoratorNodeTests.Cooldown_RunsChildOnFirstActivation` used to fail alongside
  it and now passes — the node audit landed the fix. On `main` after the Component 2 merge the suite is 258
  tests, 257 passing: that one and nothing else.
- **`BehaviorTreeVerificationTests.SetValueUsesAnInlineValueWhenThePortDeclaresOne` is live again.** It had
  been commented out, then un-commented against `PlayAnimationAndWait`, which no longer exists — so the test
  assembly stopped building. Retargeted to `SetAnimatorTrigger`, whose `TriggerName` is likewise declared
  with a default, which is the property the test asserts.
- **The Unity CLI `run_tests` and `recompile` commands report a timeout and then finish anyway.** Poll
  `test_status` / `recompile_status` afterwards rather than believing the error.
- **Component 3 touched two repos**, BH3 and UnityExtensions (the JSON reader). GraphCore was not needed —
  the sidebar panel registers from BH3's own graph context.
- **Component 2 touched two repos**, BH3 and GraphCore (the bottom dock). UnityExtensions was not needed.
  Each is on a `runtime-debugger-timeline-scrubber` branch.
- **Component 4 touched three**: BH3, GraphCore (the preferred sidebar anchor) and the superproject (the
  demo). Each is on a `runtime-debugger-variable-watch` branch. The superproject commit also carries
  `Assets/BehaviorTree.Generated/ScriptGraphAssetsRepository.asset`, which the demo's guard graphs register
  into — see Finding 28 before rebuilding a demo.
- **Component 6 touched two**: BH3 and the superproject (the demo). **GraphCore needed no change** — the
  sidebar panel registers from BH3's own graph context and the stopped-on ring reuses
  `GraphDrawer.DrawSelectionBox`, which was already public. Each is on a `runtime-debugger-breakpoints`
  branch, **based on `runtime-debugger-variable-watch` rather than on `main`**, because the variable-watch
  row is where a variable breakpoint is armed and `BehaviorTreeDebugSession` is what the responder checks
  before moving the playhead. So Component 4's PRs have to merge first. The superproject commit again carries
  two new rows in `ScriptGraphAssetsRepository.asset` for the demo's guard graphs — real entries, not
  Finding 28's dead ones, because the builder was only run once.
- **`UserSettings/BH3Breakpoints.json` is gitignored and must stay that way.** If it ever shows up in
  `git status`, something has moved the file rather than something being wrong with the ignore rule.
- **The `unity command` CLI takes `--key value`, not `--params '{...}'`.** The `--params` form is silently
  accepted and dropped, so `menu` lists every menu item instead of running one and `eval` reports `code`
  missing. Correct: `unity command menu --path "Tools/BH3/Breakpoints/Build demo scene"`.
- **`capture_game_view` and `screenshot` both read a camera's target, so they do not see IMGUI or a Screen
  Space - Overlay canvas.** The breakpoints demo's HUD is Screen Space - Camera for exactly this reason; an
  `OnGUI` version photographed as an empty skybox and looked like a HUD that had failed to run.
- **PlayMode has three pre-existing failures nobody has written down**, all
  `TacticalPositionSelectionPlayModeTests`, all a `NullReferenceException` in `GameEntity.Awake`. Unrelated to
  the debugger; do not chase them, and do not read a red PlayMode run as your own doing.
- **Recording is per agent and starts at tick 0 on spawn**, so a `bt_verify`-clean tree that never ticks
  produces an empty timeline rather than an error. If the panel says "Nothing recorded yet" while play mode
  is running, check for a `Repeater` under `Entry` before suspecting the scrubber (Finding 11).

## Acceptance criteria

Status after Component 2: **1 is met apart from the badge and lane**, which are Component 5's rendering of a
signal the engine already produces — the demo reproduces the flickering guard, the explanation names the
writer, and the timeline shows the oscillation as a run of short bars punctuated by red pins. **2 is met**:
scrubbing to any tick renders the historically-correct active path, ghosted, and
`StateAgreesWithTheTimelineAtEveryChangeTick` pins that the bars and the canvas cannot disagree. **3 is met**:
`ARoundTrippedRecordingScrubsIdentically` exports a real recording, re-imports it, and asserts identical
segments and change ticks. **5 is met** — everything is addressed as `(CallSiteId, NodeGuid)`. **4 is untouched**
and belongs to Component 1; nobody has profiled a 200-agent scene.

1. Reproduce the classic bug: a guard reading a variable that flickers → the debugger shows the
   oscillation badge, the guard lane shows the flicker, the why-inspector names the writer node.
2. Scrubbing to any tick in the buffer renders the historically-correct active path for that tick.
3. A recording exported to JSON and re-imported reproduces the same scrubber view.
4. With recording disabled, profiler shows no measurable cost in a 200-agent scene.
5. All references guid-based: renaming/moving nodes on canvas does not break breakpoints or recordings.