using HarmonyLib;
using OpenTK.Mathematics;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Vintagestory.ServerMods;
using Vintagestory.ServerMods.NoObf;

namespace Rivers;

public struct WeightedTaper
{
    public float terrainYPos;
    public float weight;
}

public struct ColumnResult
{
    public BitArray columnBlockSolidities;
    public int waterBlockId;
}

public struct ThreadLocalTempData
{
    public double[] lerpedAmplitudes;
    public double[] lerpedThresholds;
    public float[] landformWeights;
}



public class NewGenTerra : ModStdWorldGen
{
    public ICoreServerAPI sapi = null!;

    // Cache of landforms, cleared on init (why?) and when /wgen regen command reloads all the generators.
    public Dictionary<int, LerpedWeightedIndex2DMap> landformMapCache = new();

    // River fields.
    public int AboveSeaLevel => sapi.WorldManager.MapSizeY - TerraGenConfig.seaLevel;
    public Noise valleyNoise = new(0, 0.0008f, 2);
    public Noise floorNoise = new(0, 0.0008f, 1);

    const int chunkSize = 32;
    public const double terrainDistortionMultiplier = 4;
    public const double terrainDistortionThreshold = 40;
    public const double geoDistortionMultiplier = 10;
    public const double geoDistortionThreshold = 10;
    public const double maxDistortionAmount = (55 + 40 + 30 + 10) * SimplexNoiseOctave.MAX_VALUE_2D_WARP;

    public int regionMapSize;
    public float noiseScale;
    public int terrainGenOctaves = 9;

    // Set when first fetching landforms.
    public LandformsWorldProperty landforms = null!;
    public float[][] terrainYThresholds = null!;

    public int riverIndex;
    public LandformVariant riverVariant = null!;

    // Initialized in InitWorldGen.
    public NewNormalizedSimplexFractalNoise terrainNoise = null!;
    public SimplexNoise distort2dx = null!;
    public SimplexNoise distort2dz = null!;
    public NormalizedSimplexNoise geoUpheavalNoise = null!;
    public WeightedTaper[] taperMap = null!;

    public ColumnResult[] columnResults = null!;
    public bool[] layerFullySolid = null!; // We can't use BitArrays for these because code which writes to them is heavily multi-threaded; but anyhow they are only mapSizeY x 4 bytes.
    public bool[] layerFullyEmpty = null!;
    public int[] borderIndicesByCardinal = null!;
    public ThreadLocal<ThreadLocalTempData> tempDataThreadLocal = null!;

    public Type? landType;
    public bool initialized = false;

    public int maxThreads;

    public RiverGenerator riverGenerator = null!; // Can't be in constructor (loaded before config).

    public override void StartServerSide(ICoreServerAPI api)
    {
        sapi = api;

        api.Event.ServerRunPhase(EnumServerRunPhase.ModsAndConfigReady, LoadGamePre);
        api.Event.InitWorldGenerator(InitWorldGen, "standard");
        api.Event.ChunkColumnGeneration(OnChunkColumnGen, EnumWorldGenPass.Terrain, "standard");

        //InitWorldGen(); This was being called here before for some reason.
    }

    /// <summary>
    /// Set sea level before doing anything.
    /// </summary>
    public void LoadGamePre()
    {
        if (sapi.WorldManager.SaveGame.WorldType != "standard") return;
        TerraGenConfig.seaLevel = (int)(0.4313725490196078 * sapi.WorldManager.MapSizeY);
        Climate.Sealevel = TerraGenConfig.seaLevel;
        sapi.WorldManager.SetSeaLevel(TerraGenConfig.seaLevel);
    }

