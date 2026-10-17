using ArcaneOnyx.BehaviorTree;
using UnityEngine;

namespace ArcaneOnyx.BH3.Samples
{
    public static class AgentBody
    {
        public static Renderer FindRenderer(BTContext ctx) => ctx.gameObject.GetComponentInChildren<Renderer>();

        public static Transform Find(BTContext ctx)
        {
            Renderer renderer = FindRenderer(ctx);
            return renderer != null ? renderer.transform : ctx.transform;
        }
    }
}
