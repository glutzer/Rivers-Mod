using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace Rivers;

/// <summary>
/// Uses in things like deposits. Does not generate for an entire chunk column.
/// </summary>
public abstract class WorldGenPartial : WorldGenBase
{
    public ICoreServerAPI sapi = null!;
    public LCGRandom chunkRand = null!;

    public abstract int ChunkRange { get; }

    public virtual void ChunkColumnGeneration(IChunkColumnGenerateRequest request)
    {
        int chunkX = request.ChunkX;
        int chunkZ = request.ChunkZ;

        for (int i = -ChunkRange; i <= ChunkRange; i++)
        {
            for (int j = -ChunkRange; j <= ChunkRange; j++)
            {
                int targetChunkX = chunkX + i;
                int targetChunkZ = chunkZ + j;

                IServerChunk[] targetChunks;

                if (i == 0 && j == 0)
                {
                    targetChunks = request.Chunks;
                }
                else
                {
                    // Ensure neighbor chunks are loaded/accessible
                    // Use GetChunkColumn first (non-blocking, returns null if not loaded)
                    targetChunks = sapi.WorldManager.GetChunkColumn(targetChunkX, targetChunkZ);

                    // If not available, try blocking load during worldgen
                    if (targetChunks == null)
                {
                    // Only use BlockingLoadChunkColumn during worldgen phases before RunGame
                    // This forces generation if needed
                    targetChunks = sapi.WorldManager.BlockingLoadChunkColumn(targetChunkX, targetChunkZ);
                }

                if (targetChunks == null) continue;
                }

            GeneratePartial(targetChunks, chunkX, chunkZ, targetChunkX, targetChunkZ);
            }
        }
    }


    public virtual void GeneratePartial(IServerChunk[] chunks, int mainChunkX, int mainChunkZ, int generatingChunkX, int generatingChunkZ)
    {
    }
}
