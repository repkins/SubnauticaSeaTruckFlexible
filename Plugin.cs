﻿using BepInEx;

namespace SubnauticaSeaTruckFlexible
{
    [BepInPlugin("subnautica.repkins.seatruckflexible", "SeatruckFlexible", "1.0.0.0")]
    public class Plugin : BaseUnityPlugin
    {
        public void Awake()
        {
            MainPatcher.Patch();
        }
    }
}