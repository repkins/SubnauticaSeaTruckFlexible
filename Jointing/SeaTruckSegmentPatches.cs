using HarmonyLib;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
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
                    Logger.Info($"Separating joint");

                    joint.connectedBody = null;
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
                segment.rb.isKinematic = true;

                segment.StartCoroutine(AddJointAfterDocking(segment));
            }
        }

        static IEnumerator AddJointAfterDocking(SeaTruckSegment segment)
        {
            var rearSegment = segment.rearConnection.connection.truckSegment;

            do
            {
                yield return null;
            }
            while (rearSegment.updateDockedPosition);

            if (segment.isMainCab)
            {
                rearSegment.transform.localPosition += Vector3.back * 0.3835f;
            }

            // Convert connected mesh to skinned
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
                        Logger.Warning($"Boneweights for \"{meshName}\" mesh does not exist");
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

                MeshColliders.Add(segment, openedGo.GetComponentsInChildren<MeshCollider>());
            }

            // Offset rear segment before creating joint
            rearSegment.transform.localPosition += Vector3.back * 0.25f;

            rearSegment.transform.parent = null;

            rearSegment.rb.centerOfMass = Vector3.zero;
            segment.rb.centerOfMass = Vector3.zero;

            // Create joint
            if (!segment.gameObject.TryGetComponent<Joint>(out var joint))
            {
                Logger.Info($"Creating joint");

                joint = segment.gameObject.AddComponent<CharacterJoint>();

                joint.anchor = Vector3.zero;
            }

            Logger.Info($"Connecting joint");
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

            yield break;
        }

        [HarmonyPatch(nameof(SeaTruckSegment.OnDestroy))]
        [HarmonyPostfix()]
        static void ClearMeshColliders(SeaTruckSegment __instance)
        {
            MeshColliders.Remove(__instance);
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

        static Dictionary<SeaTruckSegment, MeshCollider[]> MeshColliders = new Dictionary<SeaTruckSegment, MeshCollider[]>();

        [HarmonyPatch(nameof(SeaTruckSegment.Update))]
        [HarmonyPostfix()]
        static void UpdateConnectorCollision(SeaTruckSegment __instance)
        {
            if (MeshColliders.TryGetValue(__instance, out var segmentMeshColliders))
            {
                foreach (var meshCollider in segmentMeshColliders)
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
}