    /// <summary>
    /// Called when 1. the first chunk of the server lifetime is generated or 2. when /wgen regen is called.
    /// </summary>
    public void InitWorldGen()
    {
        // Loads global settings into the Worldgen mod system.
        LoadGlobalConfig(sapi);

        landformMapCache.Clear();

        riverGenerator = new RiverGenerator(sapi);

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

        // How many threads the parallel in the main method will use.
        maxThreads = Math.Clamp(Environment.ProcessorCount - (sapi.Server.IsDedicated ? 4 : 6), 1, sapi.Server.Config.HostedMode ? 4 : 10);

        // Hardcoded values about the terrain map that I don't understand.
        regionMapSize = (int)Math.Ceiling((double)sapi.WorldManager.MapSizeX / sapi.WorldManager.RegionSize);
        noiseScale = Math.Max(1, sapi.WorldManager.MapSizeY / 256f);
        terrainGenOctaves = TerraGenConfig.GetTerrainOctaveCount(sapi.WorldManager.MapSizeY);
        terrainNoise = NewNormalizedSimplexFractalNoise.FromDefaultOctaves(
            terrainGenOctaves, 0.0005 * NewSimplexNoiseLayer.OldToNewFrequency / noiseScale, 0.9, sapi.WorldManager.Seed
        );

        // Noise to distort the terrain after the noise has been sampled.
        distort2dx = new SimplexNoise(
            new double[] { 55, 40, 30, 10 },
            ScaleAdjustedFreqs(new double[] { 1 / 5.0, 1 / 2.50, 1 / 1.250, 1 / 0.65 }, noiseScale),
            sapi.World.Seed + 9876 + 0
        );
        distort2dz = new SimplexNoise(
            new double[] { 55, 40, 30, 10 },
            ScaleAdjustedFreqs(new double[] { 1 / 5.0, 1 / 2.50, 1 / 1.250, 1 / 0.65 }, noiseScale),
            sapi.World.Seed + 9876 + 2
        );

        // Noise where upheaval will happen, raising the height of everything.
        geoUpheavalNoise = new NormalizedSimplexNoise(
            new double[] { 55, 40, 30, 15, 7, 4 },
            ScaleAdjustedFreqs(new double[] {
                    1.0 / 5.5,
                    1.1 / 2.75,
                    1.2 / 1.375,
                    1.2 / 0.715,
                    1.2 / 0.45,
                    1.2 / 0.25
            }, noiseScale),
            sapi.World.Seed + 9876 + 1
        );

        if (!initialized)
        {
            // Only do these things the first time something has been generated.

            if (landType == null) throw new Exception("NoiseLandforms type not found.");

            tempDataThreadLocal = new ThreadLocal<ThreadLocalTempData>(() => new ThreadLocalTempData
            {
                lerpedAmplitudes = new double[terrainGenOctaves],
                lerpedThresholds = new double[terrainGenOctaves],
                landformWeights = new float[landType.GetStaticField<LandformsWorldProperty>("landforms").LandFormsByIndex.Length]
            });

            initialized = true;
        }

        columnResults = new ColumnResult[32 * 32];
        layerFullyEmpty = new bool[sapi.WorldManager.MapSizeY];
        layerFullySolid = new bool[sapi.WorldManager.MapSizeY];
        taperMap = new WeightedTaper[32 * 32];

        for (int i = 0; i < 32 * 32; i++)
        {
            columnResults[i].columnBlockSolidities = new BitArray(sapi.WorldManager.MapSizeY);
        }

        borderIndicesByCardinal = new int[8];
        borderIndicesByCardinal[Cardinal.NorthEast.Index] = ((32 - 1) * 32) + 0;
        borderIndicesByCardinal[Cardinal.SouthEast.Index] = 0 + 0;
        borderIndicesByCardinal[Cardinal.SouthWest.Index] = 0 + 32 - 1;
        borderIndicesByCardinal[Cardinal.NorthWest.Index] = ((32 - 1) * 32) + 32 - 1;
    }

    /// <summary>
    /// Needs to set the "river" land type which is around river borders and will be lerped to based on the river distance and valley strength.
    /// For example: the edge of the river may be 50% of this landform.
    /// </summary>
    private void FetchLandformsAndSetRiverLandform()
    {
        if (landType == null) throw new Exception("NoiseLandforms type not found.");
        landforms = landType.GetStaticField<LandformsWorldProperty>("landforms");

        terrainYThresholds = new float[landforms.LandFormsByIndex.Length][];
        for (int i = 0; i < landforms.LandFormsByIndex.Length; i++)
        {
            // Get river landform and adjust it to new world height.
            if (landforms.LandFormsByIndex[i].Code.ToString() == "game:riverlandform")
            {
                LandformVariant riverLandform = landforms.LandFormsByIndex[i];

                riverIndex = i;
                riverVariant = riverLandform;

                float modifier = 256f / sapi.WorldManager.MapSizeY;

                float seaLevelThreshold = 0.4313725490196078f;
                float blockThreshold = seaLevelThreshold / 110 * modifier;

                riverLandform.TerrainYKeyPositions[0] = seaLevelThreshold; // 100% chance to be atleast sea level.
                riverLandform.TerrainYKeyPositions[1] = seaLevelThreshold + (blockThreshold * 4); // 50% chance to be atleast 4 blocks above sea level.
                riverLandform.TerrainYKeyPositions[2] = seaLevelThreshold + (blockThreshold * 9); // 25% chance to be atleast 6 blocks above sea level.
                riverLandform.TerrainYKeyPositions[3] = seaLevelThreshold + (blockThreshold * 15); // 0% chance to be astleast 10 blocks above sea level.

                // Re-lerp with adjusted heights.
                riverLandform.CallMethod("LerpThresholds", sapi.WorldManager.MapSizeY);
            }

            terrainYThresholds[i] = landforms.LandFormsByIndex[i].TerrainYThresholds;
        }
    }

