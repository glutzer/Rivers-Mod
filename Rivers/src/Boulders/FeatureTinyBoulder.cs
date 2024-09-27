using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.ServerMods;

namespace Rivers;

public class FeatureTinyBoulder : FeatureRiverBoulder
{
    public FeatureTinyBoulder(ICoreServerAPI sapi) : base(sapi)
    {
    }

    public override void Generate(BlockPos blockPos, IServerChunk[] chunkData, LCGRandom rand, Vec2d chunkStart, Vec2d chunkEnd, IBlockAccessor blockAccessor, int rockId, bool dry, ushort[] heightMap)
    {
        double xSize = hSize + (rand.NextDouble() * hSizeVariance);
        double ySize = hSize + (rand.NextDouble() * hSizeVariance);
        double zSize = hSize + (rand.NextDouble() * hSizeVariance);

        blockPos.Sub(0, (int)(ySize / 2), 0);

        FeatureBoundingBox box = new(blockPos.ToVec3d().Add(-xSize - 2, -ySize - 2, -zSize - 2), blockPos.ToVec3d().Add(xSize + 2, ySize + 2, zSize + 2));

        if (!box.SetBounds(chunkStart, chunkEnd)) return;

        Vec3d centerPos = blockPos.ToVec3d();

        BlockPos tempPos = new(0);

        box.ForEachPosition((x, y, z, cPos) =>
        {
            if (DistanceToEllipsoid(x, y, z, blockPos.X, blockPos.Y, blockPos.Z, xSize, ySize, zSize) <= 1)
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
}