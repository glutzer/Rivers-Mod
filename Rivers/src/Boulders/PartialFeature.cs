using System;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.ServerMods;

namespace Rivers;

public class PartialFeature
{
    public ICoreServerAPI sapi;
    public int mapHeight;

    public float hSize;
    public float vSize;

    public float hSizeVariance;
    public float vSizeVariance;

    public float chance;
    public int tries;

    public int seaLevel;

    public Noise noise = null!;

    public PartialFeature(ICoreServerAPI sapi)
    {
        this.sapi = sapi;
        mapHeight = sapi.WorldManager.MapSizeY;
        seaLevel = TerraGenConfig.seaLevel;
    }

    /// <summary>
    /// Start and end coordinates are for the chunk being generated.
    /// </summary>
    public virtual void Generate(BlockPos blockPos, IServerChunk[] chunkData, LCGRandom rand, Vec2d chunkStart, Vec2d chunkEnd, IBlockAccessor blockAccessor, int rockId, bool dry, ushort[] heightMap)
    {

    }

    public virtual bool CanGenerate(int localX, int posY, int localZ, ushort riverDistance, bool dry)
    {
        return true;
    }
}

public class FeatureBoundingBox
{
    public Vec3d start;
    public Vec3d end;

    public FeatureBoundingBox(int startX, int startY, int startZ, int endX, int endY, int endZ)
    {
        start = new Vec3d(startX, startY, startZ);
        end = new Vec3d(endX, endY, endZ);
    }

    public FeatureBoundingBox(Vec3d start, Vec3d end)
    {
        this.start = start;
        this.end = end;
    }

    /// <summary>
    /// Sets bounds to within a chunk and returns if it still exists.
    /// </summary>
    public bool SetBounds(Vec2d chunkStart, Vec2d chunkEnd)
    {
        if (start.X < chunkStart.X) start.X = chunkStart.X;
        if (start.Z < chunkStart.Y) start.Z = chunkStart.Y;

        if (end.X > chunkEnd.X) end.X = chunkEnd.X;
        if (end.Z > chunkEnd.Y) end.Z = chunkEnd.Y;

        if (start.X > end.X) return false;
        if (start.Z > end.Z) return false;

        if (start.Y < 0) start.Y = 0;
        //if (end.Y > 380) end.Y = 380; See if this can be set higher.

        if (start.Y > end.Y) return false;

        return true;
    }

    /// <summary>
    /// Evaluates something based on each world position.
    /// </summary>
    public void ForEachPosition(Action<int, int, int, Vec3d> action)
    {
        Vec3d currentPosition = new();

        for (int x = (int)start.X; x <= end.X; x++)
        {
            for (int z = (int)start.Z; z <= end.Z; z++)
            {
                for (int y = (int)start.Y; y <= end.Y; y++)
                {
                    currentPosition.X = x;
                    currentPosition.Y = y;
                    currentPosition.Z = z;
                    action(x, y, z, currentPosition);
                }
            }
        }
    }
}

[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public class FeatureAttribute : Attribute
{
    public string FeatureName { get; }

    public FeatureAttribute(string featureName)
    {
        FeatureName = featureName;
    }
}