    private void OnChunkColumnGen(IChunkColumnGenerateRequest request)
    {
        if (landforms == null)
        {
            // This only needs to be done once, but cannot be done during InitWorldGen() because NoiseLandforms.
            // Landforms is sometimes not yet setup at that point (depends on random order of ModSystems registering to events).
            FetchLandformsAndSetRiverLandform();
        }

        Generate(request.Chunks, request.ChunkX, request.ChunkZ);
    }

    private void Generate(IServerChunk[] chunks, int chunkX, int chunkZ)
    {
        IMapChunk mapChunk = chunks[0].MapChunk;

        int rockId = GlobalConfig.defaultRockId;

        RiverConfig riverConfig = RiverConfig.Loaded;

        IntMapData climateMapData = mapChunk.MapRegion.ClimateMap.GetValues(chunkX, chunkZ);
        IntMapData oceanMapData = mapChunk.MapRegion.OceanMap.GetValues(chunkX, chunkZ);
        IntMapData upheavalMapData = mapChunk.MapRegion.UpheavelMap.GetValues(chunkX, chunkZ);

        float oceanicityFac = sapi.WorldManager.MapSizeY / 256 * (1 / 3f); // At a map height of 255, submerge land by up to 85 blocks.

        IntDataMap2D landformMap = mapChunk.MapRegion.LandformMap;
        float chunkPixelSize = landformMap.InnerSize / RiverGlobals.ChunksPerRegion;

        LerpedWeightedIndex2DMap landLerpMap = GetOrLoadCachedLandformMap(chunks[0].MapChunk, chunkX / RiverGlobals.ChunksPerRegion, chunkZ / RiverGlobals.ChunksPerRegion);

        // Terrain octaves.
        float[] landformWeights = tempDataThreadLocal.Value.landformWeights;
        float baseX = chunkX % RiverGlobals.ChunksPerRegion * chunkPixelSize;
        float baseZ = chunkZ % RiverGlobals.ChunksPerRegion * chunkPixelSize;
        GetInterpolatedOctaves(landLerpMap.WeightsAt(baseX, baseZ, landformWeights), out double[] octNoiseX0, out double[] octThX0);
        GetInterpolatedOctaves(landLerpMap.WeightsAt(baseX + chunkPixelSize, baseZ, landformWeights), out double[] octNoiseX1, out double[] octThX1);
        GetInterpolatedOctaves(landLerpMap.WeightsAt(baseX, baseZ + chunkPixelSize, landformWeights), out double[] octNoiseX2, out double[] octThX2);
        GetInterpolatedOctaves(landLerpMap.WeightsAt(baseX + chunkPixelSize, baseZ + chunkPixelSize, landformWeights), out double[] octNoiseX3, out double[] octThX3);
        float[][] terrainYThresholds = this.terrainYThresholds;

        // Store height map in the map chunk.
        ushort[] rainHeightMap = chunks[0].MapChunk.RainHeightMap;
        ushort[] terrainHeightMap = chunks[0].MapChunk.WorldGenTerrainHeightMap;

        int mapSizeY = sapi.WorldManager.MapSizeY;
        int mapSizeYm2 = sapi.WorldManager.MapSizeY - 2;
        int taperThreshold = (int)(mapSizeY * 0.9f);
        double geoUpheavalAmplitude = 255;

        float chunkBlockDelta = 1f / 32;

        float chunkPixelBlockStep = chunkPixelSize * chunkBlockDelta;
        double verticalNoiseRelativeFrequency = 0.5 / TerraGenConfig.terrainNoiseVerticalScale;
        for (int y = 0; y < layerFullySolid.Length; y++) layerFullySolid[y] = true; // Fill with true; later if any block in the layer is non-solid we will set it to false.
        for (int y = 0; y < layerFullyEmpty.Length; y++) layerFullyEmpty[y] = true; // Fill with true; later if any block in the layer is non-solid we will set it to false.

        layerFullyEmpty[mapSizeY - 1] = false; // The top block is always empty (air), leaving space for grass, snow layer etc.

        // Get cached river region.
        int plateX = chunkX / riverConfig.ChunksInRegion;
        int plateZ = chunkZ / riverConfig.ChunksInRegion;
        RiverRegion plate = ObjectCacheUtil.GetOrCreate(sapi, $"{plateX}-{plateZ}", () =>
        {
            return new RiverRegion(sapi, plateX, plateZ);
        });

        // Get rivers that are valid to be tested in this chunk.
        RiverSegment[] validRivers = plate.GetSegmentsNearChunk(chunkX, chunkZ);

        // Fields that will be stored in the chunk data if a river exists.
        float[] flowVectors = new float[32 * 32 * 2];
        ushort[] riverDistance = new ushort[32 * 32];
        bool riverBank = false;
        bool riverInRange = false;

        Vector2d globalRegionStart = plate.GlobalRegionStart;
        RiverSample[,] samples = new RiverSample[32, 32];
        double maxValleyWidth = riverConfig.maxValleyWidth;

        Parallel.For(0, chunkSize * chunkSize, new ParallelOptions() { MaxDegreeOfParallelism = maxThreads }, chunkIndex2d =>
        {
            int localX = chunkIndex2d % chunkSize;
            int localZ = chunkIndex2d / chunkSize;

            int worldX = (chunkX * chunkSize) + localX;
            int worldZ = (chunkZ * chunkSize) + localZ;

            // Sample river data at this position.
            RiverSample sample = samples[localX, localZ] = riverGenerator.SampleRiver(validRivers, worldX - globalRegionStart.X, worldZ - globalRegionStart.Y);

            // Determine if water is flowing there and add it.
            if (sample.flowVectorX > -100)
            {
                flowVectors[chunkIndex2d] = sample.flowVectorX;
                flowVectors[chunkIndex2d + 1024] = sample.flowVectorZ;
                riverBank = true;
            }

            // Log river distance in chunk data.
            ushort shortDistance = (ushort)sample.riverDistance;
            if (shortDistance <= riverConfig.maxValleyWidth * 2)
            {
                riverInRange = true;
            }
            riverDistance[chunkIndex2d] = shortDistance;

            float riverLerp = 1;

            // 1 = edge of valley.
            // 0 - edge of river.
            if (sample.riverDistance < maxValleyWidth)
            {
                // Get raw perlin noise.
                double valley = valleyNoise.GetNoise(worldX, worldZ);

                // Gain for faster transitions.
                valley = Math.Clamp(valley * riverConfig.noiseExpansion, -1, 1);

                // Convert to positive number.
                valley += 1;
                valley /= 2;

                // Valley should be 0-1 now, map it to min/max valley range.
                valley = RiverMath.Map(valley, 0, 1, 1 - riverConfig.valleyStrengthMin, 1 - riverConfig.valleyStrengthMax);

                // When this noise is exactly 0 the edges will artifact.
                if (valley < 0.02) valley = 0.02;

                if (valley < 1)
                {
                    riverLerp = (float)GameMath.Lerp(valley, 1, RiverMath.InverseLerp(samples[localX, localZ].riverDistance, 0, maxValleyWidth));
                    riverLerp = MathF.Pow(riverLerp, 2);
                }
            }

            BitArray columnBlockSolidities = columnResults[chunkIndex2d].columnBlockSolidities;
            columnBlockSolidities.SetAll(false);

            double[] lerpedAmps = tempDataThreadLocal.Value.lerpedAmplitudes;
            double[] lerpedThresh = tempDataThreadLocal.Value.lerpedThresholds;

            float[] columnLandformIndexedWeights = tempDataThreadLocal.Value.landformWeights;
            landLerpMap.WeightsAt(baseX + (localX * chunkPixelBlockStep), baseZ + (localZ * chunkPixelBlockStep), columnLandformIndexedWeights);

            // Weight landform to river.
            if (riverLerp < 1)
            {
                // Multiply all landforms weights by river lerp.
                for (int i = 0; i < columnLandformIndexedWeights.Length; i++)
                {
                    columnLandformIndexedWeights[i] *= riverLerp;
                }

                // Add inverse to river landform, which cannot naturally occur.
                columnLandformIndexedWeights[riverIndex] += 1 - riverLerp;

                for (int i = 0; i < lerpedAmps.Length; i++)
                {
                    lerpedAmps[i] = GameMath.BiLerp(octNoiseX0[i], octNoiseX1[i], octNoiseX2[i], octNoiseX3[i], localX * chunkBlockDelta, localZ * chunkBlockDelta);
                    lerpedThresh[i] = GameMath.BiLerp(octThX0[i], octThX1[i], octThX2[i], octThX3[i], localX * chunkBlockDelta, localZ * chunkBlockDelta);

                    lerpedAmps[i] *= riverLerp;
                    lerpedThresh[i] *= riverLerp;

                    lerpedAmps[i] += riverVariant.TerrainOctaves[i] * (1 - riverLerp);
                    lerpedThresh[i] += riverVariant.TerrainOctaveThresholds[i] * (1 - riverLerp);
                }
            }
            else
            {
                for (int i = 0; i < lerpedAmps.Length; i++)
                {
                    lerpedAmps[i] = GameMath.BiLerp(octNoiseX0[i], octNoiseX1[i], octNoiseX2[i], octNoiseX3[i], localX * chunkBlockDelta, localZ * chunkBlockDelta);
                    lerpedThresh[i] = GameMath.BiLerp(octThX0[i], octThX1[i], octThX2[i], octThX3[i], localX * chunkBlockDelta, localZ * chunkBlockDelta);
                }
            }

            // Create a directional compression effect.
            Vector2d dist = NewDistortionNoise(worldX, worldZ);
            Vector2d distTerrain = ApplyIsotropicDistortionThreshold(dist * terrainDistortionMultiplier, terrainDistortionThreshold, terrainDistortionMultiplier * maxDistortionAmount);

            // Get y distortion from oceanicity and upheaval.
            float upheavalStrength = upheavalMapData.LerpForChunk(chunkX, chunkZ);

            // Weight upheaval to river.
            upheavalStrength *= riverLerp;

            float oceanicity = oceanMapData.LerpForChunk(chunkX, chunkZ) * oceanicityFac;

            Vector2d distGeo = ApplyIsotropicDistortionThreshold(dist * geoDistortionMultiplier, geoDistortionThreshold, geoDistortionMultiplier * maxDistortionAmount);

            float distY = oceanicity + ComputeOceanAndUpheavalDistY(upheavalStrength, worldX, worldZ, distGeo);

            columnResults[chunkIndex2d].waterBlockId = oceanicity > 1 ? GlobalConfig.saltWaterBlockId : GlobalConfig.waterBlockId;

            // Prepare the noise for the entire column.
            NewNormalizedSimplexFractalNoise.ColumnNoise columnNoise = terrainNoise.ForColumn(verticalNoiseRelativeFrequency, lerpedAmps, lerpedThresh, worldX + distTerrain.X, worldZ + distTerrain.Y);
            double noiseBoundMin = columnNoise.BoundMin;
            double noiseBoundMax = columnNoise.BoundMax;

            WeightedTaper wTaper = taperMap[chunkIndex2d];

            float distortedPosYSlide = distY - (int)Math.Floor(distY); // This value will be unchanged throughout the posY loop.

            for (int posY = 1; posY <= mapSizeYm2; posY++)
            {
                // Setup a lerp between threshold values, so that distortY can be applied continuously there.
                StartSampleDisplacedYThreshold(posY + distY, mapSizeYm2, out int distortedPosYBase);

                // Value starts as the landform y threshold.
                double threshold = 0;

                for (int i = 0; i < columnLandformIndexedWeights.Length; i++)
                {
                    float weight = columnLandformIndexedWeights[i];
                    if (weight == 0) continue;

                    // Sample the two values to lerp between. The value of distortedPosYBase is clamped in such a way that this always works.
                    // Underflow and overflow of distortedPosY result in linear extrapolation.

                    threshold += weight * ContinueSampleDisplacedYThreshold(distortedPosYBase, distortedPosYSlide, terrainYThresholds[i]);
                }

                // Geo upheaval modifier for threshold.
                ComputeGeoUpheavalTaper(posY, distY, taperThreshold, geoUpheavalAmplitude, mapSizeY, ref threshold);

                // Often we don't need to calculate the noise.
                if (threshold <= noiseBoundMin)
                {
                    columnBlockSolidities[posY] = true; // Yes terrain block, fill with stone.
                    layerFullyEmpty[posY] = false; // (Thread safe even when this is parallel).
                }
                else if (!(threshold < noiseBoundMax)) // Second case also catches NaN if it were to ever happen.
                {
                    layerFullySolid[posY] = false; // No terrain block (thread safe even when this is parallel).

                    // We can now exit the loop early, because empirical testing shows that once the threshold has exceeded the max noise bound, it never returns to a negative noise value at any higher y value in the same blocks column. This represents air well above the "interesting" part of the terrain. Tested for all world heights in the range 256-1536, tested with arches, overhangs, etc.
                    for (int yAbove = posY + 1; yAbove <= mapSizeYm2; yAbove++) layerFullySolid[yAbove] = false;
                    break;
                }
                else // But sometimes we do.
                {
                    double noiseSign = -NormalizedSimplexNoise.NoiseValueCurveInverse(threshold);
                    noiseSign = columnNoise.NoiseSign(posY, noiseSign);

                    if (noiseSign > 0)  // Solid.
                    {
                        columnBlockSolidities[posY] = true; // Yes, terrain block.
                        layerFullyEmpty[posY] = false; // Thread safe even when this is parallel.
                    }
                    else
                    {
                        layerFullySolid[posY] = false; // Thread safe even when this is parallel.
                    }
                }
            }

            // Don't do this optimization where rivers exist, because an area will be carved.
            if (sample.riverDistance <= 1)
            {
                for (int posY = 1; posY <= mapSizeYm2; posY++)
                {
                    layerFullyEmpty[posY] = false;
                    layerFullySolid[posY] = false;
                }
            }
        });

        IChunkBlocks chunkBlockData = chunks[0].Data;

        // First set all the fully solid layers in bulk, as much as possible.
        chunkBlockData.SetBlockBulk(0, chunkSize, chunkSize, GlobalConfig.mantleBlockId);
        int yBase = 1;
        for (; yBase < mapSizeY - 1; yBase++)
        {
            if (layerFullySolid[yBase])
            {
                if (yBase % chunkSize == 0)
                {
                    chunkBlockData = chunks[yBase / chunkSize].Data;
                }

                chunkBlockData.SetBlockBulk(yBase % chunkSize * chunkSize * chunkSize, chunkSize, chunkSize, rockId);
            }
            else break;
        }

        // Now figure out the top of the mixed layers (above yTop we have fully empty layers, i.e. air).
        int seaLevel = TerraGenConfig.seaLevel;

        int surfaceWaterId = 0;

        // yTop never more than (mapSizeY - 1), but leave the top block layer on the map always as air / for grass.
        int yTop = mapSizeY - 2;

        while (yTop >= yBase && layerFullyEmpty[yTop]) yTop--; // Decrease yTop, we don't need to generate anything for fully empty (air layers).
        if (yTop < seaLevel) yTop = seaLevel;
        yTop++; // Add back one because this is going to be the loop until limit.

        // Then for the rest place blocks column by column (from yBase to yTop only; outside that range layers were already placed below, or are fully air above).
        for (int localZ = 0; localZ < chunkSize; localZ++)
        {
            int worldZ = (chunkZ * chunkSize) + localZ;
            int mapIndex = ChunkMath.ChunkIndex2d(0, localZ);
            for (int localX = 0; localX < chunkSize; localX++)
            {
                ColumnResult columnResult = columnResults[mapIndex];
                int waterId = columnResult.waterBlockId;
                surfaceWaterId = waterId;

                if (yBase < seaLevel && waterId != GlobalConfig.saltWaterBlockId && !columnResult.columnBlockSolidities[seaLevel - 1]) // Should surface water be lake ice? Relevant only for fresh water and only if this particular XZ column has a non-solid block at sea-level.
                {
                    int temp = (GameMath.BiLerpRgbColor(localX * chunkBlockDelta, localZ * chunkBlockDelta, climateMapData.UpperLeft, climateMapData.UpperRight, climateMapData.BottomLeft, climateMapData.BottomRight) >> 16) & 0xFF;
                    float distort = (float)distort2dx.Noise((chunkX * chunkSize) + localX, worldZ) / 20f;
                    float tempF = Climate.GetScaledAdjustedTemperatureFloat(temp, 0) + distort;
                    if (tempF < TerraGenConfig.WaterFreezingTempOnGen) surfaceWaterId = GlobalConfig.lakeIceBlockId;
                }

                terrainHeightMap[mapIndex] = (ushort)(yBase - 1); // Initially set the height maps to values reflecting the top of the fully solid layers.
                rainHeightMap[mapIndex] = (ushort)(yBase - 1);

                chunkBlockData = chunks[yBase / chunkSize].Data;

                RiverSample sample = samples[localX, localZ];

                // Carver.
                if (sample.riverDistance <= 0)
                {
                    int bankFactorBlocks = (int)(sample.bankFactor * AboveSeaLevel);
                    int baseline = seaLevel + riverConfig.heightBoost;

                    for (int posY = yBase; posY < yTop; posY++)
                    {
                        int localY = posY % chunkSize;

                        // For every single block in the chunk, the cost is checking one of these.
                        // This is really laggy and bad Lol.

                        if (columnResult.columnBlockSolidities[posY] && (posY <= baseline - bankFactorBlocks || posY >= baseline + (bankFactorBlocks * riverConfig.topFactor))) // If isSolid.
                        {
                            terrainHeightMap[mapIndex] = (ushort)posY;
                            rainHeightMap[mapIndex] = (ushort)posY;
                            chunkBlockData[ChunkMath.ChunkIndex3d(localX, localY, localZ)] = rockId;
                        }
                        else if (posY < seaLevel)
                        {
                            int blockId;
                            if (posY == seaLevel - 1)
                            {
                                rainHeightMap[mapIndex] = (ushort)posY; // We only need to set the rainHeightMap on the top water block, i.e. seaLevel - 1.
                                blockId = surfaceWaterId;
                            }
                            else
                            {
                                blockId = waterId;
                            }

                            chunkBlockData.SetFluid(ChunkMath.ChunkIndex3d(localX, localY, localZ), blockId);
                        }

                        if (localY == chunkSize - 1)
                        {
                            chunkBlockData = chunks[(posY + 1) / chunkSize].Data; // Set up the next chunksBlockData value.
                        }
                    }
                }
                else
                {
                    for (int posY = yBase; posY < yTop; posY++)
                    {
                        int localY = posY % chunkSize;

                        if (columnResult.columnBlockSolidities[posY]) // If isSolid.
                        {
                            terrainHeightMap[mapIndex] = (ushort)posY;
                            rainHeightMap[mapIndex] = (ushort)posY;
                            chunkBlockData[ChunkMath.ChunkIndex3d(localX, localY, localZ)] = rockId;
                        }
                        else if (posY < seaLevel)
                        {
                            int blockId;
                            if (posY == seaLevel - 1)
                            {
                                rainHeightMap[mapIndex] = (ushort)posY; // We only need to set the rainHeightMap on the top water block, i.e. seaLevel - 1.
                                blockId = surfaceWaterId;
                            }
                            else
                            {
                                blockId = waterId;
                            }

                            chunkBlockData.SetFluid(ChunkMath.ChunkIndex3d(localX, localY, localZ), blockId);
                        }

                        if (localY == chunkSize - 1)
                        {
                            chunkBlockData = chunks[(posY + 1) / chunkSize].Data; // Set up the next chunksBlockData value.
                        }
                    }
                }

                mapIndex++;
            }
        }

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

        // Set the max Y position during initial terrain generation. Lighting uses this.
        ushort yMax = 0;
        for (int i = 0; i < rainHeightMap.Length; i++)
        {
            yMax = Math.Max(yMax, rainHeightMap[i]);
        }
        chunks[0].MapChunk.YMax = yMax;
    }

