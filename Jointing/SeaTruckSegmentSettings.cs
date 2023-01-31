using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace SubnauticaSeaTruckFlexible.Jointing
{
    static class SeaTruckSegmentSettings
    {
        public static Dictionary<TechType, Vector3> SegmentOffsets = new Dictionary<TechType, Vector3>()
        {
            { TechType.SeaTruck, Vector3.back * 0.3835f },
            { TechType.SeaTruckFabricatorModule, Vector3.forward * 0.14f },
            { TechType.SeaTruckStorageModule, Vector3.forward * 0.5f },
            { TechType.SeaTruckSleeperModule, Vector3.forward * 0.12f },
            { TechType.SeaTruckAquariumModule, Vector3.forward * 0.075f },
            { TechType.SeaTruckTeleportationModule, Vector3.forward * 0.6f }
        };
    }
}
