using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace Rivers;

public class FullAlluvialBlock : Block
{
    public Block toRegenerate;

    public override void OnLoaded(ICoreAPI api)
    {
        base.OnLoaded(api);

        toRegenerate = api.World.GetBlock(new AssetLocation("muddygravel"));
    }

    public override void OnBlockBroken(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier = 1)
    {
        base.OnBlockBroken(world, pos, byPlayer, dropQuantityMultiplier);

        world.BlockAccessor.SetBlock(toRegenerate.BlockId, pos);
    }
}