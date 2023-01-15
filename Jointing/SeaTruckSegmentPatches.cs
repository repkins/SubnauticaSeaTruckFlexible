using HarmonyLib;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using UnityEngine;

namespace SubnauticaSeaTruckFlexible.Jointing.SeaTruckSegmentPatches
{
    [HarmonyPatch(typeof(SeaTruckSegment))]
    [HarmonyPatch(nameof(SeaTruckSegment.OnConnectionChanged))]
    static class OnConnectionChangedPatch
    {
        static void Prefix(SeaTruckSegment __instance)
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

        static void Postfix(SeaTruckSegment __instance)
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

            rearSegment.transform.localPosition += Vector3.back * 0.5f;
            rearSegment.transform.parent = null;

            if (!segment.gameObject.TryGetComponent<Joint>(out var joint))
            {
                Logger.Info($"Creating joint");

                joint = segment.gameObject.AddComponent<CharacterJoint>();
                joint.enableCollision = false;
                //joint.autoConfigureConnectedAnchor = false;
                joint.connectedBody = rearSegment.rb;
                //joint.swing1Limit = new SoftJointLimit()
                //{
                //    limit = 30.0f,
                //    bounciness = 0.0f,
                //    contactDistance = 0.0f
                //};
                //joint.swingAxis = new Vector3(1.0f, 0.0f, 0.0f);
                //joint.anchor = new Vector3(0.0f, 0.0f, 1.0f);
                //joint.connectedAnchor = new Vector3(0.0f, 0.0f, 0.0f);

                var segmentColliders = segment.gameObject.GetComponentsInChildren<Collider>();
                foreach (var collider in segmentColliders)
                {
                    collider.enabled = false;
                    collider.enabled = true;
                }

                DrawDebugPrimitive(segment.gameObject);
                DrawDebugPrimitive(rearSegment.gameObject);

                Logger.Debug($"joint = {joint}");
                Logger.Debug($"joint.connectedBody = {joint.connectedBody}");
                Logger.Debug($"joint.enableCollision = {joint.enableCollision}");
            }
            else
            {
                Logger.Warning($"joint already exits for {segment.gameObject}.");
            }

            yield break;
        }

        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
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

        private static void DrawDebugPrimitive(GameObject go)
        {
            const string DebugPrimitiveName = "DebugPrimimive";

            if (!go.transform.Find(DebugPrimitiveName))
            {
                var spherePrim = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                spherePrim.name = DebugPrimitiveName;
                spherePrim.transform.parent = go.transform;
                spherePrim.transform.localPosition = Vector3.zero;
                spherePrim.transform.localScale = Vector3.one * 0.5f;

                if (spherePrim.TryGetComponent<Collider>(out var collider))
                {
                    UnityEngine.Object.Destroy(collider);
                }
            }
        }
    }

    [HarmonyPatch(typeof(SeaTruckSegment))]
    [HarmonyPatch(nameof(SeaTruckSegment.Update))]
    static class UpdatePatches
    { 
        static void Prefix(SeaTruckSegment __instance, ref bool __state)
        {
            __state = __instance.updateDockedPosition;
        }

        static void Postfix(SeaTruckSegment __instance, ref bool __state)
        {
            
        }

        
    }
}
