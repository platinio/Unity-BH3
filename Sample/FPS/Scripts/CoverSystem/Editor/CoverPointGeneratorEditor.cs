using Unity.AI.Navigation;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

namespace ArcaneOnyx.BehaviorTree.Editor
{
    [CustomEditor(typeof(CoverPointGenerator))]
    public class CoverPointGeneratorEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            if (GUILayout.Button("Generate Cover Points"))
            {
                NavMeshTriangulation navMeshTriangulation = NavMesh.CalculateTriangulation();

                var coverPoints = GameObject.Find("CoverPoints");
                if (coverPoints == null) coverPoints = new GameObject("CoverPoints");
                
                foreach (var vertex in navMeshTriangulation.vertices)
                {
                    var point = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    point.transform.parent = coverPoints.transform;
                    point.transform.position = vertex;
                }
            }
            
        }

        private void CreateCoverPoints(Vector3 a, Vector3 b)
        {
            float coverWidth = 0.5f;
            
            
        }
    }
}