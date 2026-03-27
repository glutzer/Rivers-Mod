using HarmonyLib;
using System;
using System.Reflection;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Vintagestory.ServerMods;
using Vintagestory.ServerMods.NoObf;

namespace Rivers;

public class RiversApi : ModSystem
{
    /// <summary>
    /// Should rivers apply the block layer patches, which lower desert sea level around rivers?
    /// </summary>
    public static bool ApplyBlockLayerPatches { get; set; } = true;

    /// <summary>
    /// Should all base terrain generation be overridden by another mod?
    /// </summary>
    public static bool TurnOffGeneration { get; set; } = false;

    private static RiverGenerator riverGenerator = null!;

    internal static Noise valleyNoise = new(0, 0.0008f, 2);
    internal static Noise floorNoise = new(0, 0.0008f, 1);

    public static RiversApi? Instance { get; private set; }
    private ICoreAPI api = null!;
    private ICoreServerAPI sapi = null!;
    private Type? landType;
    private int riverIndex = -1;

    public override double ExecuteOrder()
    {
        return 0;
    }

    /// <summary>
    /// Create the river landform type.
    /// </summary>
    public LandformVariant CreateRiverVariant()
    {
        // Get the NoiseLandforms type for reflection later.
        Type[] types = AccessTools.GetTypesFromAssembly(Assembly.GetAssembly(typeof(NoiseBase)));
        foreach (Type type in types)
        {
            if (type.Name == "NoiseLandforms")
            {
                landType = type;
                break;
            }
        }

        LandformVariant riverVariant = new()
        {
            Weight = 0,
            Code = "riverlandform",
            TerrainOctaves = [0, 0, 0, 0, 0, 1, 0, 0, 0],
            TerrainOctaveThresholds = [0, 0, 0, 0, 0, 0, 0, 0, 0],
            TerrainYKeyPositions = [0.43f, 0.44f, 0.45f, 0.46f],
            TerrainYKeyThresholds = [1.000f, 0.500f, 0.250f, 0.000f],
            HexColor = "#79E02E"
        };

        // Set river variant.
        float modifier = 256f / sapi.WorldManager.MapSizeY;

        float seaLevelThreshold = 0.4313725490196078f;
        float blockThreshold = seaLevelThreshold / 110f * modifier;

        riverVariant.TerrainYKeyPositions[0] = seaLevelThreshold; // 100% chance to be atleast sea level.
        riverVariant.TerrainYKeyPositions[1] = seaLevelThreshold + (blockThreshold * 4f); // 50% chance to be atleast 4 blocks above sea level.
        riverVariant.TerrainYKeyPositions[2] = seaLevelThreshold + (blockThreshold * 9f); // 25% chance to be atleast 6 blocks above sea level.
        riverVariant.TerrainYKeyPositions[3] = seaLevelThreshold + (blockThreshold * 15f); // 0% chance to be astleast 10 blocks above sea level.
        riverVariant.Init(sapi.WorldManager, 0);

        // Re-lerp with adjusted heights.
        riverVariant.CallMethod("LerpThresholds", sapi.WorldManager.MapSizeY);

        return riverVariant;
    }

    /// <summary>
    /// If you're using worldgen similar to GenTerra, set up a few of the landform fields to have an additional river landform at the beginning.
    /// Adds the river variant to the end for sampling.
    /// </summary>
    public void SetupRiverLandforms(ref float[][] terrainYThresholds)
    {
        LandformVariant riverVariant = CreateRiverVariant();

        if (landType == null) return;

        LandformsWorldProperty landforms = landType.GetStaticField<LandformsWorldProperty>("landforms");

        terrainYThresholds = new float[landforms.LandFormsByIndex.Length + 1][];
        for (int i = 0; i < landforms.LandFormsByIndex.Length; i++)
        {
            terrainYThresholds[i] = landforms.LandFormsByIndex[i].TerrainYThresholds;
        }

        riverIndex = terrainYThresholds.Length - 1; // The last index is the river landform.

        terrainYThresholds[riverIndex] = riverVariant.TerrainYThresholds;
    }

    /// <summary>
    /// Modify the landform weights to be weighted towards the river based on the sample.
    /// </summary>
    public void ModifyLandformWeights(RiverSample sample, int worldX, int worldZ, float[] columnLandformIndexedWeights)
    {
        if (riverIndex == -1) throw new Exception("Call SetupRiverLandforms at your generator initialization.");

        RiverConfig config = RiverConfig.Loaded;

        // Nothing to modify.
        if (sample.riverDistance >= config.maxValleyWidth) return;

        // Get raw perlin noise.
        double valley = valleyNoise.GetNoise(worldX, worldZ);

        // Gain for faster transitions.
        valley = Math.Clamp(valley * config.noiseExpansion, -1, 1);

        // Convert to positive number.
        valley += 1;
        valley /= 2;

        // Valley should be 0-1 now, map it to min/max valley range.
        valley = RiverMath.Map(valley, 0, 1, 1 - config.valleyStrengthMin, 1 - config.valleyStrengthMax);

        // When this noise is exactly 0 the edges will artifact.
        if (valley < 0.02) valley = 0.02;

        float riverLerp = (float)GameMath.Lerp(valley, 1, RiverMath.InverseLerp(sample.riverDistance, 0, config.maxValleyWidth));
        riverLerp = MathF.Pow(riverLerp, 2f);

        // Multiply all landforms weights by river lerp.
        for (int i = 0; i < columnLandformIndexedWeights.Length; i++)
        {
            columnLandformIndexedWeights[i] *= riverLerp;
        }

        // Add inverse to river landform, which cannot naturally occur.
        columnLandformIndexedWeights[riverIndex] += 1f - riverLerp;
    }

