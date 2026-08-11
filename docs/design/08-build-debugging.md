# Debugging BH3 in builds — black box, overlay, live attach, telemetry

**Status:** design spec for implementation by an AI or engineer with access to the BH3 source.
Extends spec 02 (runtime debugger) — read that first; this spec assumes its **flight recorder** exists.
Written from BH3's design documentation — verify names against the code before building.

## The scenario to solve

A designer (or playtester) is running a **standalone build**. AI entity 15 crashes or visibly misbehaves.
The designer must be able to capture that agent's timeline recording and get it into the editor debugger —
without a programmer, without a repro, ideally without even having been watching.

## Architecture principle (load-bearing)

**The build records; the editor views.** The flight recorder from spec 02 is pure data — guid-addressed
events in a ring buffer — and needs nothing from the editor. The expensive part of the debugger (canvas,
scrubber, why-inspector) stays editor-only. Everything below is just four ways of moving recorder data
from a build to that viewer. Do not rebuild the debugger UI in-game beyond the Tier 2 outline view.

**Recording portability contract:** an exported recording must be self-contained:
- Header: build id/version, tree asset ids **and a content hash per tree**, agent id/name, timestamp,
  platform, tick rate.
- Embedded `BehaviorTreeDump.ToJson` snapshot of each involved tree (root + sub-trees), so the editor can
  render the timeline even if local assets have drifted since the build. On hash mismatch with local
  assets, the editor views against the embedded dump and shows a "recorded against build X" banner.
- Event stream: the spec 02 ring buffer serialized (binary in the container for size; the editor importer
  also accepts the JSON form).

## Tier 1 — the black box (build this first; days of work, 80% of the value)

The recorder runs in builds as an aircraft-style flight data recorder:

- Compiled in under `DEVELOPMENT_BUILD || UNITY_EDITOR || BH3_DEBUG` — the custom `BH3_DEBUG` scripting
  define lets QA ship a release-configuration build with recording on. Default ring: last ~30–60 seconds
  per agent (configurable); this is exactly what a ring buffer is for — the moments *before* the problem
  are always in memory.
- **Auto-export on failure**, no human needed:
  - BH3 signal: a node returning `ExecutionStatus.Exception` triggers export of that agent's buffer.
  - Unity signal: `Application.logMessageReceived` filtered to `LogType.Exception` — export buffers of
    all agents (or the agents whose machines appear in the stack trace, when resolvable).
  - Optional watchdogs: the spec 02 oscillation detector, or "agent in branch X longer than Y seconds",
    can also fire an export — behavior *errors* rarely throw.
- **Manual export:** debug key / controller chord / console command (`bh3.export 15`, see Tier 2) dumps a
  chosen agent or all agents.
- Destination: `Application.persistentDataPath/BH3Recordings/` (the one path writable on every platform;
  console platforms map it to their scratch storage automatically). Filename:
  `{agent}_{sceneTime}_{reason}.bh3rec`.
- Editor side: an import entry point in the debugger window + double-click association for `.bh3rec`,
  landing directly in the scrubber with the why-inspector functional.

## Tier 2 — in-build overlay (the designer's live view on device)

A lightweight runtime panel, toggled by key/gesture, built with **UI Toolkit** (runtime UI Toolkit works
in players and reuses skills from the editor tooling; IMGUI is the fallback if the project already has a
debug console framework):

- Agent picker: list of `BehaviorTreeMachine`s (sortable by name / oscillation badge / last error), plus
  "pick by looking" — screen-center raycast to select the agent in front of the camera.
- Per-agent live view: the active path as an indented **text outline** (not a canvas — an outline is
  cheap, readable on a TV, and sufficient in-build), current guard values, variable watch table,
  ticks-in-current-branch.
- Buttons: export recording (Tier 1), pause this agent's machine, tick-step it.
- A tiny command console (`bh3.list`, `bh3.watch 15`, `bh3.export 15`, `bh3.exportall`) so the overlay is
  scriptable and usable over platform remote-input tools.
