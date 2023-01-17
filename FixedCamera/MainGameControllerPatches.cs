using HarmonyLib;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SubnauticaSeaTruckFlexible.FixedCamera
{
    [HarmonyPatch(typeof(MainGameController))]
    internal class MainGameControllerPatches
    {
        [HarmonyPatch(nameof(MainGameController.StartGame))]
        [HarmonyPostfix()]
        static IEnumerator AddFixedCameraControllerAfterGameLoaded(IEnumerator enumerator)
        {
            yield return enumerator;

            var freecamController = UnityEngine.Object.FindObjectOfType<FreecamController>();
            if (freecamController)
            {
                var go = freecamController.gameObject;

                if (!go.TryGetComponent<FixedCameraController>(out var fixedCamera))
                {
                    go.AddComponent<FixedCameraController>();

                    Logger.Info($"FixedCameraController added to {go}");
                }
                else
                {
                    Logger.Info($"Already have FixedCameraController added to {go}");
                }
            }
            else
            {
                Logger.Warning($"No FreecamController found.");
            }
        }
    }
}