    /// <summary>
    /// All river samples, indexed by X + Z * 32.
    /// Sets river data in chunk column if supplied.
    /// </summary>
    public static RiverSample[] GetRiverSamplesForChunkAndSetChunkRiverData(int chunkX, int chunkZ, IServerChunk[]? chunks)
    {
        RiverSample[] samples = new RiverSample[32 * 32];
        RiverRegion region = Instance!.GetRiverRegion(chunkX, chunkZ);
        RiverSegment[] segmentsToTest = region.GetSegmentsNearChunk(chunkX, chunkZ);

        float[] flowVectors = new float[32 * 32 * 2];
        ushort[] riverDistance = new ushort[32 * 32];
        bool riverBank = false;
        bool riverInRange = false;

        for (int x = 0; x < 32; x++)
        {
            for (int z = 0; z < 32; z++)
            {
                int worldX = chunkX * 32 + x;
                int worldZ = chunkZ * 32 + z;
                int chunkIndex2d = ChunkMath.ChunkIndex2d(worldX % 32, worldZ % 32);

                RiverSample sample = SampleRiver(worldX, worldZ, segmentsToTest, region);

                samples[x + z * 32] = sample;

                if (sample.flowVectorX > -100f)
                {
                    flowVectors[chunkIndex2d] = sample.flowVectorX;
                    flowVectors[chunkIndex2d + 1024] = sample.flowVectorZ;
                    riverBank = true;
                }

                // Log river distance in chunk data.
                ushort shortDistance = (ushort)sample.riverDistance;
                if (shortDistance <= RiverConfig.Loaded.maxValleyWidth * 2)
                {
                    riverInRange = true;
                }
                riverDistance[chunkIndex2d] = shortDistance;
            }
        }

        if (chunks == null) return samples;

        // Store final river data in the chunk. Flow vectors are used for physics and tessellation.
        if (riverBank)
        {
            chunks[0].SetModdata("flowVectors", flowVectors);
        }

        // Atleast one part of the chunk is within valley range, set river distance.
        if (riverInRange)
        {
            chunks[0].MapChunk.SetModdata("riverDistance", riverDistance);
        }

        return samples;
    }

    /// <summary>
    /// At this X/Z sample, and this Y position, should there be no solids here?
    /// </summary>
    public bool ShouldBeCarved(RiverSample sample, int posY)
    {
        if (sample.riverDistance > 0) return false;

        int seaLevel = TerraGenConfig.seaLevel;
        int aboveSeaLevel = sapi.WorldManager.MapSizeY - seaLevel;

        int bankFactorBlocks = (int)(sample.bankFactor * aboveSeaLevel);
        int baseline = seaLevel + RiverConfig.Loaded.heightBoost;

        return posY > baseline - bankFactorBlocks && posY < baseline + (bankFactorBlocks * RiverConfig.Loaded.topFactor);
    }

    /// <summary>
    /// Get a region where rivers should generate, based on the ocean map.
    /// </summary>
    public RiverRegion GetRiverRegion(int chunkX, int chunkZ)
    {
        RiverConfig riverConfig = RiverConfig.Loaded;

        int plateX = chunkX / riverConfig.ChunksInRegion;
        int plateZ = chunkZ / riverConfig.ChunksInRegion;
        RiverRegion plate = ObjectCacheUtil.GetOrCreate(sapi, $"{plateX}-{plateZ}", () =>
        {
            return new RiverRegion(sapi, plateX, plateZ);
        });

        return plate;
    }

    /// <summary>
    /// Retrieves all river segments within range of a chunk. If the valley is touching a chunk it is within range.
    /// </summary>
    public RiverSegment[] GetSegmentsInRangeOfChunk(int chunkX, int chunkZ)
    {
        RiverRegion region = GetRiverRegion(chunkX, chunkZ);
        return region.GetSegmentsNearChunk(chunkX, chunkZ);
    }

    /// <summary>
    /// Sample river depth and distance from edge.
    /// </summary>
    public static RiverSample SampleRiver(int worldX, int worldZ, RiverSegment[] segmentsToTest, RiverRegion region)
    {
        return riverGenerator.SampleRiver(segmentsToTest, worldX - region.GlobalRegionStart.X, worldZ - region.GlobalRegionStart.Y);
    }

    public override void StartPre(ICoreAPI api)
    {
        this.api = api;

        if (api.Side == EnumAppSide.Client) return;

        sapi = (ICoreServerAPI)api;
        riverGenerator = new RiverGenerator(sapi);

        Instance = this;
    }

    public override void Dispose()
    {
        if (api.Side == EnumAppSide.Client) return;

        Instance = null;
        ApplyBlockLayerPatches = true;
        TurnOffGeneration = false;

        riverGenerator = null!;
        riverIndex = -1;
    }
}