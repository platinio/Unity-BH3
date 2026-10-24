using UnityEngine;

namespace ArcaneOnyx.BH3.Samples
{
    [CreateAssetMenu(menuName = "BH3 Samples/Turret Kind")]
    public sealed class TurretKind : ScriptableObject
    {
        [SerializeField] private Turret prefab;
        [SerializeField] private int cost = 50;

        public string Id => name;
        public Turret Prefab => prefab;
        public int Cost => cost;
    }
}
