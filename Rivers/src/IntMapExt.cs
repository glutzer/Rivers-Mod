using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace Rivers;

public struct IntMapData
{
    public int UpperLeft;
    public int UpperRight;
    public int BottomLeft;
    public int BottomRight;

    public readonly float LerpForChunk(int localChunkX, int localChunkZ)
    {
        const float chunkBlockDelta = 1f / 32;

        float result = GameMath.BiLerp(UpperLeft, UpperRight, BottomLeft, BottomRight, localChunkX * chunkBlockDelta, localChunkZ * chunkBlockDelta);

        return result;
    }
}

public static class IntMapExt
{
    /// <summary>
    /// Takes global chunk coordinates, returns the values of the 4 corners in that region.
    /// </summary>
    public static IntMapData GetValues(this IntDataMap2D map, int chunkX, int chunkZ)
    {
        if (map.Data.Length == 0) return new IntMapData();

        int rlX = chunkX % RiverGlobals.ChunksPerRegion;
        int rlZ = chunkZ % RiverGlobals.ChunksPerRegion;

        float factor = (float)map.InnerSize / RiverGlobals.ChunksPerRegion;

        IntMapData mapData;

        mapData.UpperLeft = map.GetUnpaddedInt((int)(rlX * factor), (int)(rlZ * factor));
        mapData.UpperRight = map.GetUnpaddedInt((int)((rlX * factor) + factor), (int)(rlZ * factor));
        mapData.BottomLeft = map.GetUnpaddedInt((int)(rlX * factor), (int)((rlZ * factor) + factor));
        mapData.BottomRight = map.GetUnpaddedInt((int)((rlX * factor) + factor), (int)((rlZ * factor) + factor));

        return mapData;
    }
}