- Budget: near-zero when hidden; no allocations per frame while visible beyond string building for
  visible rows.

## Tier 3 — live attach from the editor (the AAA trick, and Unity makes it nearly free)

The full editor debugger — canvas, scrubber, why-inspector — attached to a **running build**, like the
Unity Profiler attaches to a device.

- **The easy Unity way: `PlayerConnection` / `EditorConnection`**
  (`UnityEngine.Networking.PlayerConnection` in the player, `UnityEditor.Networking.PlayerConnection.
  EditorConnection` in the editor). This is the same built-in transport the Profiler and device console
  use: development builds **auto-advertise and auto-discover** over USB and local network, the editor
  shows them in the same connection dropdown the Profiler uses, and the API is small — register a
  message `System.Guid`, `Send(guid, byte[])`, receive callbacks on the other side. No sockets, no
  discovery code, no firewall dance for the common case. This API is the single most underused gem for
  tool builders; most teams never learn it exists.
- Protocol: recorder already produces delta events — stream them as they're written (batched per frame,
  compact binary). Editor-side, the stream feeds the same ingestion path as an imported recording, so the
  scrubber/why-inspector work identically live and offline. Add two control messages: subscribe(agentId)
  and request-full-sync (tree hashes + current state to join mid-session).
- Bandwidth is trivial (BT events are tens of bytes; even 200 agents is nothing next to the Profiler).
- Limitation to document: PlayerConnection is development-build only. For release-config QA builds
  (`BH3_DEBUG` without dev build), fall back to a plain TCP/WebSocket listener behind the same streaming
  interface — one `IRecorderTransport` abstraction, two implementations. The WebSocket variant also
  enables a future browser-based viewer for anyone on the LAN, no editor install — nice-to-have, not in
  scope.

## Tier 4 — telemetry at scale (unattended playtests)

Nobody is watching entity 15 during a 30-person playtest. Close the loop without a human:

- On auto-export (Tier 1 triggers), also **upload** the `.bh3rec`:
  - Easy Unity way: **Unity Cloud Diagnostics** supports attaching files to crash/exception reports —
    recordings ride along with the crash they explain.
  - More control: Sentry (attachments API) or a dumb HTTPS endpoint into a bucket, tagged with build id +
    tree hashes from the recording header.
- A "report AI bug" button in the overlay for testers: captures recording + screenshot + camera position
  + free-text note, uploads as one bundle.
- Editor tooling: a browser panel in the debugger window listing uploaded recordings for the current
  build, one click to open in the scrubber. The designer's morning starts with a queue of replayable AI
  bugs instead of a channel full of "the zombie did a weird thing, can't repro."

## Build order and effort shape

1. Tier 1 (recorder already exists per spec 02 — this is serialization, triggers, and an importer).
2. Tier 3 via PlayerConnection (transport is free; mostly wiring the stream into the existing ingestion).
3. Tier 2 overlay (independent of 3; do whichever unblocks designers faster).
4. Tier 4 when playtests scale past "designer at the desk."

## Acceptance criteria

1. In a Windows development build, an agent whose node returns `ExecutionStatus.Exception` produces a
   `.bh3rec` in persistentDataPath with no user action; dragging it into the editor opens the scrubber at
   the failure tick and the why-inspector names the failing node.
2. A recording made from build version A opens correctly in an editor whose assets have since changed,
   using the embedded dump, with the mismatch banner shown.
3. Editor attaches to a running device build via the standard connection dropdown and live-follows agent
   15's canvas within one second of events occurring; detach/reattach mid-session works.
4. Overlay on a gamepad-only build: select entity 15, read its active path and guards, export, all
   without keyboard.
5. With `BH3_DEBUG` off and not a development build, the compiled player contains no recorder, overlay,
   or transport code (verify via build report / IL inspection), and per-frame cost is zero.
