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
                    // Get individual chunk, not full column
                    // Returns null if not loaded — check before use
                    IServerChunk? chunk = sapi.WorldManager.GetChunk(targetChunkX, 0, targetChunkZ);
                    if (chunk == null) continue;

                    // Build minimal array for GeneratePartial
                    targetChunks = new IServerChunk[] { chunk };
                }


            GeneratePartial(targetChunks, chunkX, chunkZ, targetChunkX, targetChunkZ);
            }
        }
    }


    public virtual void GeneratePartial(IServerChunk[] chunks, int mainChunkX, int mainChunkZ, int generatingChunkX, int generatingChunkZ)
    {
    }
}
