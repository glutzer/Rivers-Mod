using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace Rivers;

public class BEWaterWheel : BlockEntity
{
    public float RotateY { get; private set; }
    public BlockFacing facing;

    public ICoreServerAPI sapi;

    public override void Initialize(ICoreAPI api)
    {
        base.Initialize(api);

        facing = BlockFacing.FromCode(Block.Variant["side"]);

        facing ??= BlockFacing.NORTH;

        RotateY = 0;
        switch (facing.Index)
        {
            case 0:
                RotateY = 180;
                break;
            case 1:
                RotateY = 90;
                break;
            case 3:
                RotateY = 270;
                break;
        }
    }
}