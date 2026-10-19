using UnityEngine;

namespace ArcaneOnyx.BH3.Samples
{
    /// <summary>
    /// What a building needs to know before the unit exists: which prefab to make, what it costs and how
    /// long it takes. The unit itself — its tree, health, weapon, sight, speed and size — is authored on
    /// that prefab.
    /// </summary>
    [CreateAssetMenu(menuName = "BH3 Samples/Unit Kind")]
    public sealed class UnitKind : ScriptableObject
    {
        [SerializeField] private RtsUnit prefab;
        [SerializeField] private int goldCost = 50;
        [SerializeField] private int woodCost;
        [SerializeField] private float trainSeconds = 6f;

        public string Id => name;
        public RtsUnit Prefab => prefab;
        public int GoldCost => goldCost;
        public int WoodCost => woodCost;
        public float TrainSeconds => trainSeconds;
    }
}
