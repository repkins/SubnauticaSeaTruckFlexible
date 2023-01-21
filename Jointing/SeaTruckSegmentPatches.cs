using HarmonyLib;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using UnityEngine;

namespace SubnauticaSeaTruckFlexible.Jointing.SeaTruckSegmentPatches
{
    [HarmonyPatch(typeof(SeaTruckSegment))]
    static class OnConnectionChangedPatch
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
                rearSegment.transform.localPosition += Vector3.back * 0.5f;
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

            //DrawDebugPrimitive(segment.gameObject, joint.anchor);
            //DrawDebugPrimitive(rearSegment.gameObject, joint.connectedAnchor);

            Logger.Debug($"joint = {joint}");
            Logger.Debug($"joint.connectedBody = {joint.connectedBody}");
            Logger.Debug($"joint.connectedAnchor = {joint.connectedAnchor}");

            var openedGo = segment.rearConnection.openedGo;

            // Create skinned mesh
            if (!openedGo.transform.Find("Skinned Mesh"))
            {
                var connectorMeshFilter = openedGo.GetComponentInChildren<MeshFilter>();
                var connectorMesh = connectorMeshFilter.mesh;
                Logger.Debug($"Connector mesh {connectorMesh}");

                var skinnedObject = new GameObject("Skinned Mesh");
                skinnedObject.transform.parent = openedGo.transform;
                skinnedObject.transform.position = connectorMeshFilter.transform.position;
                skinnedObject.transform.rotation = connectorMeshFilter.transform.rotation;
                skinnedObject.transform.localScale = connectorMeshFilter.transform.localScale;

                var skinnedRenderer = skinnedObject.AddComponent<SkinnedMeshRenderer>();
                skinnedRenderer.materials = openedGo.GetComponentInChildren<MeshRenderer>().materials;
                skinnedRenderer.localBounds = connectorMesh.bounds;

                var bones = new Transform[2];
                bones[0] = new GameObject("Lower").transform;
                bones[0].parent = skinnedObject.transform;

                // Set the position relative to the parent
                bones[0].localRotation = Quaternion.identity;
                bones[0].localPosition = new Vector3(0, 0, -2);

                bones[1] = new GameObject("Upper").transform;
                bones[1].parent = skinnedObject.transform;

                // Set the position relative to the parent
                bones[1].localRotation = Quaternion.identity;
                bones[1].localPosition = new Vector3(0f, 0f, -2.6f);

                connectorMesh.bindposes = new[] {
                    bones[0].worldToLocalMatrix * skinnedObject.transform.localToWorldMatrix,
                    bones[1].worldToLocalMatrix * skinnedObject.transform.localToWorldMatrix
                };

                if (boneWeights == null)
                {
                    var boneWeightsFilePath = Path.Combine("Assets", "boneweights.bytes");
                    var executingAssemblyPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

                    using (var fileStream = new FileStream(Path.Combine(executingAssemblyPath, boneWeightsFilePath), FileMode.Open))
                    using (var binaryReader = new BinaryReader(fileStream))
                    {
                        var vertexCount = binaryReader.ReadInt32();
                        boneWeights = new BoneWeight[vertexCount];

                        for (var i = 0; i< vertexCount; i++)
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

                connectorMesh.boneWeights = boneWeights;

                skinnedRenderer.bones = bones;
                skinnedRenderer.sharedMesh = connectorMesh;

                UnityEngine.Object.Destroy(connectorMeshFilter.gameObject);
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

        [HarmonyPatch(nameof(SeaTruckSegment.Update))]
        [HarmonyPostfix()]
        static void DrawPrimDirection(SeaTruckSegment __instance)
        {
            var lines = __instance.rearConnection.openedGo.GetComponentsInChildren<LineRenderer>(true);

            lines.Where(l => l.gameObject.name == DebugPrimitiveName).ForEach(line =>
            {
                line.positionCount = 2;

                // set the position
                line.SetPosition(0, line.transform.position);
                line.SetPosition(1, line.transform.position + line.transform.forward * 1f);
            });
        }
    }
}
