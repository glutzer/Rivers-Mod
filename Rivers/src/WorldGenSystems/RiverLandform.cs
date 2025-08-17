using System;
using System.Threading;
using Vintagestory.API.MathTools;
using Vintagestory.ServerMods.NoObf;

namespace Rivers;

public class RiverLandform
{
    private readonly float riverHeight = 0.05f;
    private readonly int mapSizeY;

    private readonly float[] startYKeyPositions;
    private readonly float[] riverYKeyPositions;

    private readonly ThreadLocal<float[]> lerpedYKeyPositionsTL;
    private readonly ThreadLocal<float[]> columnThresholdsOutTL;

    private readonly float[] variantThresholds;

    public RiverLandform(LandformVariant variant, int mapSizeY)
    {
        this.mapSizeY = mapSizeY;
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

        float seaLevelPos = 110 / 256f;
        float lowestZeroPos = 1f;
        float heightModifier = 256f / mapSizeY;
        riverHeight *= heightModifier;

        for (int i = 0; i < variant.TerrainYKeyPositions.Length; i++)
        {
            float threshold = variant.TerrainYKeyThresholds[i];
            if (threshold > 0f) continue;

            lowestZeroPos = variant.TerrainYKeyPositions[i];
            break;
        }

        lowestZeroPos = Math.Max(lowestZeroPos, seaLevelPos + 0.02f);

        float distanceAboveSeaLevel = lowestZeroPos - seaLevelPos;
        float heightMultiplier = riverHeight / distanceAboveSeaLevel;

        for (int i = 0; i < startYKeyPositions.Length; i++)
        {
            if (startYKeyPositions[i] <= seaLevelPos)
            {
                riverYKeyPositions[i] = seaLevelPos;
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
    public float[] GetValuesForLerpLevel(float riverLerp)
    {
        float[] lerpedYKeyPositions = lerpedYKeyPositionsTL.Value!;
        float[] columnThresholdsOut = columnThresholdsOutTL.Value!;

        for (int i = 0; i < startYKeyPositions.Length; i++)
        {
            lerpedYKeyPositions[i] = GameMath.Lerp(startYKeyPositions[i], riverYKeyPositions[i], riverLerp);
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

        return columnThresholdsOut;
    }
}