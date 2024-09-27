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
    // Passive physics for items disabled.
    /*
    [HarmonyPatch(typeof(EntityBehaviorPassivePhysics))]
    [HarmonyPatch("DoPhysics")]
    public static class DoPhysicsPrefix
    {
        [HarmonyPrefix]
        public static bool Prefix(EntityBehaviorPassivePhysics __instance, float dt, EntityPos pos)
        {
            if (__instance.entity.FeetInLiquid || __instance.entity.Swimming)
            {
                IWorldChunk chunk = __instance.entity.Api.World.BlockAccessor.GetChunk((int)pos.X / 32, 0, (int)pos.Z / 32);
                float[] flowVectorsX = chunk?.GetModdata<float[]>("flowVectorsX");

                if (flowVectorsX != null)
                {
                    float density = 300f / GameMath.Clamp(__instance.entity.MaterialDensity, 750f, 2500f) * (60 * dt); // Calculate density.

                    float[] flowVectorsZ = chunk.GetModdata<float[]>("flowVectorsZ");

                    pos.Motion.Add(flowVectorsX[LocalChunkIndex2D((int)pos.X % 32, (int)pos.Z % 32)] * 0.001 * (double)density, 0, flowVectorsZ[LocalChunkIndex2D((int)pos.X % 32, (int)pos.Z % 32)] * 0.001 * (double)density);
                }
            }

            return true;
        }

        public static int LocalChunkIndex2D(int localX, int localZ)
        {
            return localZ * 32 + localX;
        }
    }
    */

    // Really laggy but if it's just controlled entities it's fine.
    [HarmonyPatch(typeof(PModuleInLiquid))]
    [HarmonyPatch("DoApply")]
    public static class DoApplyPrefix
    {
        public static bool Prefix(PModuleInLiquid __instance, float dt, Entity entity, EntityPos pos, EntityControls controls)
        {
            if (entity.Swimming && entity.Alive)
            {
                __instance.CallMethod("HandleSwimming", dt, entity, pos, controls);
            }

            Block block = entity.World.BlockAccessor.GetBlock((int)pos.X, (int)pos.Y, (int)pos.Z, BlockLayersAccess.Fluid);
            if (block.PushVector != null)
            {
                if (block.PushVector.Y >= 0 || !entity.World.BlockAccessor.IsSideSolid((int)pos.X, (int)pos.Y - 1, (int)pos.Z, BlockFacing.UP))
                {
                    pos.Motion.Add(block.PushVector);
                }
            }

            if (entity is EntityPlayer)
            {
                IWorldChunk chunk = entity.Api.World.BlockAccessor.GetChunk((int)pos.X / 32, 0, (int)pos.Z / 32);
                float[] flowVectors = chunk?.GetModdata<float[]>("flowVectors");

                if (flowVectors != null)
                {
                    float riverSpeed = RiversMod.RiverSpeed;

                    float density = 300f / GameMath.Clamp(entity.MaterialDensity, 750f, 2500f) * (60 * dt); // Calculate density.

                    if (controls.ShiftKey) density /= 2;

                    pos.Motion.Add(flowVectors[LocalChunkIndex2D((int)pos.X % 32, (int)pos.Z % 32)] * 0.0025 * density * riverSpeed, 0, flowVectors[LocalChunkIndex2D((int)pos.X % 32, (int)pos.Z % 32) + 1024] * 0.0025 * density * riverSpeed);
                }
            }

            return false;
        }

        public static int LocalChunkIndex2D(int localX, int localZ)
        {
            return (localZ * 32) + localX;
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

                IWorldChunk chunk = __instance.Api.World.BlockAccessor.GetChunk((int)__instance.SidedPos.X / 32, 0, (int)__instance.SidedPos.Z / 32);

                float[] flowVectors = chunk?.GetModdata<float[]>("flowVectors");

                if (flowVectors != null)
                {
                    __instance.SidedPos.Motion.Add(flowVectors[RiverMath.ChunkIndex2d((int)__instance.SidedPos.X % 32, (int)__instance.SidedPos.Z % 32)] * 0.01 * riverSpeed * 2, 0, flowVectors[RiverMath.ChunkIndex2d((int)__instance.SidedPos.X % 32, (int)__instance.SidedPos.Z % 32) + 1024] * 0.01 * riverSpeed * 2);
                }
            }
        }
    }
}