    /// <summary>
    /// Called once per column.
    /// </summary>
    public LerpedWeightedIndex2DMap GetOrLoadCachedLandformMap(IMapChunk mapchunk, int regionX, int regionZ)
    {
        landformMapCache.TryGetValue((regionZ * regionMapSize) + regionX, out LerpedWeightedIndex2DMap? map);
        if (map != null) return map;

        IntDataMap2D landformMap = mapchunk.MapRegion.LandformMap;

        map = landformMapCache[(regionZ * regionMapSize) + regionX] = new LerpedWeightedIndex2DMap(landformMap.Data, landformMap.Size, TerraGenConfig.landFormSmoothingRadius, landformMap.TopLeftPadding, landformMap.BottomRightPadding);

        return map;
    }

    /// <summary>
    /// Indices is a float array of weights for each landform.
    /// Outputs the interpolated amplitudes and thresholds for each of those landforms based on the weight.
    /// Called 4 times per column, one for each corner.
    /// </summary>
    public void GetInterpolatedOctaves(float[] landformWeights, out double[] octaveAmplitudes, out double[] octaveThresholds)
    {
        octaveAmplitudes = new double[terrainGenOctaves];
        octaveThresholds = new double[terrainGenOctaves];

        // There are N octaves, by default 8.

        for (int octave = 0; octave < terrainGenOctaves; octave++)
        {
            double amplitude = 0;
            double threshold = 0;

            for (int i = 0; i < landformWeights.Length; i++)
            {
                float weight = landformWeights[i];
                if (weight == 0) continue;
                LandformVariant variant = landforms.LandFormsByIndex[i];

                // Weight is 0-1.
                amplitude += variant.TerrainOctaves[octave] * weight;
                threshold += variant.TerrainOctaveThresholds[octave] * weight;
            }

            octaveAmplitudes[octave] = amplitude;
            octaveThresholds[octave] = threshold;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double[] ScaleAdjustedFreqs(double[] vs, float horizontalScale)
    {
        for (int i = 0; i < vs.Length; i++)
        {
            vs[i] /= horizontalScale;
        }

        return vs;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void StartSampleDisplacedYThreshold(float distortedPosY, int mapSizeYm2, out int yBase)
    {
        yBase = Math.Clamp((int)Math.Floor(distortedPosY), 0, mapSizeYm2);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float ContinueSampleDisplacedYThreshold(int yBase, float ySlide, float[] thresholds)
    {
        return GameMath.Lerp(thresholds[yBase], thresholds[yBase + 1], ySlide);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float ComputeOceanAndUpheavalDistY(float upheavalStrength, double worldX, double worldZ, Vector2d distGeo)
    {
        float upheavalNoiseValue = (float)geoUpheavalNoise.Noise((worldX + distGeo.X) / 400, (worldZ + distGeo.Y) / 400) * 0.9f;
        float upheavalMultiplier = Math.Min(0, 0.5f - upheavalNoiseValue);
        return upheavalStrength * upheavalMultiplier;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ComputeGeoUpheavalTaper(double posY, double distY, double taperThreshold, double geoUpheavalAmplitude, double mapSizeY, ref double threshold)
    {
        const double AMPLITUDE_MODIFIER = 40;
        if (posY > taperThreshold && distY < -2)
        {
            double upheavalAmount = GameMath.Clamp(-distY, posY - mapSizeY, posY);
            double ceilingDelta = posY - taperThreshold;
            threshold += ceilingDelta * upheavalAmount / (AMPLITUDE_MODIFIER * geoUpheavalAmplitude);
        }
    }

    // Closely matches the old two-noise distortion in a given seed, but is more fair to all angles.
    public Vector2d NewDistortionNoise(double worldX, double worldZ)
    {
        double noiseX = worldX / 400;
        double noiseZ = worldZ / 400;
        SimplexNoise.NoiseFairWarpVector(distort2dx, distort2dz, noiseX, noiseZ, out double distX, out double distZ);
        return new Vector2d { X = distX, Y = distZ };
    }

    // Cuts off the distortion in a circle rather than a square.
    // Between this and the new distortion noise, this makes the bigger difference.
    public static Vector2d ApplyIsotropicDistortionThreshold(Vector2d dist, double threshold, double maximum)
    {
        double distMagnitudeSquared = (dist.X * dist.X) + (dist.Y * dist.Y);
        double thresholdSquared = threshold * threshold;
        if (distMagnitudeSquared <= thresholdSquared) dist.X = dist.Y = 0;
        else
        {
            // `slide` is 0 to 1 between `threshold` and `maximum` (input vector magnitude).
            double baseCurve = (distMagnitudeSquared - thresholdSquared) / distMagnitudeSquared;
            double maximumSquared = maximum * maximum;
            double baseCurveReciprocalAtMaximum = maximumSquared / (maximumSquared - thresholdSquared);
            double slide = baseCurve * baseCurveReciprocalAtMaximum;

            // Let `slide` be smooth to start.
            slide *= slide;

            // `forceDown` needs to make `dist` zero at `threshold` and `expectedOutputMaximum` at `maximum`.
            double expectedOutputMaximum = maximum - threshold;
            double forceDown = slide * (expectedOutputMaximum / maximum);

            dist *= forceDown;
        }
        return dist;
    }

    public override bool ShouldLoad(EnumAppSide side)
    {
        return side == EnumAppSide.Server;
    }

    public override double ExecuteOrder()
    {
        return 0;
    }
}