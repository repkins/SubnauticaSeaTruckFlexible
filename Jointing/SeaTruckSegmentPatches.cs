using HarmonyLib;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace SubnauticaSeaTruckFlexible.Jointing
{
    [HarmonyPatch(typeof(SeaTruckSegment))]
    static class SeaTruckSegmentPatches
    {
        static BoneWeight[] boneWeights;

        [HarmonyPatch(nameof(SeaTruckSegment.OnConnectionChanged))]
        [HarmonyPrefix()]
        static void DestroyJoint(SeaTruckSegment __instance)
        {
            if (!__instance.isRearConnected)
            {
                if (__instance.gameObject.TryGetComponent<Joint>(out var joint))
                {
                    Logger.Info($"Destroying joint");

                    UnityEngine.Object.Destroy(joint);
                }
            }
        }

        [HarmonyPatch(nameof(SeaTruckSegment.OnConnectionChanged))]
        [HarmonyPostfix()]
        static void AddJoint(SeaTruckSegment __instance)
        {
            var segment = __instance;

            if (segment.isRearConnected)
            {
                segment.StartCoroutine(AddJointAfterDocking(segment));

                segment.rb.isKinematic = true;
            }
        }

        static IEnumerator AddJointAfterDocking(SeaTruckSegment segment)
        {
            var rearSegment = segment.rearConnection.connection.truckSegment;

            while (rearSegment.updateDockedPosition)
            {
                yield return null;
            }

            if (segment.isMainCab)
            {
                rearSegment.transform.localPosition += Vector3.back * 0.3835f;
            }
            rearSegment.transform.parent = null;

            rearSegment.rb.centerOfMass = Vector3.zero;
            segment.rb.centerOfMass = Vector3.zero;

            if (!segment.gameObject.TryGetComponent<Joint>(out var joint))
            {
                Logger.Info($"Creating joint");

                joint = segment.gameObject.AddComponent<CharacterJoint>();
            }

            joint.anchor = Vector3.zero;
            joint.connectedBody = rearSegment.rb;

            var segmentColliders = segment.gameObject.GetComponentsInChildren<Collider>();
            foreach (var collider in segmentColliders)
            {
                collider.enabled = false;
                collider.enabled = true;
            }

            segment.rb.isKinematic = false;

            //DrawDebugPrimitive(segment.gameObject, joint.anchor);
            //DrawDebugPrimitive(rearSegment.gameObject, joint.connectedAnchor);

            Logger.Debug($"joint = {joint}");
            Logger.Debug($"joint.connectedBody = {joint.connectedBody}");
            Logger.Debug($"joint.connectedAnchor = {joint.connectedAnchor}");

            var openedGo = segment.rearConnection.openedGo;

            if (!openedGo.transform.Find("Skinned Mesh"))
            {
                // Prepare skinned mesh object
                var connectorMeshFilter = openedGo.GetComponentInChildren<MeshFilter>();
                var connectorMesh = connectorMeshFilter.mesh;
                Logger.Debug($"Connector mesh {connectorMesh}");

                var skinnedObject = new GameObject("Skinned Mesh");
                skinnedObject.transform.parent = openedGo.transform;
                skinnedObject.transform.localPosition = connectorMeshFilter.transform.localPosition;
                skinnedObject.transform.localRotation = connectorMeshFilter.transform.localRotation;
                skinnedObject.transform.localScale = connectorMeshFilter.transform.localScale;

                // Assign connecting segments as 2 bones
                var bones = new Transform[2];

                bones[0] = rearSegment.transform;
                bones[1] = segment.transform;

                // Calculate bindposes for bones
                connectorMesh.bindposes = new[] {
                    bones[0].worldToLocalMatrix * skinnedObject.transform.localToWorldMatrix,
                    bones[1].worldToLocalMatrix * skinnedObject.transform.localToWorldMatrix
                };

                if (boneWeights == null)
                {
                    // Load bone weights data for mesh
                    var meshName = connectorMesh.name.Replace(" Instance", string.Empty);
                    var boneWeightsFilePath = Path.Combine("Assets", "boneweights", $"{meshName}.bytes");
                    var executingAssemblyPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

                    if (File.Exists(Path.Combine(executingAssemblyPath, boneWeightsFilePath)))
                    {
                        using (var fileStream = new FileStream(Path.Combine(executingAssemblyPath, boneWeightsFilePath), FileMode.Open))
                        using (var binaryReader = new BinaryReader(fileStream))
                        {
                            var vertexCount = binaryReader.ReadInt32();
                            boneWeights = new BoneWeight[vertexCount];

                            for (var i = 0; i < vertexCount; i++)
                            {
                                boneWeights[i].boneIndex0 = binaryReader.ReadInt32();
                                boneWeights[i].weight0 = binaryReader.ReadSingle();

                                boneWeights[i].boneIndex1 = binaryReader.ReadInt32();
                                boneWeights[i].weight1 = binaryReader.ReadSingle();

                                boneWeights[i].boneIndex2 = binaryReader.ReadInt32();
                                boneWeights[i].weight2 = binaryReader.ReadSingle();

                                boneWeights[i].boneIndex3 = binaryReader.ReadInt32();
                                boneWeights[i].weight3 = binaryReader.ReadSingle();
                            }
                        }
                    }
                    else
                    {
                        Logger.Warning($"Boneweights for \"{connectorMesh.name}\" mesh does not exist");
                    }
                }

                // Assign bone weights to mesh
                connectorMesh.boneWeights = boneWeights;

                // Prepare renderer
                var skinnedRenderer = skinnedObject.AddComponent<SkinnedMeshRenderer>();
                skinnedRenderer.materials = openedGo.GetComponentInChildren<MeshRenderer>().materials;
                skinnedRenderer.localBounds = connectorMesh.bounds;
                skinnedRenderer.sharedMesh = connectorMesh;
                skinnedRenderer.bones = bones;

                // Destroy original mesh object
                UnityEngine.Object.Destroy(connectorMeshFilter.gameObject);

                // Make mesh colliders from simple quads in place of box colliders
                foreach (var connectorCollider in openedGo.GetComponentsInChildren<BoxCollider>())
                {
                    var localExtents = connectorCollider.size / 2;

                    // Create mesh
                    var colliderMesh = new Mesh();
                    if (connectorCollider.transform.localScale.x > connectorCollider.transform.localScale.y)
                    {
                        colliderMesh.vertices = new Vector3[] {
                            connectorCollider.center + new Vector3(-localExtents.x, 0f, -localExtents.z),
                            connectorCollider.center + new Vector3(-localExtents.x, 0f, localExtents.z),
                            connectorCollider.center + new Vector3(localExtents.x, 0f, localExtents.z),
                            connectorCollider.center + new Vector3(localExtents.x, 0f, -localExtents.z)
                        };
                    }
                    else
                    {
                        colliderMesh.vertices = new Vector3[] {
                            connectorCollider.center + new Vector3(0f, -localExtents.y, -localExtents.z),
                            connectorCollider.center + new Vector3(0f, -localExtents.y, localExtents.z),
                            connectorCollider.center + new Vector3(0f, localExtents.y, localExtents.z),
                            connectorCollider.center + new Vector3(0f, localExtents.y, -localExtents.z)
                        };
                    }
                    colliderMesh.triangles = new[] {
                        0, 1, 2,
                        2, 3, 0
                    };
                    colliderMesh.boneWeights = new[] { 
                        new BoneWeight()
                        {
                            boneIndex0 = 0,
                            weight0 = 1,
                        },
                        new BoneWeight()
                        {
                            boneIndex0 = 1,
                            weight0 = 1,
                        },
                        new BoneWeight()
                        {
                            boneIndex0 = 1,
                            weight0 = 1,
                        },
                        new BoneWeight()
                        {
                            boneIndex0 = 0,
                            weight0 = 1,
                        }
                    };
                    colliderMesh.bindposes = new[] {
                        bones[0].worldToLocalMatrix * connectorCollider.gameObject.transform.localToWorldMatrix,
                        bones[1].worldToLocalMatrix * connectorCollider.gameObject.transform.localToWorldMatrix
                    };

                    Mesh meshForCollider = new Mesh();

                    var meshCollider = connectorCollider.gameObject.AddComponent<MeshCollider>();
                    meshCollider.sharedMaterial = connectorCollider.sharedMaterial;
                    meshCollider.sharedMesh = meshForCollider;
                    meshCollider.convex = true;
                    meshCollider.cookingOptions = MeshColliderCookingOptions.None;

                    connectorCollider.enabled = false;

                    var skinnedCollisionRenderer = connectorCollider.gameObject.AddComponent<SkinnedMeshRenderer>();
                    skinnedCollisionRenderer.sharedMesh = colliderMesh;
                    skinnedCollisionRenderer.bones = bones;
                    skinnedCollisionRenderer.enabled = false;
                }

                openedGo.GetComponentsInChildren(meshColliders);
            }

            yield break;
        }

        [HarmonyPatch(nameof(SeaTruckSegment.OnConnectionChanged))]
        [HarmonyTranspiler()]
        static IEnumerable<CodeInstruction> RemoveRigidbodyDestruction(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            var codeMatcherCursor = new CodeMatcher(instructions);

            ApplyNoRigidbodyDestructionPatch(codeMatcherCursor, generator);
            if (codeMatcherCursor.IsInvalid)
            {
                codeMatcherCursor.ReportFailure(AccessTools.Method(typeof(SeaTruckSegment), nameof(SeaTruckSegment.OnConnectionChanged)), Logger.Warning);
                return instructions;
            }

            return codeMatcherCursor.InstructionEnumeration();
        }

        static void ApplyNoRigidbodyDestructionPatch(CodeMatcher codeCursor, ILGenerator generator)
        {
            codeCursor.Start();
            codeCursor.MatchForward(false, 
                new CodeMatch(OpCodes.Ldarg_0),
                new CodeMatch(OpCodes.Ldfld, AccessTools.Field(typeof(SeaTruckSegment), nameof(SeaTruckSegment.rb))),
                new CodeMatch(OpCodes.Call, AccessTools.Method(typeof(UnityEngine.Object), nameof(UnityEngine.Object.Destroy), new[] { typeof(UnityEngine.Object) }))
            );

            if (codeCursor.IsValid)
            {
                // Remove Rigidbody destruction call instructions.
                codeCursor.RemoveInstructions(3);
            }
        }

        const string DebugPrimitiveName = "DebugPrimitive";

        private static GameObject DrawDebugPrimitive(GameObject go)
        {
            return DrawDebugPrimitive(go, Vector3.zero);
        }

        private static GameObject DrawDebugPrimitive(GameObject go, Vector3 localPosition)
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

        private static void ClearDebugPrimitives(GameObject go)
        {
            foreach (Transform transform in go.transform)
            {
                if (transform.gameObject.name == DebugPrimitiveName)
                {
                    UnityEngine.Object.Destroy(transform.gameObject);
                }
            }
        }

        //[HarmonyPatch(nameof(SeaTruckSegment.Update))]
        //[HarmonyPostfix()]
        static void DrawConnectorColliderLines(SeaTruckSegment __instance)
        {
            if (!__instance.isRearConnected)
            {
                return;
            }

            var openedGo = __instance.rearConnection.openedGo;
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
                        DrawLine(meshCollider.gameObject, i+1, color, material, width);
                        DrawLine(meshCollider.gameObject, i+2, color, material, width);
                    }
                }
            }

            foreach (var meshCollider in openedGo.GetComponentsInChildren<MeshCollider>())
            {
                var triVertexIndices = meshCollider.sharedMesh.triangles;
                var vertices = meshCollider.sharedMesh.vertices;

                for (var i = 0; i < triVertexIndices.Length; i += 3)
                {
                    PositionLine(meshCollider.gameObject, i, vertices[triVertexIndices[i]], vertices[triVertexIndices[i+1]]);
                    PositionLine(meshCollider.gameObject, i+1, vertices[triVertexIndices[i+1]], vertices[triVertexIndices[i+2]]);
                    PositionLine(meshCollider.gameObject, i+2, vertices[triVertexIndices[i+2]], vertices[triVertexIndices[i]]);
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

        public static List<MeshCollider> meshColliders = new List<MeshCollider>();
    }

    [HarmonyPatch(typeof(SeaTruckMotor))]
    static class SeaTruckMotorPatches
    {
        [HarmonyPatch(nameof(SeaTruckMotor.FixedUpdate))]
        [HarmonyPostfix()]
        static void UpdateConnectorCollision(SeaTruckMotor __instance)
        {
            foreach (var meshCollider in SeaTruckSegmentPatches.meshColliders.Where(collider => collider))
            {
                var colliderMesh = meshCollider.sharedMesh;
                meshCollider.gameObject.GetComponent<SkinnedMeshRenderer>().BakeMesh(colliderMesh);

                var invScale = new Vector3(1f / meshCollider.transform.localScale.x,
                                        1f / meshCollider.transform.localScale.y,
                                        1f / meshCollider.transform.localScale.z);

                var vertices = colliderMesh.vertices;
                for (var i = 0; i < vertices.Length; i++)
                {
                    vertices[i].Scale(invScale);
                }
                colliderMesh.vertices = vertices;

                meshCollider.sharedMesh = colliderMesh;
            }
        }
    }
}
