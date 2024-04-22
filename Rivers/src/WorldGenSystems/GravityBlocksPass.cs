using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Vintagestory.GameContent;
using Vintagestory.ServerMods;

namespace Rivers;

public class GravityBlocksPass : ModStdWorldGen
{
    public ICoreServerAPI sapi;

    public HashSet<int> unstableBlockIds = new();

    public override bool ShouldLoad(EnumAppSide side)
    {
        return side == EnumAppSide.Server;
    }

    public override double ExecuteOrder()
    {
        // Execute after block layers have been placed.
        return 0.5;
    }

    public override void StartServerSide(ICoreServerAPI api)
    {
        sapi = api;

        if (RiverConfig.Loaded.fixGravityBlocks)
        {
            api.Event.InitWorldGenerator(InitWorldGen, "standard");
            api.Event.ChunkColumnGeneration(OnChunkColumnGen, EnumWorldGenPass.Terrain, "standard");
        }
    }

    public void InitWorldGen()
    {
        // Find all blocks that can fall once. Soil only has this if gravity is enabled.
        foreach (Block block in sapi.World.Blocks)
        {
            if (block.HasBehavior<BlockBehaviorUnstableFalling>())
            {
                unstableBlockIds.Add(block.Id);
            }
        }
    }

    public void OnChunkColumnGen(IChunkColumnGenerateRequest request)
    {
        IServerChunk[] chunks = request.Chunks;
        ushort[] terrainHeightMap = request.Chunks[0].MapChunk.WorldGenTerrainHeightMap;
        int[] topRockIdMap = request.Chunks[0].MapChunk.TopRockIdMap;

        for (int localZ = 0; localZ < 32; localZ++)
        {
            for (int localX = 0; localX < 32; localX++)
            {
                int surfaceLevel = terrainHeightMap[RiverMath.ChunkIndex2d(localX, localZ)];

                int chunkY = surfaceLevel / 32;

                if (unstableBlockIds.Contains(chunks[chunkY].Data[RiverMath.ChunkIndex3d(localX, surfaceLevel % 32, localZ)]))
                {
                    bool illegal = false;

                    int index = 1;

                    int newChunkY;

                    while (true)
                    {
                        newChunkY = (surfaceLevel - index) / 32;

                        int id = chunks[newChunkY].Data[RiverMath.ChunkIndex3d(localX, (surfaceLevel - index) % 32, localZ)];

                        if (unstableBlockIds.Contains(id))
                        {
                            index++;
                            continue;
                        }

                        if (id == 0) illegal = true;

                        break;
                    }

                    // 99% of the time not called.
                    if (illegal)
                    {

                        // Set the air block below to rock.
                        newChunkY = (surfaceLevel - index) / 32;
                        chunks[newChunkY].Data[RiverMath.ChunkIndex3d(localX, (surfaceLevel - index) % 32, localZ)] = topRockIdMap[RiverMath.ChunkIndex2d(localX, localZ)];
                    }
                }
            }
        }
    }
}