using HarmonyLib;
using System;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;
using Vintagestory.ServerMods;
using Vintagestory.ServerMods.NoObf;

namespace Rivers;

public class SeaPatch
{
    public static float Multiplier { get; set; }

    // Zoom for map.
    [HarmonyPatch(typeof(GuiElementMap))]
    [HarmonyPatch("ZoomAdd")]
    public static class GuiElementMapPrefix
    {
        [HarmonyPrefix]
        public static bool Prefix(GuiElementMap __instance, float zoomDiff, float px, float pz)
        {
            if ((!(zoomDiff < 0f) || !(__instance.ZoomLevel + zoomDiff < 0.25f)) && (!(zoomDiff > 0f) || !(__instance.ZoomLevel + zoomDiff > 10f)))
            {
                __instance.ZoomLevel += zoomDiff;

                if (RiverZoomCommand.Zoomed) __instance.ZoomLevel = 0.06f;

                double zoomRel = 1f / __instance.ZoomLevel;
                double relWidth = (__instance.Bounds.InnerWidth * zoomRel) - __instance.CurrentBlockViewBounds.Width;
                double relHeight = (__instance.Bounds.InnerHeight * zoomRel) - __instance.CurrentBlockViewBounds.Length;
                __instance.CurrentBlockViewBounds.X2 += relWidth;
                __instance.CurrentBlockViewBounds.Z2 += relHeight;
                __instance.CurrentBlockViewBounds.Translate((0.0 - relWidth) * (double)px, 0.0, (0.0 - relHeight) * (double)pz);
                __instance.EnsureMapFullyLoaded();
            }

            return false;
        }
    }

    // Checks for both water and saltwater.
    [HarmonyPatch(typeof(BlockSeaweed))]
    [HarmonyPatch("TryPlaceBlockForWorldGen")]
    public static class SeaweedPrefix
    {
        [HarmonyPrefix]
        public static bool Prefix(BlockSeaweed __instance, ref bool __result, IBlockAccessor blockAccessor, BlockPos pos)
        {
            BlockPos blockPos = pos.DownCopy();
            Block block = blockAccessor.GetBlock(blockPos, 2);
            if (block.LiquidCode != "water" && block.LiquidCode != "saltwater")
            {
                __result = false;
                return false;
            }

            if (Multiplier == 0) Multiplier = (__instance.GetField<ICoreAPI>("api") as ICoreServerAPI).WorldManager.MapSizeY / 256f;

            for (int i = 1; i < 80 * Multiplier; i++)
            {
                blockPos.Down();
                block = blockAccessor.GetBlock(blockPos);

                if (block.Fertility > 0)
                {
                    __instance.CallMethod("PlaceSeaweed", blockAccessor, blockPos, i);

                    __result = true;
                    return false;
                }

                if (block is BlockSeaweed || !block.IsLiquid())
                {
                    __result = false;
                    return false;
                }
            }

            __result = false;
            return false;
        }
    }

    [HarmonyPatch(typeof(BlockSeaweed))]
    [HarmonyPatch("PlaceSeaweed")]
    public static class PlaceSeaweedPrefix
    {
        [HarmonyPrefix]
        public static bool Prefix(BlockSeaweed __instance, IBlockAccessor blockAccessor, BlockPos pos, int depth)
        {
            Random random = __instance.GetField<Random>("random");
            Block[] blocks = __instance.GetField<Block[]>("blocks");

            // 20 instead of 6.
            depth = (int)Math.Min(depth, 40 * Multiplier);
            int toPlace = Math.Min(depth - 1, 1 + random.Next(depth));

            if (blocks == null)
            {
                __instance.SetField("blocks", new Block[2]
                {
                    blockAccessor.GetBlock(__instance.CodeWithParts("section")),
                    blockAccessor.GetBlock(__instance.CodeWithParts("top"))
                });

                blocks = __instance.GetField<Block[]>("blocks");
            }

            while (toPlace-- > 1)
            {
                pos.Up();
                blockAccessor.SetBlock(blocks[0].BlockId, pos);
            }

            pos.Up();
            blockAccessor.SetBlock(blocks[1].BlockId, pos);

            return false;
        }
    }

    // Checks if liquid code is saltwater instead.
    [HarmonyPatch(typeof(BlockPatchConfig))]
    [HarmonyPatch("IsPatchSuitableAt")]
    public static class PatchPrefix
    {
        [HarmonyPrefix]
        public static bool Prefix(BlockPatchConfig __instance, ref bool __result, BlockPatch patch, Block onBlock, int mapSizeY, int climate, int y, float forestRel, float shrubRel)
        {
            if ((patch.Placement == EnumBlockPatchPlacement.NearWater || patch.Placement == EnumBlockPatchPlacement.UnderWater) && onBlock.LiquidCode != "water")
            {
                __result = false;
                return false;
            }

            if ((patch.Placement == EnumBlockPatchPlacement.NearSeaWater || patch.Placement == EnumBlockPatchPlacement.UnderSeaWater) && onBlock.LiquidCode != "saltwater")
            {
                __result = false;
                return false;
            }

            if (forestRel < patch.MinForest || forestRel > patch.MaxForest || shrubRel < patch.MinShrub || forestRel > patch.MaxShrub)
            {
                __result = false;
                return false;
            }

            int rainFall = TerraGenConfig.GetRainFall((climate >> 8) & 0xFF, y);
            float rainNormal = rainFall / 255f;
            if (rainNormal < patch.MinRain || rainNormal > patch.MaxRain)
            {
                __result = false;
                return false;
            }

            int scaledAdjustedTemperature = TerraGenConfig.GetScaledAdjustedTemperature((climate >> 16) & 0xFF, y - TerraGenConfig.seaLevel);
            if (scaledAdjustedTemperature < patch.MinTemp || scaledAdjustedTemperature > patch.MaxTemp)
            {
                __result = false;
                return false;
            }

            float seaLevelNormal = (y - (float)TerraGenConfig.seaLevel) / (mapSizeY - (float)TerraGenConfig.seaLevel);
            if (seaLevelNormal < patch.MinY || seaLevelNormal > patch.MaxY)
            {
                __result = false;
                return false;
            }

            float fertilityNormal = TerraGenConfig.GetFertility(rainFall, scaledAdjustedTemperature, seaLevelNormal) / 255f;
            if (fertilityNormal >= patch.MinFertility)
            {
                __result = fertilityNormal <= patch.MaxFertility;
                return false;
            }

            __result = false;
            return false;
        }
    }
}