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

        // ONLY process current chunk — moddata already contains river info
        GeneratePartial(request.Chunks, chunkX, chunkZ, chunkX, chunkZ);
    }

    public virtual void GeneratePartial(IServerChunk[] chunks, int mainChunkX, int mainChunkZ, int generatingChunkX, int generatingChunkZ)
    {
    }
}
