using System.Runtime.CompilerServices;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Vintagestory.ServerMods;
using Vintagestory.ServerMods.NoObf;

namespace Rivers;

public abstract class WorldGenBase : ModSystem
{
    public override bool ShouldLoad(EnumAppSide side)
    {
        return side == EnumAppSide.Server;
    }

    public override double ExecuteOrder()
    {
        return 0;
    }

    public GlobalConfig globalConfig = null!;

    public int chunkSize;

    public int regionSize;
    public int regionChunkSize;

    public int mapHeight;
    public int seaLevel;
    public int aboveSeaLevel;

    public int chunkMapHeight;
    public int chunkMapWidth;

    public void LoadGlobalConfig(ICoreServerAPI api)
    {
        chunkSize = 32;

        regionSize = api.World.BlockAccessor.RegionSize; // 512
        regionChunkSize = regionSize / chunkSize; // 512 / 32 = 16

        mapHeight = api.World.BlockAccessor.MapSizeY;
        seaLevel = TerraGenConfig.seaLevel;
        aboveSeaLevel = mapHeight - seaLevel;

        chunkMapHeight = mapHeight / chunkSize; // 256 / 32
        chunkMapWidth = api.WorldManager.MapSizeX / chunkSize;

        globalConfig = api.Assets.Get("game:worldgen/global.json").ToObject<GlobalConfig>();

        Block? block;

        block = api.World.GetBlock(globalConfig.defaultRockCode);
        globalConfig.defaultRockId = block?.BlockId ?? 0;

        block = api.World.GetBlock(globalConfig.waterBlockCode);
        globalConfig.waterBlockId = block?.BlockId ?? 0;

        block = api.World.GetBlock(globalConfig.saltWaterBlockCode);
        globalConfig.saltWaterBlockId = block?.BlockId ?? 0;

        block = api.World.GetBlock(globalConfig.lakeIceBlockCode);
        globalConfig.lakeIceBlockId = block?.BlockId ?? 0;

        block = api.World.GetBlock(globalConfig.lavaBlockCode);
        globalConfig.lavaBlockId = block?.BlockId ?? 0;

        block = api.World.GetBlock(globalConfig.basaltBlockCode);
        globalConfig.basaltBlockId = block?.BlockId ?? 0;

        block = api.World.GetBlock(globalConfig.mantleBlockCode);
        globalConfig.mantleBlockId = block?.BlockId ?? 0;

    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static long GlobalChunkIndex2d(int chunkX, int chunkZ, int chunkMapWidth)
    {
        return ((long)chunkZ * chunkMapWidth) + chunkX;
    }
}