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
        [HarmonyPatch(nameof(SeaTruckSegment.OnConnectionChanged))]
        [HarmonyPrefix()]
        static void DestroyJoint(SeaTruckSegment __instance)
        {
            if (!__instance.isRearConnected)
            {
                if (__instance.gameObject.TryGetComponent<Joint>(out var joint))
                {
                    Logger.Info($"Destroying joint of {__instance}");

                    UnityEngine.Object.Destroy(joint);

                    var openedGo = __instance.rearConnection.openedGo;

                    var segmentTechType = CraftData.GetTechType(__instance.gameObject);
                    openedGo.transform.localPosition -= SeaTruckSegmentSettings.ConnectorFrontOffsets[segmentTechType];
                }
            }
        }

        [HarmonyPatch(nameof(SeaTruckSegment.OnConnectionChanged))]
        [HarmonyPostfix()]
        static void AddJoint(SeaTruckSegment __instance)
        {
            var segment = __instance;

            if (segment.isRearConnected && !__instance.gameObject.GetComponent<Joint>())
            {
                segment.StartCoroutine(AddJointAfterDocking(segment));
            }
        }

        static IEnumerator AddJointAfterDocking(SeaTruckSegment segment)
        {
            var rearSegment = segment.rearConnection.connection.truckSegment;
            var rearOpenedGo = segment.rearConnection.openedGo;

            do
            {
                yield return null;
            }
            while (rearSegment.updateDockedPosition);

            // Apply connector front/back offsets
            var segmentTechType = CraftData.GetTechType(segment.gameObject);
            rearSegment.transform.localPosition += SeaTruckSegmentSettings.ConnectorBackOffsets[segmentTechType];
            rearOpenedGo.transform.localPosition += SeaTruckSegmentSettings.ConnectorFrontOffsets[segmentTechType];

            // Assign connecting segments as 2 bones
            var bones = new Transform[2];

            bones[0] = rearSegment.transform;
            bones[1] = segment.transform;

            // Ensures original mesh is converted and replaced with skinned connector mesh
            var skinnedMeshTransform = rearOpenedGo.transform.Find("Skinned Mesh");
            if (!skinnedMeshTransform)
            {
                // Prepare skinned mesh object
                var connectorMeshFilter = rearOpenedGo.GetComponentInChildren<MeshFilter>();
                var connectorMesh = connectorMeshFilter.mesh;
                Logger.Debug($"Connector mesh {connectorMesh}");

                var skinnedObject = new GameObject("Skinned Mesh");
                skinnedObject.transform.parent = rearOpenedGo.transform;
                skinnedObject.transform.localPosition = connectorMeshFilter.transform.localPosition;
                skinnedObject.transform.localRotation = connectorMeshFilter.transform.localRotation;
                skinnedObject.transform.localScale = connectorMeshFilter.transform.localScale;

                // Load bone weights for mesh
                var meshName = connectorMesh.name.Replace(" Instance", string.Empty);
                var boneWeightsFilePath = Path.Combine("Assets", "boneweights", $"{meshName}.bytes");
                var executingAssemblyPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

                if (File.Exists(Path.Combine(executingAssemblyPath, boneWeightsFilePath)))
                {
                    using (var fileStream = new FileStream(Path.Combine(executingAssemblyPath, boneWeightsFilePath), FileMode.Open))
                    using (var binaryReader = new BinaryReader(fileStream))
                    {
                        var vertexCount = binaryReader.ReadInt32();
                        var boneWeights = new BoneWeight[vertexCount];

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

                        // Assign bone weights to mesh
                        connectorMesh.boneWeights = boneWeights;
                    }
                }
                else
                {
                    Logger.Warning($"Boneweights for \"{meshName}\" mesh does not exist");
                }

                // Prepare new renderer
                var originalMeshRenderer = rearOpenedGo.GetComponentInChildren<MeshRenderer>();
                var newSkinnedRenderer = skinnedObject.AddComponent<SkinnedMeshRenderer>();
                newSkinnedRenderer.materials = originalMeshRenderer.materials;
                newSkinnedRenderer.shadowCastingMode = originalMeshRenderer.shadowCastingMode;
                newSkinnedRenderer.receiveShadows = originalMeshRenderer.receiveShadows;
                newSkinnedRenderer.localBounds = connectorMesh.bounds;
                newSkinnedRenderer.sharedMesh = connectorMesh;

                // Destroy original mesh object
                UnityEngine.Object.Destroy(connectorMeshFilter.gameObject);

                skinnedMeshTransform = skinnedObject.transform;
            }

            // Assign new bones to renderer
            var skinnedRenderer = skinnedMeshTransform.gameObject.GetComponent<SkinnedMeshRenderer>();
            skinnedRenderer.bones = bones;

            // Calculate bindposes of renderer
            skinnedRenderer.sharedMesh.bindposes = new[] {
                bones[0].worldToLocalMatrix * skinnedMeshTransform.gameObject.transform.localToWorldMatrix,
                bones[1].worldToLocalMatrix * skinnedMeshTransform.gameObject.transform.localToWorldMatrix
            };

            // Ensures mesh colliders from simple quads is created in place of original box colliders
            if (!MeshColliders.TryGetValue(segment, out var segmentMeshColliders))
            {
                foreach (var connectorBoxCollider in rearOpenedGo.GetComponentsInChildren<BoxCollider>())
                {
                    var localExtents = connectorBoxCollider.size / 2;

                    // Create mesh
                    var colliderMesh = new Mesh();
                    if (connectorBoxCollider.transform.localScale.x > connectorBoxCollider.transform.localScale.y)
                    {
                        //   /|\ z
                        //  1 | 2
                        //    |
                        // ---+---> x
                        //    |
                        //  0 | 3
                        //    |
                        colliderMesh.vertices = new Vector3[] {
                            connectorBoxCollider.center + new Vector3(-localExtents.x, localExtents.y, -localExtents.z),
                            connectorBoxCollider.center + new Vector3(-localExtents.x, localExtents.y, localExtents.z),
                            connectorBoxCollider.center + new Vector3(localExtents.x, localExtents.y, localExtents.z),
                            connectorBoxCollider.center + new Vector3(localExtents.x, localExtents.y, -localExtents.z)
                        };
                    }
                    else
                    {
                        //   /|\ z
                        //  1 | 2
                        //    |
                        // ---+---> y
                        //    |
                        //  0 | 3
                        //    |
                        colliderMesh.vertices = new Vector3[] {
                            connectorBoxCollider.center + new Vector3(0f, -localExtents.y, -localExtents.z),
                            connectorBoxCollider.center + new Vector3(0f, -localExtents.y, localExtents.z),
                            connectorBoxCollider.center + new Vector3(0f, localExtents.y, localExtents.z),
                            connectorBoxCollider.center + new Vector3(0f, localExtents.y, -localExtents.z)
                        };
                    }

                    //   /|\s
                    //  1---2
                    //  |  /|
                    // -| / |->
                    //  |/  |
                    //  0---3
                    //    |
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

                    Mesh meshForCollider = new Mesh();

                    var meshCollider = connectorBoxCollider.gameObject.AddComponent<MeshCollider>();
                    meshCollider.sharedMaterial = connectorBoxCollider.sharedMaterial;
                    meshCollider.sharedMesh = meshForCollider;
                    meshCollider.convex = true;
                    meshCollider.cookingOptions = MeshColliderCookingOptions.None;

                    connectorBoxCollider.enabled = false;

                    var skinnedCollisionRenderer = connectorBoxCollider.gameObject.AddComponent<SkinnedMeshRenderer>();
                    skinnedCollisionRenderer.sharedMesh = colliderMesh;
                    skinnedCollisionRenderer.enabled = false;
                }

                segmentMeshColliders = rearOpenedGo.GetComponentsInChildren<MeshCollider>();
                MeshColliders.Add(segment, segmentMeshColliders);
            }

            // Assign bones and calculate bindposes for collider mesh renderers
            foreach (var meshCollider in segmentMeshColliders)
            {
                var colliderMeshRenderer = meshCollider.gameObject.GetComponent<SkinnedMeshRenderer>();
                colliderMeshRenderer.bones = bones;
                colliderMeshRenderer.sharedMesh.bindposes = new[] {
                    bones[0].worldToLocalMatrix * meshCollider.gameObject.transform.localToWorldMatrix,
                    bones[1].worldToLocalMatrix * meshCollider.gameObject.transform.localToWorldMatrix
                };
            }

            // Offset rear segment before creating joint
            rearSegment.transform.localPosition += Vector3.back * 0.25f;

            // Deparent player piloting temporarly
            if (Player.main.transform.parent == segment.motor.pilotPosition)
            {
                Player.main.transform.parent = null;
            }

            // Deparent
            rearSegment.transform.parent = null;
            LargeWorldStreamer.main.cellManager.RegisterEntity(rearSegment.gameObject);

            // Assign center of mass of connecting segments
            var rearSegmentTechType = CraftData.GetTechType(rearSegment.gameObject);
            rearSegment.rb.centerOfMass = SeaTruckSegmentSettings.ConnectorJointAnchors[rearSegmentTechType];
            segment.rb.centerOfMass = SeaTruckSegmentSettings.ConnectorJointAnchors[segmentTechType];

            // Create joint
            if (!segment.gameObject.TryGetComponent<CharacterJoint>(out var joint))
            {
                Logger.Info($"Creating joint");

                joint = segment.gameObject.AddComponent<CharacterJoint>();

                joint.anchor = SeaTruckSegmentSettings.ConnectorJointAnchors[segmentTechType];
                joint.axis = Vector3.forward;
                joint.lowTwistLimit = new SoftJointLimit() { limit = -20f };
                joint.highTwistLimit = new SoftJointLimit() { limit = 20f };
                joint.swing1Limit = new SoftJointLimit() { limit = 30f };
                joint.swing2Limit = new SoftJointLimit() { limit = 30f };
            }

            Logger.Info($"Connecting joint");
            joint.connectedBody = rearSegment.rb;

            var segmentColliders = segment.gameObject.GetComponentsInChildren<Collider>();
            foreach (var collider in segmentColliders)
            {
                collider.enabled = false;
                collider.enabled = true;
            }

            // Restore player pilotable parent if piloting
            if (segment.motor.piloting && !Player.main.transform.parent)
            {
                Player.main.transform.parent = segment.motor.pilotPosition;
            }

            //Utils.DrawDebugPrimitive(segment.gameObject, joint.anchor);

            Logger.Debug($"joint = {joint}");
            Logger.Debug($"joint.axis = {joint.axis}");
            Logger.Debug($"joint.connectedBody = {joint.connectedBody}");
            Logger.Debug($"joint.connectedAnchor = {joint.connectedAnchor}");
            Logger.Debug($"joint.swingAxis = {joint.swingAxis}");
            Logger.Debug($"joint.swing1Limit = {joint.swing1Limit.limit}");
            Logger.Debug($"joint.swing2Limit = {joint.swing2Limit.limit}");
            Logger.Debug($"joint.lowTwistLimit = {joint.lowTwistLimit.limit}");
            Logger.Debug($"joint.highTwistLimit = {joint.highTwistLimit.limit}");

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

                    var invScale = new Vector3(1f / meshCollider.transform.lossyScale.x,
                                            1f / meshCollider.transform.lossyScale.y,
                                            1f / meshCollider.transform.lossyScale.z);

                    var vertices = colliderMesh.vertices;
                    for (var i = 0; i < vertices.Length; i++)
                    {
                        vertices[i].Scale(invScale);
                    }
                    colliderMesh.vertices = vertices;

                    if (colliderMesh.vertexCount > 0)
                    {
                        meshCollider.sharedMesh = colliderMesh;
                    }
                }
            }

            Utils.DrawConnectorColliderLines(__instance);
        }
    }
}
