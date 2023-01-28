using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace SubnauticaSeaTruckFlexible
{
    internal static class Utils
    {
        const string DebugPrimitiveName = "DebugPrimitive";

        public static GameObject DrawDebugPrimitive(GameObject go)
        {
            return DrawDebugPrimitive(go, Vector3.zero);
        }

        public static GameObject DrawDebugPrimitive(GameObject go, Vector3 localPosition)
        {
            var spherePrim = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            spherePrim.name = DebugPrimitiveName;
            if (spherePrim.TryGetComponent<Collider>(out var collider))
            {
                UnityEngine.Object.Destroy(collider);
            }
            spherePrim.transform.parent = go.transform;
            spherePrim.transform.localPosition = localPosition;
            spherePrim.transform.localScale = Vector3.one * 0.25f;

            return spherePrim;
        }

        public static void ClearDebugPrimitives(GameObject go)
        {
            foreach (Transform transform in go.transform)
            {
                if (transform.gameObject.name == DebugPrimitiveName)
                {
                    UnityEngine.Object.Destroy(transform.gameObject);
                }
            }
        }

        public static void DrawConnectorColliderLines(SeaTruckSegment segment)
        {
            if (!segment.isRearConnected)
            {
                return;
            }

            var openedGo = segment.rearConnection.openedGo;
            if (!openedGo.GetComponentInChildren<LineRenderer>())
            {
                Material material = new Material(Shader.Find("Unlit/Color"));
                Color color = Color.green;
                material.color = color;
                float width = 0.01f;

                foreach (var meshCollider in openedGo.GetComponentsInChildren<MeshCollider>())
                {
                    var triVertices = meshCollider.sharedMesh.vertices;

                    for (var i = 0; i < triVertices.Length; i += 3)
                    {
                        DrawLine(meshCollider.gameObject, i, color, material, width);
                        DrawLine(meshCollider.gameObject, i + 1, color, material, width);
                        DrawLine(meshCollider.gameObject, i + 2, color, material, width);
                    }
                }
            }

            foreach (var meshCollider in openedGo.GetComponentsInChildren<MeshCollider>())
            {
                var triVertexIndices = meshCollider.sharedMesh.triangles;
                var vertices = meshCollider.sharedMesh.vertices;

                for (var i = 0; i < triVertexIndices.Length; i += 3)
                {
                    PositionLine(meshCollider.gameObject, i, vertices[triVertexIndices[i]], vertices[triVertexIndices[i + 1]]);
                    PositionLine(meshCollider.gameObject, i + 1, vertices[triVertexIndices[i + 1]], vertices[triVertexIndices[i + 2]]);
                    PositionLine(meshCollider.gameObject, i + 2, vertices[triVertexIndices[i + 2]], vertices[triVertexIndices[i]]);
                }
            }

        }

        static void DrawLine(GameObject attachTo, int index, Color color, Material material, float width = 0.01f)
        {
            LineRenderer line = new GameObject($"Line_{index}").AddComponent<LineRenderer>();
            line.material = material;
            line.startColor = color;
            line.endColor = color;
            line.startWidth = width;
            line.endWidth = width;
            line.positionCount = 2;
            line.useWorldSpace = true;
            line.transform.SetParent(attachTo.transform);
        }

        static void PositionLine(GameObject attachTo, int index, Vector3 start, Vector3 end)
        {
            LineRenderer line = attachTo.transform.Find($"Line_{index}").GetComponent<LineRenderer>();

            line.SetPosition(0, attachTo.transform.TransformPoint(start));
            line.SetPosition(1, attachTo.transform.TransformPoint(end));
        }
    }
}
