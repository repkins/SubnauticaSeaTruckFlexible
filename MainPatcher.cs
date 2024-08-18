using HarmonyLib;
using System.Reflection;

namespace SubnauticaSeaTruckFlexible
{
    public class MainPatcher
    {
        private static Harmony harmony;

        public static void Patch()
        {
            harmony = Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly(), "subnautica.repkins.seatruck-flexible");
            Logger.Info("Successfully patched");
        }
    }
}
