using HarmonyLib;
using Vintagestory.API.Server;
using Vintagestory.ServerMods;

namespace Rivers;

public class DisableGenTerra
{
    [HarmonyPatch(typeof(GenTerra))]
    [HarmonyPatch("StartServerSide")]
    public static class GenTerraDisable
    {
        [HarmonyPrefix]
        public static bool Prefix(GenTerra __instance, ICoreServerAPI api)
        {
            __instance.SetField("api", api);
            return false;
        }
    }

    [HarmonyPatch(typeof(GenTerra))]
    [HarmonyPatch("initWorldGen")]
    public static class RegenChunksDisable
    {
        [HarmonyPrefix]
        public static bool Prefix(GenTerra __instance)
        {
            ICoreServerAPI api = __instance.GetField<ICoreServerAPI>("api");
            NewGenTerra system = api.ModLoader.GetModSystem<NewGenTerra>();
            if (!system.initialized) system.InitWorldGen();
            return false;
        }
    }
}