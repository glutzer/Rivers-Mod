using HarmonyLib;
using System;
using System.Collections.Generic;
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
            // Forward original init to the new one...
            // In the new one, make sure it's ok to call everything when doing /wgen regen.
            ICoreServerAPI api = __instance.GetField<ICoreServerAPI>("api");
            NewGenTerra newSystem = api.ModLoader.GetModSystem<NewGenTerra>();
            newSystem.InitWorldGen();
            return false;
        }
    }

    /// <summary>
    /// Changes the landform reload to only reload IF the landforms have not been loaded yet.
    /// </summary>
    [HarmonyPatch]
    public static class BrokenReload
    {
        public static MethodBase TargetMethod()
        {
            // Get all assemblies.
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();

            Assembly survivalAssembly = assemblies.FirstOrDefault(assembly => assembly.GetName().Name == "VSSurvivalMod")!;
            Type type = survivalAssembly.GetType("Vintagestory.ServerMods.NoiseLandforms")!;
            MethodInfo method = type.GetMethod("LoadLandforms", BindingFlags.Public | BindingFlags.Static)!;
            return method;
        }

        [HarmonyPrefix]
        public static bool Prefix(ICoreServerAPI api)
        {
            Type landType = null!;
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

            if (landforms == null)
            {
                //Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
                //Assembly survivalAssembly = assemblies.FirstOrDefault(assembly => assembly.GetName().Name == "VSSurvivalMod")!;
                //Type type = survivalAssembly.GetType("Vintagestory.ServerMods.NoiseLandforms")!;
                //MethodInfo method = type.GetMethod("LoadLandforms", BindingFlags.Public | BindingFlags.Static)!;

                //// Invoke the static method with api as the first parameter.
                //method.Invoke(null, new object[] { api });
                return true;
            }

            // Don't call original method.
            return false;
        }
    }

    [HarmonyPatch]
    public static class ScalePatch
    {
        public static MethodBase TargetMethod()
        {
            // Get all assemblies.
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();

            Assembly survivalAssembly = assemblies.FirstOrDefault(assembly => assembly.GetName().Name == "VSSurvivalMod")!;
            Type type = survivalAssembly.GetType("Vintagestory.ServerMods.NoiseOcean")!;

            // Get the constructor method.
            ConstructorInfo method = type.GetConstructor(new Type[] { typeof(long), typeof(float), typeof(float) })!;

            return method;
        }

        [HarmonyPostfix]
        public static void Postfix(NoiseBase __instance, float scale)
        {
            __instance.SetField("scale", scale * RiverConfig.Loaded.landScaleMultiplier);
        }
    }
}