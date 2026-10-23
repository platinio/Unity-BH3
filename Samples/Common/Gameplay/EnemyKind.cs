using UnityEngine;

namespace ArcaneOnyx.BH3.Samples
{
    /// <summary>
    /// What killing one of these is worth. Everything else about an enemy — its tree, health, weapon,
    /// sight, speed and size — is authored on its prefab, where a designer can see it.
    /// </summary>
    [CreateAssetMenu(menuName = "BH3 Samples/Enemy Kind")]
    public sealed class EnemyKind : ScriptableObject
    {
        [SerializeField] private int bounty = 10;

        public int Bounty => bounty;
    }
}
