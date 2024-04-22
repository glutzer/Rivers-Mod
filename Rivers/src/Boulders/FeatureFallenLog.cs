using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.ServerMods;

namespace Rivers;

/// <summary>
/// 1x1 fallen log.
/// </summary>
public class FeatureFallenLog : PartialFeature
{
    public int[] blocks = new int[3];
    public LCGRandom logRand = new(0);

    public FeatureFallenLog(ICoreServerAPI sapi) : base(sapi)
    {
        blocks[0] = sapi.World.GetBlock(new AssetLocation("game:log-grown-aged-we")).BlockId;
        blocks[1] = sapi.World.GetBlock(new AssetLocation("game:log-grown-aged-ns")).BlockId;
        blocks[2] = sapi.World.GetBlock(new AssetLocation("game:attachingplant-moss")).BlockId;
    }

    public override bool CanGenerate(int localX, int posY, int localZ, ushort riverDistance, bool dry)
    {
        return posY > TerraGenConfig.seaLevel && riverDistance < 50 && !dry;
    }

    public override void Generate(BlockPos blockPos, IServerChunk[] chunkData, LCGRandom rand, Vec2d chunkStart, Vec2d chunkEnd, IBlockAccessor blockAccessor, int rockId, bool dry, ushort[] heightMap)
    {
        int direction = rand.NextInt(2); // 0 - W/E, 1 - N/S.
        int logType;
        BlockPos offset = new(0, 0, 0);
        int length = 5 + rand.NextInt(5);

        //log-grown-aged-we
        //log-grown-aged-ns
        if (direction == 0)
        {
            logType = blocks[0];
            offset.X = length;
        }
        else
        {
            logType = blocks[1];
            offset.Z = length;
        }

        FeatureBoundingBox box = new(blockPos.ToVec3d(), (blockPos + offset).ToVec3d());
        if (!box.SetBounds(chunkStart, chunkEnd)) return;

        bool invalid = false;

        box.ForEachPosition((x, y, z, cPos) =>
        {
            if (invalid || blockAccessor.GetBlock(cPos.AsBlockPos).Replaceable < 2000 || blockAccessor.GetBlock(cPos.AsBlockPos.Add(0, -1, 0)).Replaceable > 2000)
            {
                invalid = true;
            }

            if (invalid) return;

            BlockPos newPos = cPos.AsBlockPos;
            blockAccessor.SetBlock(logType, newPos);

            // Decor type.
            Block decor = sapi.World.GetBlock(blocks[2]);

            logRand.InitPositionSeed(x, z);

            foreach (BlockFacing face in BlockFacing.ALLFACES)
            {
                if (logRand.NextInt(100) > 50)
                {
                    blockAccessor.SetDecor(decor, newPos, face);
                }
            }
        });
    }
}