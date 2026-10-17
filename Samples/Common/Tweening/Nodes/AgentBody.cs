using ArcaneOnyx.BehaviorTree;
using UnityEngine;

namespace ArcaneOnyx.BH3.Samples
{
    public static class AgentBody
    {
        public static Renderer FindRenderer(BTContext ctx) => FindRenderer(ctx.gameObject);

        public static Renderer FindRenderer(GameObject agent) => agent.GetComponentInChildren<Renderer>();

        public static Transform Find(BTContext ctx) => Find(ctx.gameObject);

        public static Transform Find(GameObject agent)
        {
            Renderer renderer = FindRenderer(agent);
            return renderer != null ? renderer.transform : agent.transform;
        }
    }
}
