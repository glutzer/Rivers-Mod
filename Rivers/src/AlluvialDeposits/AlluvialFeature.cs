using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.ServerMods;

namespace Rivers;

public class AlluvialFeature : PartialFeature
{
    public int blockId;

    public AlluvialFeature(ICoreServerAPI sapi, string code) : base(sapi)
    {
        blockId = sapi.World.GetBlock(new AssetLocation(code))?.BlockId ?? 0;
    }

    public override bool CanGenerate(int localX, int posY, int localZ, ushort riverDistance, bool dry)
    {
        return riverDistance < 10;
    }

    public override void Generate(BlockPos blockPos, IServerChunk[] chunkData, LCGRandom rand, Vec2d chunkStart, Vec2d chunkEnd, IBlockAccessor blockAccessor, int rockId, bool dry, ushort[] heightMap)
    {
        float radius = 1;

        double xSize = hSize + (rand.NextFloat() * hSizeVariance);
        double zSize = hSize + (rand.NextFloat() * hSizeVariance);

        FeatureBoundingBox box = new(blockPos.ToVec3d().Add(-xSize - 2, 0, -zSize - 2), blockPos.ToVec3d().Add(xSize + 2, 0, zSize + 2));

        if (!box.SetBounds(chunkStart, chunkEnd)) return;

        Vec3d centerPos = blockPos.ToVec3d();
        centerPos.Y = 0;

        Vec3d tempPos = new();

        box.ForEachPosition((x, y, z, cPos) =>
        {
            int localX = x % 32;
            int localZ = z % 32;

            int height = heightMap[(localZ * 32) + localX];

            tempPos.X = x;
            tempPos.Z = z;

            double distance = RiverMath.DistanceTo(centerPos, tempPos, xSize, zSize);

            if (distance < radius && height < TerraGenConfig.seaLevel - 1)
            {
                BlockPos pos = new(x, height, z, 0);

                blockAccessor.SetBlock(blockId, pos);
            }
        });
    }
}