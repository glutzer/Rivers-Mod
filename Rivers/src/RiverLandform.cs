using System.Collections.Generic;
using System.Threading;
using Vintagestory.API.MathTools;
using Vintagestory.ServerMods.NoObf;

namespace Rivers;

public class RiverLandform
{
    // Sea level y key position.
    private const float seaLevelPos = 110 / 256f;

    // How many blocks above sea level the river edge should be.
    private readonly int riverHeight = 5;

    private readonly int mapSizeY;

    private readonly float[] startYKeyPositions;
    private readonly float[] riverYKeyPositions;

    private readonly ThreadLocal<float[]> lerpedYKeyPositionsTL;
    public readonly ThreadLocal<float[]> columnThresholdsOutTL;

    private readonly float[] variantThresholds;

    // Average position of heights above sea level.
    // Returns 0 if nothing above sea level.
    private float GetAverageAboveSeaHeight(LandformVariant variant, int mapSizeY)
    {
        float riverKeyHeight = seaLevelPos + (riverHeight / (float)mapSizeY);
        float[] yKeyPositions = variant.TerrainYKeyPositions;
        float[] yKeyThresholds = variant.TerrainYKeyThresholds;

        List<(float position, float weight)> weightedValues = [];

        float totalWeight = 0f;

        for (int i = 0; i < yKeyPositions.Length; i++)
        {
            if (yKeyPositions[i] <= riverKeyHeight) continue;

            float threshold = yKeyThresholds[i];
            if (threshold == 1f) continue;

            totalWeight += 1f - threshold;
            weightedValues.Add((yKeyPositions[i] * mapSizeY, 1f - threshold));
        }

        float averagePosition = 0f;

        foreach ((float position, float weight) in weightedValues)
        {
            float weightMulti = weight / totalWeight;
            averagePosition += (position - seaLevelPos) * weightMulti;
        }

        if (averagePosition == 0f) averagePosition = riverKeyHeight;

        return averagePosition;
    }

    public RiverLandform(LandformVariant variant, int mapSizeY)
    {
        this.mapSizeY = mapSizeY;

        float averageKeyHeight = GetAverageAboveSeaHeight(variant, mapSizeY);
        float riverKeyHeight = riverHeight / (float)mapSizeY;

        // What to multiply original heights by to squish them.
        float heightMultiplier = riverKeyHeight / averageKeyHeight;

        startYKeyPositions = [.. variant.TerrainYKeyPositions];
        riverYKeyPositions = new float[variant.TerrainYKeyPositions.Length];

        lerpedYKeyPositionsTL = new ThreadLocal<float[]>(() =>
        {
            return new float[startYKeyPositions.Length];
        });

        columnThresholdsOutTL = new ThreadLocal<float[]>(() =>
        {
            return new float[mapSizeY];
        });

        variantThresholds = variant.TerrainYKeyThresholds;
        riverKeyHeight += seaLevelPos;

        for (int i = 0; i < startYKeyPositions.Length; i++)
        {
            if (startYKeyPositions[i] <= riverKeyHeight)
            {
                riverYKeyPositions[i] = riverKeyHeight;
                continue;
            }

            riverYKeyPositions[i] = startYKeyPositions[i] - seaLevelPos;
            riverYKeyPositions[i] *= heightMultiplier;
            riverYKeyPositions[i] += seaLevelPos;
        }
    }

    /// <summary>
    /// Get the values required for lerping a point in a river.
    /// </summary>
    public void ComputeThresholds(float riverLerp)
    {
        float[] lerpedYKeyPositions = lerpedYKeyPositionsTL.Value!;
        float[] columnThresholdsOut = columnThresholdsOutTL.Value!;

        for (int i = 0; i < startYKeyPositions.Length; i++)
        {
            lerpedYKeyPositions[i] = GameMath.Lerp(riverYKeyPositions[i], startYKeyPositions[i], riverLerp);
        }

        float lastThreshold = 1f;
        float lastKeyBlockHeight = 0f;
        int yKeyIndex = -1;

        for (int i = 0; i < mapSizeY; i++)
        {
            if (yKeyIndex + 1 >= variantThresholds.Length)
            {
                columnThresholdsOut[i] = 1f;
                continue;
            }

            if (i >= lerpedYKeyPositions[yKeyIndex + 1] * mapSizeY)
            {
                lastThreshold = variantThresholds[yKeyIndex + 1];
                lastKeyBlockHeight = lerpedYKeyPositions[yKeyIndex + 1] * mapSizeY;
                yKeyIndex++;
            }

            float nextThreshold = 0f;
            float nextKeyBlockHeight = mapSizeY;

            if (yKeyIndex + 1 < variantThresholds.Length)
            {
                nextThreshold = variantThresholds[yKeyIndex + 1];
                nextKeyBlockHeight = lerpedYKeyPositions[yKeyIndex + 1] * mapSizeY;
            }

            float range = nextKeyBlockHeight - lastKeyBlockHeight;

            // Allow for matching key positions.
            if (range == 0f) range = 1f;

            float t = (i - lastKeyBlockHeight) / range;

            columnThresholdsOut[i] = 1f - GameMath.Lerp(lastThreshold, nextThreshold, t);
        }
    }
}