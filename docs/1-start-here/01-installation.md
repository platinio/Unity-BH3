# Installation

What BH3 needs, how to get it into a project, and how to check it works. Ten minutes.

---

## Requirements

| Requirement | Notes |
|---|---|
| **Unity 6** | BH3 is built and verified on `6000.4.x`. |
| **Visual Scripting** package (`com.unity.visualscripting`) | BH3 is built on it: ports, the graph canvas and Functions all come from it. `1.9.11` is the verified version. |
| *Optional:* **Pipeline** package (`com.unity.pipeline`, `0.4.0-exp.1`) | Only for the `bt_*` and `fn_*` command-line tools. They live in their own editor assembly that compiles only when the package is present; nothing else in BH3 uses it. |

BH3 is plain folders of C# with assembly definitions. There is no UPM package: everything lives under
`Assets/ArcaneOnyx/` and is wired together by assembly references.

---

## Install

BH3 ships as a single `.unitypackage` on its GitHub release page. The package is self-contained: the three
ArcaneOnyx modules BH3 is built on are bundled inside it, so there is nothing else to fetch.

1. Add Visual Scripting through **Window → Package Manager** or directly in `Packages/manifest.json`:

   ```json
   "com.unity.visualscripting": "1.9.11"
   ```

   Add `"com.unity.pipeline": "0.4.0-exp.1"` beside it only if you want the
   [command-line tools](../4-extending-with-csharp/05-command-line-tools.md).

2. Download `BH3.unitypackage` from the latest release. This link always points at the newest one:

   ```
   https://github.com/platinio/Unity-BH3/releases/latest/download/BH3.unitypackage
   ```

   A specific version is on its release page under
   [platinio/Unity-BH3/releases](https://github.com/platinio/Unity-BH3/releases).

3. Import it with **Assets → Import Package → Custom Package…**, keeping everything selected. It lands as:

   ```
   Assets/ArcaneOnyx/
     BH3/                              ← the tool
     Modules/GraphCore/                ← the graph window, canvas, port system and Blackboard panel
     Modules/VisualScriptingExtension/ ← Functions: named, reusable Visual Scripting graphs with a contract
     Modules/BlockVariables/           ← variable storage used by the Blackboard
   ```

   If a module is already in the project from another ArcaneOnyx package, Unity matches it by asset GUID and
   updates it in place rather than importing a second copy.

4. Let Unity compile. BH3 registers a `MODULE_BH3_EXIST` scripting define on first load so other modules can
   detect it; expect one extra recompile.

Upgrading is the same import over the existing folders. Each release is verified before it is published: the
package is imported into an empty Unity project, compiled, its tests run, and a player built from it.

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
- Create a tree, open it, and add a node from the canvas right-click menu. The canvas should draw it.

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
