using HarmonyLib;
using System;
using System.Linq;
using System.Reflection;
using Vintagestory.API.Server;
using Vintagestory.ServerMods;
using Vintagestory.ServerMods.NoObf;

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
            system.InitWorldGen();
            return false;
        }
    }

    [HarmonyPatch]
    public static class BrokenReload
    {
        public static MethodBase TargetMethod()
        {
            // Get all assemblies.
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();

            // Get FairPlayGuardian's assembly.
            Assembly survivalAssembly = assemblies.FirstOrDefault(assembly => assembly.GetName().Name == "VSSurvivalMod");

            Type type = survivalAssembly.GetType("Vintagestory.ServerMods.NoiseLandforms");
            MethodInfo method = type.GetMethod("ReloadLandforms", BindingFlags.Public | BindingFlags.Static);
            return method;
        }

        [HarmonyPrefix]
        public static bool Prefix()
        {
            Type landType = null;
            Type[] types = AccessTools.GetTypesFromAssembly(Assembly.GetAssembly(typeof(NoiseBase)));
            foreach (Type type in types)
            {
                if (type.Name == "NoiseLandforms")
                {
                    landType = type;
                    break;
                }
            }
            LandformsWorldProperty landforms = landType.GetStaticField<LandformsWorldProperty>("landforms");

            if (landforms == null) return true;

            return false;
        }
    }
}