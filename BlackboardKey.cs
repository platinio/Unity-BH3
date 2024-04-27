using UnityEngine;

namespace Platinio.BehaviorTree
{
    [System.Serializable]
    public class BlackboardKey
    {
        [SerializeField] private string blackboardKeyName;

        public string BlackboardKeyName => blackboardKeyName;
    }
}