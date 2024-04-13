using Unity.VisualScripting;
using UnityEngine;

namespace Platinio.BehaviorTree
{
    [Inspector(typeof(Vector3BlackboardVariable))]
    public class Vector3BlackboardVariableInspector : BlackboardVariableInspector<Vector3>
    {
        public Vector3BlackboardVariableInspector(Metadata metadata) : base(metadata)
        {
        }
    }
}