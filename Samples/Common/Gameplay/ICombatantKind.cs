using ArcaneOnyx.BehaviorTree;
using UnityEngine;

namespace ArcaneOnyx.BH3.Samples
{
    public interface ICombatantKind
    {
        Material BodyMaterial { get; }
        WeaponDefinition Weapon { get; }
        BehaviorTreeGraphAsset Tree { get; }
        int MaxHealth { get; }
        float Speed { get; }
        float SightRadius { get; }
        float KeepDistance { get; }
        float BodyScale { get; }
    }
}
