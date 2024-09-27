using HarmonyLib;
using Vintagestory.GameContent;

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
}