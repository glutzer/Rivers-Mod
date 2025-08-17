using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.ServerMods;

namespace Rivers;

public class GeneratePartialFeatures : WorldGenPartial
{
    public override double ExecuteOrder()
    {
        return 0.15;
    }

    private bool loaded;

    public IWorldGenBlockAccessor blockAccessor = null!;

    public List<PartialFeature> features = [];

    // Can't be more than 1 because neighbor chunks are required.
    public override int ChunkRange => 1;

    public override void StartServerSide(ICoreServerAPI api)
    {
        sapi = api;

        if (TerraGenConfig.DoDecorationPass)
        {
            sapi.Event.InitWorldGenerator(InitWorldGenerator, "standard");
            sapi.Event.ChunkColumnGeneration(ChunkColumnGeneration, EnumWorldGenPass.Vegetation, "standard");
            sapi.Event.GetWorldgenBlockAccessor(OnWorldGenBlockAccessor);
        }

        chunkRand = new LCGRandom(sapi.World.Seed);
    }

    private void OnWorldGenBlockAccessor(IChunkProviderThread chunkProvider)
    {
        blockAccessor = chunkProvider.GetBlockAccessor(true);
    }

    public void InitWorldGenerator()
    {
        // Load config into the system base.
        LoadGlobalConfig(sapi);

        if (loaded) return; // UHh it inits when doing wgen regen.
        loaded = true;

        FeatureRiverBoulder riverBoulder = new(sapi)
        {
            hSize = 5f,
            hSizeVariance = 5f,
            tries = 3,
            chance = 0.05f,
            noise = new Noise(0, 0.05f, 2)
        };

        FeatureTinyBoulder tinyBoulder = new(sapi)
        {
            hSize = 2f,
            hSizeVariance = 1f,
            tries = 10,
            chance = 0.15f,
            noise = new Noise(0, 0.05f, 2)
        };

        if (RiverConfig.Loaded.boulders)
        {
            features.Add(riverBoulder);
            features.Add(tinyBoulder);
        }
    }

    public override void GeneratePartial(IServerChunk[] chunks, int mainChunkX, int mainChunkZ, int generatingChunkX, int generatingChunkZ)
    {
        chunkRand.InitPositionSeed(generatingChunkX, generatingChunkZ);

        IMapChunk mapChunk = blockAccessor.GetMapChunk(generatingChunkX, generatingChunkZ);

        ushort[] heightMap = mapChunk.WorldGenTerrainHeightMap;
        ushort[] riverDistanceMap = mapChunk.GetModdata<ushort[]>("riverDistance");

        if (riverDistanceMap == null) return;

        int startX = generatingChunkX * chunkSize;
        int startZ = generatingChunkZ * chunkSize;

        // Get 0-255 rain.
        IntDataMap2D climateMap = mapChunk.MapRegion.ClimateMap;
        int regionChunkSize = sapi.WorldManager.RegionSize / chunkSize;
        float cFac = (float)climateMap.InnerSize / regionChunkSize;
        int rlX = generatingChunkX % regionChunkSize;
        int rlZ = generatingChunkZ % regionChunkSize;
        int climateUpLeft = climateMap.GetUnpaddedInt((int)(rlX * cFac), (int)(rlZ * cFac));
        int climateUpRight = climateMap.GetUnpaddedInt((int)((rlX * cFac) + cFac), (int)(rlZ * cFac));
        int climateBotLeft = climateMap.GetUnpaddedInt((int)(rlX * cFac), (int)((rlZ * cFac) + cFac));
        int climateBotRight = climateMap.GetUnpaddedInt((int)((rlX * cFac) + cFac), (int)((rlZ * cFac) + cFac));
        int rain = (GameMath.BiLerpRgbColor(0.5f, 0.5f, climateUpLeft, climateUpRight, climateBotLeft, climateBotRight) >> 8) & 0xFF;
        bool dry = rain < 100;

        BlockPos pos = new(0, 0, 0, 0);

        foreach (PartialFeature feature in features)
        {
            for (int x = 0; x < feature.tries; x++)
            {
                if (chunkRand.NextFloat() >= feature.chance) continue;

                int randX = chunkRand.NextInt(chunkSize);
                int randZ = chunkRand.NextInt(chunkSize);

                pos.X = startX + randX;
                pos.Y = heightMap[(randZ * chunkSize) + randX] + 1;
                pos.Z = startZ + randZ;

                if (!feature.CanGenerate(randX, pos.Y, randZ, riverDistanceMap[(randZ * 32) + randX], dry)) continue;

                int rockId = mapChunk.TopRockIdMap[(randZ * chunkSize) + randX];

                feature.Generate(pos, chunks, chunkRand, new Vec2d(mainChunkX * chunkSize, mainChunkZ * chunkSize), new Vec2d((mainChunkX * chunkSize) + chunkSize - 1, (mainChunkZ * chunkSize) + chunkSize - 1), blockAccessor, rockId, dry, heightMap);
            }
        }
    }
}