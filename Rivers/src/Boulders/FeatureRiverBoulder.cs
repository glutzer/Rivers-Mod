using System;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.ServerMods;

namespace Rivers;

public class FeatureRiverBoulder : PartialFeature
{
    public Block decor;
    public float multi;

    public FeatureRiverBoulder(ICoreServerAPI sapi) : base(sapi)
    {
        decor = sapi.World.GetBlock(new AssetLocation("waterwheels:sheetmoss-down"));
        multi = sapi.WorldManager.MapSizeY / 256f;
    }

    public override void Generate(BlockPos blockPos, IServerChunk[] chunkData, LCGRandom rand, Vec2d chunkStart, Vec2d chunkEnd, IBlockAccessor blockAccessor, int rockId, bool dry, ushort[] heightMap)
    {
        double xSize = hSize + (rand.NextFloat() * hSizeVariance);
        double ySize = hSize;
        double zSize = hSize + (rand.NextFloat() * hSizeVariance);

        if (blockPos.Y < TerraGenConfig.seaLevel) ySize += Math.Min(rand.NextDouble() * (TerraGenConfig.seaLevel - blockPos.Y) * 4, 30 * multi);

        blockPos.Sub(0, (int)(ySize / 2), 0);

        FeatureBoundingBox box = new(blockPos.ToVec3d().Add(-xSize - 2, -ySize - 2, -zSize - 2), blockPos.ToVec3d().Add(xSize + 2, ySize + 2, zSize + 2));

        if (!box.SetBounds(chunkStart, chunkEnd)) return;

        Vec3d centerPos = blockPos.ToVec3d();

        BlockPos tempPos = new();

        box.ForEachPosition((x, y, z, cPos) =>
        {
            float value = noise.GetPosNoise(x, y, z);
            if (DistanceToEllipsoid(x, y, z, blockPos.X, blockPos.Y, blockPos.Z, xSize, ySize, zSize) + (value / 2) <= 1)
            {
                tempPos.Set(x, y, z);
                blockAccessor.SetBlock(rockId, tempPos);

                if (y > TerraGenConfig.seaLevel - 3 && !dry)
                {
                    tempPos.Y++;
                    if (blockAccessor.GetBlock(tempPos).Replaceable > 5000) blockAccessor.SetBlock(decor.Id, tempPos);

                    // Remove floating moss.
                    /*
                    tempPos.Y++;
                    if (blockAccessor.GetBlock(tempPos).Id == decor.Id) blockAccessor.SetBlock(0, tempPos);
                    */
                }

                /*
                if (y > TerraGenConfig.seaLevel - 2)
                {
                    foreach (BlockFacing face in BlockFacing.ALLFACES)
                    {
                        blockAccessor.SetDecor(decor, tempPos, face);
                    }
                }
                */
            }
        });
    }

    public override bool CanGenerate(int localX, int posY, int localZ, ushort riverDistance, bool dry)
    {
        return riverDistance <= 0 && posY < TerraGenConfig.seaLevel + 2;
    }

    public static double DistanceToEllipsoid(double worldX, double worldY, double worldZ, double centerX, double centerY, double centerZ, double xSize, double ySize, double zSize)
    {
        double translatedX = worldX - centerX;
        double translatedY = worldY - centerY;
        double translatedZ = worldZ - centerZ;

        double normX = translatedX / xSize;
        double normY = translatedY / ySize;
        double normZ = translatedZ / zSize;

        return (normX * normX) + (normY * normY) + (normZ * normZ);
    }
}