using ArcaneOnyx.BehaviorTree;
using UnityEngine;

namespace ArcaneOnyx.BH3.Samples
{
    [CreateAssetMenu(menuName = "BH3 Samples/Building Kind")]
    public sealed class BuildingKind : ScriptableObject
    {
        [SerializeField] private GameObject model;
        [SerializeField] private float modelScale = 4f;
        [SerializeField] private BehaviorTreeGraphAsset tree;
        [SerializeField] private int woodCost = 100;
        [SerializeField] private int goldCost;
        [SerializeField] private float buildSeconds = 20f;
        [SerializeField] private float footprintRadius = 2f;
        [SerializeField] private UnitKind[] trains;
        [SerializeField] private Vector3 rallyOffset = new(0f, 0f, -4f);

        public string Id => name;
        public GameObject Model => model;
        public float ModelScale => modelScale;
        public BehaviorTreeGraphAsset Tree => tree;
        public int WoodCost => woodCost;
        public int GoldCost => goldCost;
        public float BuildSeconds => buildSeconds;
        public float FootprintRadius => footprintRadius;
        public UnitKind[] Trains => trains;
        public Vector3 RallyOffset => rallyOffset;
    }
}
