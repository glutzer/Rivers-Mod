using System.Runtime.CompilerServices;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Vintagestory.ServerMods;
using Vintagestory.ServerMods.NoObf;

namespace Rivers;

public abstract class WorldGenBase : ModSystem
{
    public override bool ShouldLoad(EnumAppSide side) => side == EnumAppSide.Server;
    public override double ExecuteOrder() => 0;

    public GlobalConfig globalConfig;

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
        chunkSize = api.World.BlockAccessor.ChunkSize; // 32

        regionSize = api.World.BlockAccessor.RegionSize; // 512
        regionChunkSize = regionSize / chunkSize; // 512 / 32 = 16

        mapHeight = api.World.BlockAccessor.MapSizeY;
        seaLevel = TerraGenConfig.seaLevel;
        aboveSeaLevel = mapHeight - seaLevel;

        chunkMapHeight = mapHeight / chunkSize; // 256 / 32
        chunkMapWidth = api.WorldManager.MapSizeX / chunkSize;

        globalConfig = api.Assets.Get("game:worldgen/global.json").ToObject<GlobalConfig>();

        globalConfig.defaultRockId = api.World.GetBlock(globalConfig.defaultRockCode).BlockId;
        globalConfig.waterBlockId = api.World.GetBlock(globalConfig.waterBlockCode).BlockId;
        globalConfig.saltWaterBlockId = api.World.GetBlock(globalConfig.saltWaterBlockCode).BlockId;
        globalConfig.lakeIceBlockId = api.World.GetBlock(globalConfig.lakeIceBlockCode).BlockId;
        globalConfig.lavaBlockId = api.World.GetBlock(globalConfig.lavaBlockCode).BlockId;
        globalConfig.basaltBlockId = api.World.GetBlock(globalConfig.basaltBlockCode).BlockId;
        globalConfig.mantleBlockId = api.World.GetBlock(globalConfig.mantleBlockCode).BlockId;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int LocalChunkIndex2D(int localX, int localZ)
    {
        return (localZ * chunkSize) + localX;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int LocalChunkIndex3D(int localX, int localY, int localZ)
    {
        return (((localY * chunkSize) + localZ) * chunkSize) + localX;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static long GlobalChunkIndex2D(int chunkX, int chunkZ, int chunkMapWidth)
    {
        return ((long)chunkZ * chunkMapWidth) + chunkX;
    }
}