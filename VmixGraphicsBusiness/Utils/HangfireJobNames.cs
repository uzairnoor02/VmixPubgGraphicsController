using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VmixGraphicsBusiness.Utils
{
    public static class HangfireJobNames
    {
        public const string FetchAndPostDataJob = "FetchAndPostDataJob";

    }

    public static class HelperRedis
    {
        public const string VehicleEliminationsKey = "VehicleEliminations";
        public const string GrenadeEliminationsKey = "GrenadeEliminations";
        public const string AirDropLootedKey = "AirDropLooted";
        public const string FirstBloodKey = "FirstBlood";
        public const string isEliminated = "isEliminated";
    }
}
