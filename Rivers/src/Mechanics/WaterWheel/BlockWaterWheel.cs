using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent.Mechanics;

namespace Rivers;

public class BlockWaterWheel : BlockMPBase
{
    public BlockFacing powerOutFacing;

    public override void DidConnectAt(IWorldAccessor world, BlockPos pos, BlockFacing face)
    {
    }

    public override bool HasMechPowerConnectorAt(IWorldAccessor world, BlockPos pos, BlockFacing face)
    {
        return face == powerOutFacing || face.Opposite == powerOutFacing; // Provide power to both sides.
    }

    public override void OnLoaded(ICoreAPI api)
    {
        powerOutFacing = BlockFacing.FromCode(Variant["side"]).Opposite;
        base.OnLoaded(api);
    }

    public override bool TryPlaceBlock(IWorldAccessor world, IPlayer byPlayer, ItemStack itemstack, BlockSelection blockSel, ref string failureCode)
    {
        // Check if placing is allowed.
        if (!CanPlaceBlock(world, byPlayer, blockSel, ref failureCode)) return false;

        foreach (BlockFacing face in BlockFacing.HORIZONTALS)
        {
            BlockPos pos = blockSel.Position.AddCopy(face);

            // If there's a mechanical power block at the side.
            if (world.BlockAccessor.GetBlock(pos) is IMechanicalPowerBlock block)
            {

                // If the block can connect to this face.
                if (block.HasMechPowerConnectorAt(world, pos, face.Opposite))
                {
                    Block toPlaceBlock = world.GetBlock(new AssetLocation("waterwheels:" + FirstCodePart() + "-" + Variant["size"] + "-" + face.Opposite.Code));
                    world.BlockAccessor.SetBlock(toPlaceBlock.BlockId, blockSel.Position);

                    block.DidConnectAt(world, pos, face.Opposite);
                    WasPlaced(world, blockSel.Position, face);

                    return true;
                }
            }
        }

        bool placeable = base.TryPlaceBlock(world, byPlayer, itemstack, blockSel, ref failureCode);
        if (placeable)
        {
            WasPlaced(world, blockSel.Position, null);
        }
        return placeable;
    }

    public override bool CanPlaceBlock(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, ref string failureCode)
    {
        BlockFacing facing = blockSel.Face;
        int radius = Attributes["radius"].AsInt();

        BlockPos upper = blockSel.Position.Copy();
        BlockPos lower = blockSel.Position.Copy();

        BlockPos add = facing == BlockFacing.NORTH || facing == BlockFacing.SOUTH ? new BlockPos(1, 0, 0, 0) : new BlockPos(0, 0, 1, 0);

        for (int i = 0; i < radius; i++)
        {
            upper.Add(add).Add(0, 1, 0);
            lower.Sub(add).Sub(0, 1, 0);
        }

        bool waterWheelInArea = false;
        world.BlockAccessor.WalkBlocks(lower, upper, delegate (Block block, int x, int y, int z)
        {
            if (block.Code.Path.StartsWith(FirstCodePart(0)))
            {
                waterWheelInArea = true;
            }
        }, false);

        return !waterWheelInArea && base.CanPlaceBlock(world, byPlayer, blockSel, ref failureCode);
    }
}