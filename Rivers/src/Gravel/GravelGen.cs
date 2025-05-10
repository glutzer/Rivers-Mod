using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.ServerMods;

namespace Rivers;

/// <summary>
/// Generates gravel on banks of rivers.
/// </summary>
public class GravelGen : ModStdWorldGen
{
    public Noise gravelNoise = new(0, 0.01f, 2);

    public Dictionary<int, int> gravelMappings = new();

    public ICoreServerAPI sapi = null!;
    public IBlockAccessor blockAccessor = null!;

    public override double ExecuteOrder()
    {
        return 0.45;
    }

    public override void StartServerSide(ICoreServerAPI api)
    {
        base.StartServerSide(api);

        sapi = api;

        if (RiverConfig.Loaded.gravelBeaches)
        {
            sapi.Event.InitWorldGenerator(InitWorldGen, "standard");
            sapi.Event.GetWorldgenBlockAccessor(OnWorldGenBlockAccessor);
            sapi.Event.ChunkColumnGeneration(OnChunkColumnGen, EnumWorldGenPass.Terrain, "standard");
        }
    }

    private void OnWorldGenBlockAccessor(IChunkProviderThread chunkProvider)
    {
        blockAccessor = chunkProvider.GetBlockAccessor(false);
    }

    public int baseSeaLevel;

    public void InitWorldGen()
    {
        RockStrataConfig rockStrata = sapi.Assets.Get("worldgen/rockstrata.json").ToObject<RockStrataConfig>();

        foreach (RockStratum stratum in rockStrata.Variants)
        {
            int stratumId = sapi.World.GetBlock(stratum.BlockCode).BlockId;

            if (gravelMappings.ContainsKey(stratumId)) continue;

            // No gravel for kimberlite/phyllite.
            int gravelId = sapi.World.GetBlock(new AssetLocation("gravel-" + stratum.BlockCode.ToString().Split('-')[1]))?.BlockId ?? stratumId;

            if (gravelId == 0)
            {
                sapi.Logger.Error($"GravelGen: No gravel found for {stratum.BlockCode} ({stratumId}).");
                continue;
            }

            gravelMappings.Add(stratumId, gravelId);
        }

        gravelMappings[0] = 0; // There is an error here that this has already been added.

        baseSeaLevel = TerraGenConfig.seaLevel - 1;
    }

    private void OnChunkColumnGen(IChunkColumnGenerateRequest request)
    {
        IMapChunk mapChunk = request.Chunks[0].MapChunk;

        int chunkX = request.ChunkX;
        int chunkZ = request.ChunkZ;

        int startX = chunkX * 32;
        int startZ = chunkZ * 32;

        ushort[] riverDistance = mapChunk.GetModdata<ushort[]>("riverDistance");
        int[] topRocks = mapChunk.TopRockIdMap;
        ushort[] heightMap = mapChunk.WorldGenTerrainHeightMap;

        // If this chunk doesn't have river distance, it's outside of the range needed to generate gravel.
        if (riverDistance == null) return;

        for (int x = 0; x < 32; x++)
        {
            for (int z = 0; z < 32; z++)
            {
                ushort dist = riverDistance[(z * 32) + x];

                // Below 10 distance, no gravel is generated.
                if (dist > 10) continue;

                int height = heightMap[(z * 32) + x];
                if (height < baseSeaLevel) continue;
                int diff = height - baseSeaLevel;

                double maxDist = gravelNoise.GetNoise(x + startX, z + startZ);
                maxDist *= 10;
                maxDist -= diff;

                if (dist < maxDist)
                {
                    int topRock = topRocks[(z * 32) + x];
                    int gravelId = gravelMappings[topRock];

                    BlockPos pos = new(startX + x, height + 1, startZ + z, 0);

                    blockAccessor.SetBlock(0, pos);
                    pos.Y--;
                    blockAccessor.SetBlock(gravelId, pos);
                    pos.Y--;
                    blockAccessor.SetBlock(gravelId, pos);
                    pos.Y--;
                    blockAccessor.SetBlock(gravelId, pos);
                }
            }
        }
    }
}