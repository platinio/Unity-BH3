using ArcaneOnyx.BehaviorTree.Authoring;
using Unity.VisualScripting;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ArcaneOnyx.BehaviorTree.Debugging
{
    /// <summary>
    /// Generates a small agent whose recording is worth reading, so the flight recorder can be judged on a
    /// real tree rather than on a description of one.
    ///
    /// <para>
    /// The tree is chosen to put the three things that are easy to get wrong on screen at once: the same
    /// branch asset runs at <b>two call sites</b>, so a reader can check that two identical node guids stay
    /// distinguishable; Idle carries <b>two guards</b>, so the short-circuit and its stale-value consequence
    /// are visible; and the facts both guards read are written from <b>outside the tree</b>, the way a
    /// perception sensor would write them.
    /// </para>
    ///
    /// <para>
    /// Nothing here drives the agent on a timer. Facts are flipped by hand from
    /// <see cref="FlightRecorderWindow"/>, because a demo you can poke tells you more about whether a
    /// recording answers questions than one that plays a script at you.
    /// </para>
    /// </summary>
    public static class FlightRecorderDemoBuilder
    {
        public const string HasTarget = "hasTarget";
        public const string HasNoise = "hasNoise";

        private const string Folder = "Assets/ArcaneOnyx/BH3/Sample/FlightRecorder";
        private const string EngagePath = Folder + "/FR_Demo_Engage.asset";
        private const string SentryPath = Folder + "/FR_Demo_Sentry.asset";
        private const string ScenePath = Folder + "/FlightRecorderDemo.unity";

        [MenuItem("Tools/BH3/Flight Recorder/Build demo scene", priority = 20)]
        public static void Build()
        {
            var engage = BuildEngageBranch();
            var sentry = BuildSentryTree(engage);

            BuildScene(sentry);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // Logged rather than shown in a dialog: a modal here blocks the Editor, which makes the menu item
            // unusable from the CLI and from anything else driving the editor without a person in front of it.
            Debug.Log(
                $"[FlightRecorderDemo] Built {SentryPath}, {EngagePath} and {ScenePath}.\n"
                + "Press Play, then open Tools > BH3 > Flight Recorder and toggle the agent facts to make it "
                + "change its mind.");

            EditorUtility.FocusProjectWindow();
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<Object>(SentryPath);
        }

        /// <summary>
        /// The shared branch: approach, then strike. Deliberately dull — it exists to be run twice from
        /// different places, which is the thing being demonstrated, not to be interesting itself.
        /// </summary>
        private static BehaviorTreeGraphAsset BuildEngageBranch()
        {
            var asset = BehaviorTreeAuthoring.CreateTree(EngagePath);

            var sequence = BehaviorTreeAuthoring.AddNode<Sequence>(asset, 0.0f, 160.0f);
            BehaviorTreeAuthoring.Connect(asset, asset.graph.EntryNode, sequence);

            var approach = BehaviorTreeAuthoring.AddNode<WaitTime>(asset, -160.0f, 340.0f);
            BehaviorTreeAuthoring.FeedFloat(asset, approach.Time, 0.8f, -160.0f, 520.0f);

            var strike = BehaviorTreeAuthoring.AddNode<WaitTime>(asset, 160.0f, 340.0f);
            BehaviorTreeAuthoring.FeedFloat(asset, strike.Time, 0.8f, 160.0f, 520.0f);

            BehaviorTreeAuthoring.Connect(asset, sequence, approach, 0);
            BehaviorTreeAuthoring.Connect(asset, sequence, strike, 1);

            BehaviorTreeAuthoring.AddSticky(asset, "Shared branch",
                "The Sentry tree runs this asset twice, from two different Run Behavior Tree nodes. Both copies "
                + "keep these node guids, so a recording can only tell them apart by call site.",
                -520.0f, 160.0f);

            BehaviorTreeAuthoring.Save(asset);

            return asset;
        }

        /// <summary>
        /// Entry -> Repeater -> Selector -> [ Engage(target), Engage(noise), Idle ].
        /// <para>
        /// The Repeater is what makes a switch observable: a Selector whose running child fails runs out of
        /// children and reports Failure, so something has to restart it. Child order is canvas X, so the
        /// left-most branch is the highest priority.
        /// </para>
        /// </summary>
        private static BehaviorTreeGraphAsset BuildSentryTree(BehaviorTreeGraphAsset engage)
        {
            var asset = BehaviorTreeAuthoring.CreateTree(SentryPath);

            var repeater = BehaviorTreeAuthoring.AddNode<Repeater>(asset, 0.0f, 120.0f);
            var selector = BehaviorTreeAuthoring.AddNode<Selector>(asset, 0.0f, 300.0f);

            BehaviorTreeAuthoring.Connect(asset, asset.graph.EntryNode, repeater);
            BehaviorTreeAuthoring.Connect(asset, repeater, selector);

            // Highest priority: a seen target.
            var engageTarget = BehaviorTreeAuthoring.AddSubTree(asset, engage, -900.0f, 520.0f);
            BehaviorTreeAuthoring.GuardOnVariable(asset, engageTarget, HasTarget, true, false, -900.0f, 340.0f);

            // Same asset, different call site, different precondition.
            var engageNoise = BehaviorTreeAuthoring.AddSubTree(asset, engage, 0.0f, 520.0f);
            BehaviorTreeAuthoring.GuardOnVariable(asset, engageNoise, HasNoise, true, false, 0.0f, 340.0f);

            // Two guards on one owner, ANDed. Idle stands down the moment either fact appears, and because the
            // loop returns on the first false, only one of them is ever named as the reason.
            var idle = BehaviorTreeAuthoring.AddNode<WaitTime>(asset, 900.0f, 520.0f);
            BehaviorTreeAuthoring.FeedFloat(asset, idle.Time, 999.0f, 1150.0f, 520.0f);
            BehaviorTreeAuthoring.GuardOnVariable(asset, idle, HasTarget, false, false, 700.0f, 340.0f);
            BehaviorTreeAuthoring.GuardOnVariable(asset, idle, HasNoise, false, false, 1000.0f, 340.0f);

            BehaviorTreeAuthoring.Connect(asset, selector, engageTarget, 0);
            BehaviorTreeAuthoring.Connect(asset, selector, engageNoise, 1);
            BehaviorTreeAuthoring.Connect(asset, selector, idle, 2);

            BehaviorTreeAuthoring.AddSticky(asset, "What to watch",
                "Flip hasNoise: Idle aborts and 'investigate noise' takes over.\n"
                + "Then flip hasTarget: that branch aborts and 'engage target' takes over.\n\n"
                + "Both Engage nodes run the same asset, so their inner nodes share guids. In the recording "
                + "they are told apart by call site.\n\n"
                + "Idle has two guards. Only the first one to go false is ever blamed, and the other is not "
                + "evaluated that tick at all.",
                -520.0f, 120.0f);

            BehaviorTreeAuthoring.Save(asset);

            return asset;
        }

        /// <summary>
        /// A scene with one agent. The machine reads its macro in <c>Awake</c>, and the guards read facts off
        /// the Variables component, so both have to be in place before play mode starts.
        /// </summary>
        private static void BuildScene(BehaviorTreeGraphAsset sentry)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            var agent = new GameObject("Sentry");

            var variables = agent.AddComponent<Variables>();
            variables.declarations.Set(HasTarget, false);
            variables.declarations.Set(HasNoise, false);

            var machine = agent.AddComponent<BehaviorTreeMachine>();
            machine.nest.macro = sentry;

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
        }
    }
}
