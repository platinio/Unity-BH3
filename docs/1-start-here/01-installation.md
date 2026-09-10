# Installation

What BH3 needs, how to get it into a project, and how to check it works. Ten minutes.

---

## Requirements

| Requirement | Notes |
|---|---|
| **Unity 6** | The reference project is on `6000.4.x`. |
| **Visual Scripting** package (`com.unity.visualscripting`) | BH3 is built on it: ports, the graph canvas and Functions all come from it. The reference project uses `1.9.11`. |
| Three ArcaneOnyx modules | `GraphCore`, `VisualScriptingExtension`, `BlockVariables`. See the table below. |
| *Optional:* **Pipeline** package (`com.unity.pipeline`, `0.4.0-exp.1`) | Only for the `bt_*` and `fn_*` command-line tools. They live in their own editor assembly that compiles only when the package is present; nothing else in BH3 uses it. |

BH3 and its modules are plain folders of C# with assembly definitions. There is no UPM package: they live
under `Assets/ArcaneOnyx/` and are wired together by assembly references.

### The modules BH3 depends on

Every one of these is its own GitHub repository. The reference project pulls them in as git submodules.

| Folder under `Assets/ArcaneOnyx/` | Repository | What BH3 takes from it |
|---|---|---|
| `Modules/GraphCore` | [platinio/graph-core-library](https://github.com/platinio/graph-core-library) | The graph window, canvas, port system and Blackboard panel that BH3's editor builds on |
| `Modules/VisualScriptingExtension` | [platinio/visual-scripting-extension](https://github.com/platinio/visual-scripting-extension) | **Functions**: named, reusable Visual Scripting graphs with a contract |
| `Modules/BlockVariables` | [platinio/Unity-BlockVariables](https://github.com/platinio/Unity-BlockVariables) | Variable storage used by the Blackboard |

**Optional.** Install [Tactical Position Selection](https://github.com/platinio/Unity-TacticalPositionSelection)
(plus its own dependencies `AIEntities` and `ScriptableObjectDatabase`) and BH3 adds a
**Tactical Position Selection** node and a **TPS Query** literal. Those live in a separate assembly that only
compiles when the `MODULE_TACTICAL_POSITION_SELECTION_EXIST` define is present, so a project without the
module never sees them.

---

## Option A: clone the reference project

The fastest way to get a working setup, with every demo:

```bash
git clone --recurse-submodules https://github.com/platinio/bh3-development.git
```

Open it with Unity 6 and skip to [Initialise Visual Scripting](#initialise-visual-scripting).

## Option B: add BH3 to your own project

1. Put the four repositories under `Assets/ArcaneOnyx/`, keeping the folder names above:

   ```
   Assets/ArcaneOnyx/
     BH3/                              ← this repository
     Modules/GraphCore/
     Modules/VisualScriptingExtension/
     Modules/BlockVariables/
   ```

   As submodules, from your project root:

   ```bash
   git submodule add https://github.com/platinio/Unity-BH3.git Assets/ArcaneOnyx/BH3
   git submodule add https://github.com/platinio/graph-core-library.git Assets/ArcaneOnyx/Modules/GraphCore
   git submodule add https://github.com/platinio/visual-scripting-extension.git Assets/ArcaneOnyx/Modules/VisualScriptingExtension
   git submodule add https://github.com/platinio/Unity-BlockVariables.git Assets/ArcaneOnyx/Modules/BlockVariables
   ```

2. Add Visual Scripting through **Window → Package Manager** or directly in `Packages/manifest.json`:

   ```json
   "com.unity.visualscripting": "1.9.11"
   ```

   Add `"com.unity.pipeline": "0.4.0-exp.1"` beside it only if you want the
   [command-line tools](../4-extending-with-csharp/05-command-line-tools.md).

3. Let Unity compile. BH3 registers a `MODULE_BH3_EXIST` scripting define on first load so other modules can
   detect it; expect one extra recompile.

> **Installing from a `.unitypackage` or zip instead?** The archive ships a `ProjectSettings` folder. Close
> Unity, copy `ProjectSettings.asset`, `TagManager.asset` and `VisualScriptingSettings.asset` over your
> project's, then continue below. That flow is also described in `Installation Guide.pdf` at the root of this
> repository.

---

## Initialise Visual Scripting

BH3's nodes and its **Set BT Variable** / **Get BT Variable** units only appear once Visual Scripting has
built its node library. One menu item does all of it:

**Tools → BH3 → Install**

It initialises Visual Scripting if the project has never used it, adds BH3's assemblies and types to the
node library and type options (keeping whatever the project already had), regenerates the nodes, and
generates the custom inspector properties. A dialog lists what it did. Running it again is harmless.

The same thing by hand, if you prefer to see each step:

1. **Edit → Project Settings → Visual Scripting**
2. Click **Initialize Visual Scripting** (only offered on a project that has never used it)
3. Under **Node Library**, add `ArcaneOnyx.BehaviorTree` and `ArcaneOnyx.GraphCore`; under **Type
   Options**, add `GuardTrigger`
4. Under **Node Library**, click **Regenerate Nodes** and wait
5. Under **Custom Inspector Properties**, click **Generate** and wait

Run **Install** (or step 4) again whenever a custom Visual Scripting **unit** does not show up in a script
graph's finder. Behavior tree nodes are different: a C# node appears in the canvas right-click menu as soon
as it compiles.

---

## Check it works

- Right-click in the Project window. **Create → Visual Scripting → Behavior Tree** should be there.
- Add a component to any GameObject: **Behavior Tree Machine** should be offered.
- In the reference project, open `Assets/ArcaneOnyx/BH3Demos/TimelineScrubber/TimelineScrubberDemo.unity`,
  double-click `Trees/TimelineDemo_Agent.asset` in the same folder, and press Play. The canvas should light up
  as the tree runs.

If the create menu is missing, Visual Scripting has not been initialised. If the nodes exist but the
**Set BT Variable** unit cannot be found in a script graph, run **Tools → BH3 → Install** again.

---

## Player builds

Everything in BH3 runs in a build. The debugging *recorder* is compiled out of ordinary builds and comes back
in a development build when you add the `BH3_DEV_TOOLS` scripting define. See
[Debugging: overview](../3-debugging/01-overview.md).

---

## Next

- [Your first tree](02-your-first-tree.md)
- [Core concepts](03-core-concepts.md)
