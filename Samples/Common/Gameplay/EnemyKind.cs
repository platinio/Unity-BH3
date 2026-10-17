using ArcaneOnyx.BehaviorTree;
using UnityEngine;

namespace ArcaneOnyx.BH3.Samples
{
    [CreateAssetMenu(menuName = "BH3 Samples/Enemy Kind")]
    public sealed class EnemyKind : ScriptableObject
    {
        [SerializeField] private Material bodyMaterial;
        [SerializeField] private WeaponDefinition weapon;
        [SerializeField] private BehaviorTreeGraphAsset tree;
        [SerializeField] private int maxHealth = 100;
        [SerializeField] private float speed = 3.8f;
        [SerializeField] private float sightRadius = 8f;
        [SerializeField] private float keepDistance;
        [SerializeField] private float bodyScale = 1f;
        [SerializeField] private int bounty = 10;

        public Material BodyMaterial => bodyMaterial;
        public WeaponDefinition Weapon => weapon;
        public BehaviorTreeGraphAsset Tree => tree;
        public int MaxHealth => maxHealth;
        public float Speed => speed;
        public float SightRadius => sightRadius;
        public float KeepDistance => keepDistance;
        public float BodyScale => bodyScale;
        public int Bounty => bounty;
    }
}
