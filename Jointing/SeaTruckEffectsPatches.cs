using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SubnauticaSeaTruckFlexible.Jointing.SeaTruckEffectsPatches
{
    [HarmonyPatch(typeof(SeaTruckEffects))]
    [HarmonyPatch(nameof(SeaTruckEffects.Update))]
    static class UpdatePatch
    {
        static void Prefix(SeaTruckEffects __instance)
        {
            if (!__instance.useRigidbody)
            {
                Logger.Warning($"Missing object instance SeaTruckEffects.useRigidbody referering to");
            }
        }
    }
}
