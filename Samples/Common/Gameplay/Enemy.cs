using UnityEngine;

namespace ArcaneOnyx.BH3.Samples
{
    [RequireComponent(typeof(Combatant))]
    public sealed class Enemy : MonoBehaviour
    {
        [SerializeField] private EnemyKind kind;

        public EnemyKind Kind => kind;
    }
}
