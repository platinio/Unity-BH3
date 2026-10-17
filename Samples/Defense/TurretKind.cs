using ArcaneOnyx.BehaviorTree;
using UnityEngine;

namespace ArcaneOnyx.BH3.Samples
{
    [CreateAssetMenu(menuName = "BH3 Samples/Turret Kind")]
    public sealed class TurretKind : ScriptableObject
    {
        [SerializeField] private Material bodyMaterial;
        [SerializeField] private WeaponDefinition weapon;
        [SerializeField] private BehaviorTreeGraphAsset tree;
        [SerializeField] private int cost = 50;

        public string Id => name;
        public Material BodyMaterial => bodyMaterial;
        public WeaponDefinition Weapon => weapon;
        public BehaviorTreeGraphAsset Tree => tree;
        public int Cost => cost;
    }
}
