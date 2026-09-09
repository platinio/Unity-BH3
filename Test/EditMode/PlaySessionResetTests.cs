using System;
using ArcaneOnyx.BehaviorTree.Debugging;
using NUnit.Framework;

namespace ArcaneOnyx.BehaviorTree.Tests
{
    /// <summary>
    /// What the runtime debugging statics look like at the start of a play session when nothing reloaded
    /// the domain. The editor already resets its own statics at the boundary; this pins the runtime side,
    /// which has to hold on its own in a project with Enter Play Mode Options turned on.
    /// </summary>
    [TestFixture]
    public class PlaySessionResetTests
    {
        private string storePath;

        [SetUp]
        public void SetUp()
        {
            storePath = BehaviorTreeBreakpointStore.OverridePath;
            BehaviorTreeBreakpointStore.OverridePath = System.IO.Path.GetTempFileName();

            BehaviorTreeFlightRecorders.Reset();
            BehaviorTreeBreakpoints.Clear();
            BehaviorTreeBreakpoints.AgentFilter = null;
        }

        [TearDown]
        public void TearDown()
        {
            BehaviorTreeFlightRecorders.Reset();
            BehaviorTreeBreakpoints.Clear();
            BehaviorTreeBreakpoints.AgentFilter = null;

            if (System.IO.File.Exists(BehaviorTreeBreakpointStore.OverridePath))
            {
                System.IO.File.Delete(BehaviorTreeBreakpointStore.OverridePath);
            }

            BehaviorTreeBreakpointStore.OverridePath = storePath;
        }

        [Test]
        public void ANewPlaySessionStartsWithNoRegisteredRecorders()
        {
            BehaviorTreeFlightRecorders.Register(new BehaviorTreeFlightRecorder("Ghost", "Tree"));
            Assert.AreEqual(1, BehaviorTreeFlightRecorders.Active.Count, "Fixture check.");

            BehaviorTreeFlightRecorders.ResetForPlaySession();

            Assert.IsEmpty(BehaviorTreeFlightRecorders.Active,
                "A recorder carried across the boundary is an agent that no longer exists sitting in the triage view.");
        }

        [Test]
        public void ANewPlaySessionKeepsTheSwitchesAlone()
        {
            BehaviorTreeFlightRecorders.GloballyEnabled = false;
            BehaviorTreeFlightRecorders.TracingGloballyEnabled = false;

            try
            {
                BehaviorTreeFlightRecorders.ResetForPlaySession();

                Assert.IsFalse(BehaviorTreeFlightRecorders.GloballyEnabled,
                    "The Rec button's choice lives in this static without a domain reload to restore it from.");
                Assert.IsFalse(BehaviorTreeFlightRecorders.TracingGloballyEnabled);
            }
            finally
            {
                BehaviorTreeFlightRecorders.GloballyEnabled = true;
                BehaviorTreeFlightRecorders.TracingGloballyEnabled = true;
            }
        }

        [Test]
        public void ANewPlaySessionDropsTheAgentFilterAndTalliesButKeepsBreakpointsArmed()
        {
            var recording = new BehaviorTreeFlightRecorder("Agent", "Tree");
            var guid = Guid.NewGuid();
            var breakpoint = BehaviorTreeBreakpoints.SetNode(guid, BehaviorTreeNodeBreakEvents.Enter);

            BehaviorTreeBreakpoints.AgentFilter = recording;
            BehaviorTreeBreakpoints.Evaluate(recording, Enter(guid));
            Assert.AreEqual(1, breakpoint.HitCount, "Fixture check: nothing to reset otherwise.");

            BehaviorTreeBreakpoints.ResetForPlaySession();

            Assert.IsNull(BehaviorTreeBreakpoints.AgentFilter,
                "The filter names last run's recording, and holding it pins that run's ring in memory.");
            Assert.AreEqual(0, breakpoint.HitCount);
            Assert.AreEqual(0, breakpoint.MatchCount);
            Assert.AreSame(breakpoint, BehaviorTreeBreakpoints.ForNode(guid),
                "Armed breakpoints outlive a play session, with or without a domain reload.");
        }

        [Test]
        public void ANewPlaySessionKeepsHitSubscribers()
        {
            var guid = Guid.NewGuid();
            var fired = 0;
            Action<BehaviorTreeBreakpointHit> handler = _ => fired++;

            BehaviorTreeBreakpoints.Hit += handler;

            try
            {
                BehaviorTreeBreakpoints.SetNode(guid, BehaviorTreeNodeBreakEvents.Enter);
                BehaviorTreeBreakpoints.ResetForPlaySession();
                BehaviorTreeBreakpoints.Evaluate(null, Enter(guid));

                Assert.AreEqual(1, fired,
                    "The responder subscribes once per domain reload; without one, this is its only subscription.");
            }
            finally
            {
                BehaviorTreeBreakpoints.Hit -= handler;
            }
        }

        private static BehaviorTreeEvent Enter(Guid node) =>
            BehaviorTreeEvent.Create(BehaviorTreeEventKind.NodeEnter, 0, 0, 0, 0.0f, 0, node);
    }
}
