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

    public IWorldGenBlockAccessor blockAccessor;

    public List<PartialFeature> features = new();

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

        FeatureFallenLog log = new(sapi)
        {
            tries = 1,
            chance = 0.2f
        };

        FeatureRiverBoulder riverBoulder = new(sapi)
        {
            hSize = 5,
            hSizeVariance = 5,
            tries = 3,
            chance = 0.1f,
            noise = new Noise(0, 0.05f, 2)
        };

        FeatureTinyBoulder tinyBoulder = new(sapi)
        {
            hSize = 2,
            hSizeVariance = 1,
            tries = 10,
            chance = 0.15f,
            noise = new Noise(0, 0.05f, 2)
        };

        /*
        AlluvialFeature sand = new(sapi, "waterwheels:alluvialblock-blueclay-full")
        {
            hSize = 2,
            hSizeVariance = 5,
            tries = 3,
            chance = 0.1f
        };
        */

        /*
        SurfaceAlluvialFeature sand = new(sapi, "sludgygravel")
        {
            hSize = 2,
            hSizeVariance = 5,
            tries = 5,
            chance = 0.2f
        };
        */

        /*
        if (RiverConfig.Loaded.riverDeposits)
        {
            features.Add(sand);
        }
        */

        if (RiverConfig.Loaded.boulders)
        {
            features.Add(log);
            features.Add(riverBoulder);
            features.Add(tinyBoulder);
        }
    }

    public override void GeneratePartial(IServerChunk[] chunks, int mainChunkX, int mainChunkZ, int generatingChunkX, int generatingChunkZ)
    {
        chunkRand.InitPositionSeed(generatingChunkX, generatingChunkZ);

        IMapChunk mapChunk = sapi.WorldManager.GetMapChunk(generatingChunkX, generatingChunkZ);

        ushort[] ownHeightMap = chunks[0].MapChunk.WorldGenTerrainHeightMap;
        ushort[] heightMap = mapChunk.WorldGenTerrainHeightMap;
        ushort[] riverMap = mapChunk.GetModdata<ushort[]>("riverDistance");

        if (riverMap == null) return;

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

                if (!feature.CanGenerate(randX, pos.Y, randZ, riverMap[(randZ * 32) + randX], dry)) continue;

                int rockId = mapChunk.TopRockIdMap[(randZ * chunkSize) + randX];

                feature.Generate(pos, chunks, chunkRand, new Vec2d(mainChunkX * chunkSize, mainChunkZ * chunkSize), new Vec2d((mainChunkX * chunkSize) + chunkSize - 1, (mainChunkZ * chunkSize) + chunkSize - 1), blockAccessor, rockId, dry, ownHeightMap);
            }
        }
    }
}