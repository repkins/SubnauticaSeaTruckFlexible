using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace SubnauticaSeaTruckFlexible.FixedCamera
{
    [HarmonyPatch(typeof(PlayerController))]
    internal class PlayerControllerPatches
    {
        static FixedCameraController fixedCamera;

        [HarmonyPatch(nameof(PlayerController.forwardReference), MethodType.Getter)]
        [HarmonyPrefix()]
        static bool GetFromMainCameraControl(ref Transform __result)
        {
            if (!fixedCamera)
            {
                fixedCamera = UnityEngine.Object.FindObjectOfType<FixedCameraController>();
            }
            if (fixedCamera && fixedCamera.isActive)
            {
                __result = MainCameraControl.main.transform;

                return false;
            }

            return true;
        }
    }
}
