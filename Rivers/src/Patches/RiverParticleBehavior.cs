using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.ServerMods;

namespace Rivers;

public class RiverBlockBehavior : BlockBehavior
{
    public ICoreAPI api;
    private SimpleParticleProperties steamParticles;

    public RiverBlockBehavior(Block block) : base(block)
    {

    }

    public override void OnLoaded(ICoreAPI api)
    {
        this.api = api;
        steamParticles = new(0.5f, 2f, ColorUtil.ColorFromRgba(240, 200, 200, 50), new Vec3d(), new Vec3d(1.0, 1.0, 1.0), new Vec3f(-0.7f, 0.2f, -0.7f), new Vec3f(0.7f, 0.5f, 0.7f), 0.4f, 1f, 0.5f, 1f, EnumParticleModel.Quad)
        {
            WindAffected = true,
            WindAffectednes = 0f,
            OpacityEvolve = EvolvingNatFloat.create(EnumTransformFunction.QUADRATIC, -20f)
        };

        steamParticles.AddPos.Set(1, 0, 1);

        steamParticles.AddVelocity.Set(0, 2, 0);

        steamParticles.ShouldSwimOnLiquid = true;
    }

    // Can receive ticks if there's moving water here.
    public override bool ShouldReceiveClientParticleTicks(IWorldAccessor world, IPlayer byPlayer, BlockPos pos, ref EnumHandling handling)
    {
        if (pos.Y != TerraGenConfig.seaLevel - 1 || RiverConfig.Loaded.clientParticles == false) return false;

        float[] flowVectors = world.BlockAccessor.GetChunk(pos.X / 32, 0, pos.Z / 32)?.GetModdata<float[]>("flowVectors");

        if (flowVectors != null)
        {
            if (flowVectors[(pos.Z % 32 * 32) + (pos.X % 32)] != 0)
            {
                handling = EnumHandling.PreventDefault;
                return true;
            }
        }

        return false;
    }

    public AdvancedParticleProperties particles = new();

    public override void OnAsyncClientParticleTick(IAsyncParticleManager manager, BlockPos pos, float windAffectednessAtPos, float secondsTicking)
    {
        if (api.World.Rand.NextDouble() > 0.995)
        {
            IWorldChunk chunk = api.World.BlockAccessor.GetChunk(pos.X / 32, 0, pos.Z / 32);

            if (chunk == null) return;

            float[] flowVectors = chunk.GetModdata<float[]>("flowVectors");

            if (flowVectors == null) return;

            float x = flowVectors[(pos.Z % 32 * 32) + (pos.X % 32)];
            float z = flowVectors[(pos.Z % 32 * 32) + (pos.X % 32) + 1024];

            x *= RiversMod.RiverSpeed;
            z *= RiversMod.RiverSpeed;

            // 5% chance to spawn particles.
            Random rand = api.World.Rand;

            int sub = rand.Next(100);
            steamParticles.Color = ColorUtil.ColorFromRgba(200, 200, 150 - (sub / 2), 150 - sub);

            steamParticles.MinPos.Set(pos.X + 0.5f, pos.Y + 1f, pos.Z + 0.5f);

            steamParticles.MinVelocity.Set(x * 0.8f, 1f, z * 0.8f);

            steamParticles.ShouldSwimOnLiquid = true;

            manager.Spawn(steamParticles);
        }
    }
}