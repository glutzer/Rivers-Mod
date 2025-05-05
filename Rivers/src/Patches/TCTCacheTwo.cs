using Vintagestory.Client.NoObf;

namespace Rivers;

public class TCTCacheTwo : TCTCache
{
    public float[]? flowVectors;
    public float riverSpeed;

    public TCTCacheTwo(ChunkTesselator tct) : base(tct)
    {
    }
}