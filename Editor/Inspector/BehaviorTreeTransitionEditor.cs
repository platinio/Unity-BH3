using Unity.VisualScripting;
using UnityEngine;

namespace ArcaneOnyx.BehaviorTree
{
    [Editor(typeof(BehaviorTreeTransition))]
    public class BehaviorTreeTransitionEditor : Inspector
    {
        public BehaviorTreeTransitionEditor(Metadata metadata) : base(metadata) { }

        protected override float GetHeight(float width, GUIContent label) => 0;

        protected override void OnGUI(Rect position, GUIContent label) { }
    }
}