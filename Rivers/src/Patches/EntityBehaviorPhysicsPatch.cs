using HarmonyLib;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace Rivers;

/// <summary>
/// Physics for entities in rivers.
/// Adjust speed here.
/// Might need a transpiler for performance?
/// Might need to exclude entities so they don't pile up on edges?
/// </summary>
public class EntityBehaviorPhysicsPatch
{
    // Really laggy but if it's just controlled entities it's fine.
    [HarmonyPatch(typeof(PModuleInLiquid))]
    [HarmonyPatch("DoApply")]
    public static class DoApplyPrefix
    {
        public static bool Prefix(PModuleInLiquid __instance, float dt, Entity entity, EntityPos pos, EntityControls controls)
        {
            if (entity is EntityPlayer player)
            {
                IWorldChunk chunk = entity.Api.World.BlockAccessor.GetChunk((int)pos.X / 32, 0, (int)pos.Z / 32);
                float[]? flowVectors = chunk?.GetModdata<float[]>("flowVectors");

                if (flowVectors != null)
                {
                    float riverSpeed = RiversMod.RiverSpeed;
                    float density = 300f / GameMath.Clamp(entity.MaterialDensity, 750f, 2500f) * (60 * dt); // Calculate density.
                    if (controls.ShiftKey) density /= 2;
                    pos.Motion.Add(flowVectors[ChunkMath.ChunkIndex2d((int)pos.X % 32, (int)pos.Z % 32) * 2] * 0.0025 * density * riverSpeed, 0, flowVectors[ChunkMath.ChunkIndex2d((int)pos.X % 32, (int)pos.Z % 32) * 2 + 1] * 0.0025 * density * riverSpeed);
                }
            }

            return true;
        }
    }

    [HarmonyPatch(typeof(EntityBoat))]
    [HarmonyPatch("updateBoatAngleAndMotion")]
    public static class UpdateBoatAngleAndMotionPostfix
    {
        public static void Postfix(EntityBoat __instance)
        {
            if (__instance.ForwardSpeed != 0.0)
            {
                float riverSpeed = RiversMod.RiverSpeed;

                IWorldChunk chunk = __instance.Api.World.BlockAccessor.GetChunk((int)__instance.Pos.X / 32, 0, (int)__instance.Pos.Z / 32);

                float[]? flowVectors = chunk?.GetModdata<float[]>("flowVectors");

                if (flowVectors != null)
                {
                    __instance.SidedPos.Motion.Add(flowVectors[ChunkMath.ChunkIndex2d((int)__instance.Pos.X % 32, (int)__instance.Pos.Z % 32) * 2] * 0.01 * riverSpeed * 2, 0, flowVectors[ChunkMath.ChunkIndex2d((int)__instance.Pos.X % 32, (int)__instance.Pos.Z % 32) * 2 + 1] * 0.01 * riverSpeed * 2);
                }
            }
        }
    }
}