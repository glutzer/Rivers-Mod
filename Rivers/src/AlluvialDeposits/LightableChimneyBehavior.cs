using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace Rivers;

public class LightableChimneyBehavior : Block, IIgnitable
{
    EnumIgniteState IIgnitable.OnTryIgniteStack(EntityAgent byEntity, BlockPos pos, ItemSlot slot, float secondsIgniting)
    {
        return EnumIgniteState.NotIgnitable;
    }

    public EnumIgniteState OnTryIgniteBlock(EntityAgent byEntity, BlockPos pos, float secondsIgniting)
    {
        if (!(secondsIgniting > 2))
        {
            return EnumIgniteState.Ignitable;
        }

        return EnumIgniteState.IgniteNow;
    }

    public void OnTryIgniteBlockOver(EntityAgent byEntity, BlockPos pos, float secondsIgniting, ref EnumHandling handling)
    {
        handling = EnumHandling.PreventDefault;

        api.World.BlockAccessor.ExchangeBlock(api.World.GetBlock(new AssetLocation(Code.ToString().Replace("unlit", "lit"))).Id, pos);
    }
}