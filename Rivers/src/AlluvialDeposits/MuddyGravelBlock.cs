using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.ServerMods;

namespace Rivers;

public class MuddyGravelBlock : Block
{
    public Noise depositNoise;
    public Noise typeNoise;

    public float growthChanceOnTick = 0.05f;
    public float threshold;

    public int[] types;

    public override void OnLoaded(ICoreAPI api)
    {
        base.OnLoaded(api);

        depositNoise = new Noise(api.World.Seed, 0.02f, 2);
        typeNoise = new Noise(api.World.Seed + 1, 0.001f, 1);
        threshold = 1 - RiverConfig.Loaded.clayDepositFrequency;

        List<int> ids = new()
        {
            api.World.GetBlock(new AssetLocation("waterwheels:alluvialblock-blueclay")).Id,
            api.World.GetBlock(new AssetLocation("waterwheels:alluvialblock-fireclay")).Id
        };

        if (RiverConfig.Loaded.clayExpansion)
        {
            ids.Add(api.World.GetBlock(new AssetLocation("waterwheels:alluvialblock-brownclay")).Id);
            ids.Add(api.World.GetBlock(new AssetLocation("waterwheels:alluvialblock-redclay")).Id);
        }

        types = ids.ToArray();
    }

    public override void OnServerGameTick(IWorldAccessor world, BlockPos pos, object extra = null)
    {
        base.OnServerGameTick(world, pos, extra);

        // Only below sea level and if close to the river.
        IWorldChunk chunk = world.BlockAccessor.GetChunk(pos.X / 32, 0, pos.Z / 32);

        ushort[] riverDistance = chunk.MapChunk.GetModdata<ushort[]>("riverDistance");
        if (riverDistance == null) return;

        bool valid = riverDistance[(pos.Z % 32 * 32) + (pos.X % 32)] < 5;

        ushort[] wGenHeight = chunk.MapChunk.WorldGenTerrainHeightMap;
        if (pos.Y != wGenHeight[(pos.Z % 32 * 32) + (pos.X % 32)] || pos.Y > TerraGenConfig.seaLevel - 1) return;
        if (!valid || world.BlockAccessor.GetBlock(pos.Copy().Add(0, 1, 0)).LiquidLevel != 7) return;

        float deposit = depositNoise.GetPosNoise(pos.X, pos.Z);

        if (deposit > threshold)
        {
            float type = typeNoise.GetPosNoise(pos.X, pos.Z);
            world.BlockAccessor.ExchangeBlock(types[Math.Min((int)(type * types.Length), types.Length - 1)], pos);
        }
    }

    public override bool ShouldReceiveServerGameTicks(IWorldAccessor world, BlockPos pos, Random offThreadRandom, out object extra)
    {
        extra = null;

        if (offThreadRandom.NextDouble() > growthChanceOnTick)
        {
            return false;
        }

        return true;
    }
}

public class InitialClay : ModStdWorldGen
{
    public ICoreServerAPI sapi;

    public IBlockAccessor blockAccessor;

    public MuddyGravelBlock muddyGravel;

    public override double ExecuteOrder()
    {
        return 0.92;
    }

    public override void StartServerSide(ICoreServerAPI api)
    {
        if (TerraGenConfig.DoDecorationPass && RiverConfig.Loaded.riverDeposits)
        {
            sapi = api;
            api.Event.ChunkColumnGeneration(ChunkColumnGeneration, EnumWorldGenPass.Vegetation, "standard");
            api.Event.GetWorldgenBlockAccessor(x =>
            {
                blockAccessor = x.GetBlockAccessor(true);
            });
            muddyGravel = sapi.World.GetBlock(new AssetLocation("muddygravel")) as MuddyGravelBlock;
        }
    }

    public void ChunkColumnGeneration(IChunkColumnGenerateRequest request)
    {
        ushort[] heightMap = request.Chunks[0].MapChunk.WorldGenTerrainHeightMap;

        IServerChunk[] chunks = request.Chunks;

        ushort[] riverDistance = chunks[0].MapChunk.GetModdata<ushort[]>("riverDistance");

        if (riverDistance == null) return;

        for (int x = 0; x < 32; x++)
        {
            for (int z = 0; z < 32; z++)
            {
                int height = heightMap[(z * 32) + x];

                if (chunks[height / 32].Data[ChunkIndex3d(x, height % 32, z)] != muddyGravel.Id) continue;

                TickBlock(new BlockPos((32 * request.ChunkX) + x, height, (32 * request.ChunkZ) + z), riverDistance);
            }
        }
    }

    public void TickBlock(BlockPos pos, ushort[] riverDistance)
    {
        bool valid = riverDistance[(pos.Z % 32 * 32) + (pos.X % 32)] < 5;
        if (pos.Y > TerraGenConfig.seaLevel - 1 || !valid || blockAccessor.GetBlock(pos.Copy().Add(0, 1, 0)).LiquidLevel != 7) return;
        float deposit = muddyGravel.depositNoise.GetPosNoise(pos.X, pos.Z);

        if (deposit > muddyGravel.threshold)
        {
            float type = muddyGravel.typeNoise.GetPosNoise(pos.X, pos.Z);
            blockAccessor.SetBlock(muddyGravel.types[(int)(type * muddyGravel.types.Length)], pos); // Set instead of exchange.
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int ChunkIndex3d(int x, int y, int z)
    {
        return (((y * chunksize) + z) * chunksize) + x;
    }
}