using UnityEngine;

namespace ArcaneOnyx.BehaviorTree
{
    public class Cloner : MonoBehaviour
    {
        public GameObject prefab;
        public int amount;

        private void Start()
        {
            for (int i = 0; i < amount; i++)
            {
                Instantiate(prefab);
            }
        }
    }

}

