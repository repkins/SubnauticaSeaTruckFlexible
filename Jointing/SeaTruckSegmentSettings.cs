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
        public static Dictionary<TechType, Vector3> ConnectorBackOffsets = new Dictionary<TechType, Vector3>()
        {
            { TechType.SeaTruck, Vector3.forward * -0.3785f },
            { TechType.SeaTruckFabricatorModule, Vector3.forward * 0.14f },
            { TechType.SeaTruckStorageModule, Vector3.forward * 0.05f },
            { TechType.SeaTruckSleeperModule, Vector3.forward * 0.12f },
            { TechType.SeaTruckAquariumModule, Vector3.forward * 0.075f },
            { TechType.SeaTruckTeleportationModule, Vector3.forward * 0.148f }
        };

        public static Dictionary<TechType, Vector3> ConnectorFrontOffsets = new Dictionary<TechType, Vector3>()
        {
            { TechType.SeaTruck, Vector3.forward * 0.005f },
            { TechType.SeaTruckFabricatorModule, Vector3.zero },
            { TechType.SeaTruckStorageModule, Vector3.zero },
            { TechType.SeaTruckSleeperModule, Vector3.zero },
            { TechType.SeaTruckAquariumModule, Vector3.zero },
            { TechType.SeaTruckTeleportationModule, Vector3.forward * 0.088f }
        };

        public static Dictionary<TechType, Vector3> ConnectorJointAnchors = new Dictionary<TechType, Vector3>()
        {
            { TechType.SeaTruck, Vector3.zero },
            { TechType.SeaTruckFabricatorModule, Vector3.forward * -0.356f },
            { TechType.SeaTruckStorageModule, Vector3.forward * -0.356f },
            { TechType.SeaTruckSleeperModule, Vector3.forward * -0.356f },
            { TechType.SeaTruckAquariumModule, Vector3.forward * -0.356f },
            { TechType.SeaTruckTeleportationModule, Vector3.forward * -0.356f },
            { TechType.SeaTruckDockingModule, Vector3.zero },
        };
    }
}
