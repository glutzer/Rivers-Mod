using OpenTK.Mathematics;
using System.Collections.Generic;
using Vintagestory.API.Common;

namespace Rivers;

public static class ModDataCache
{
    private static readonly object clientLock = new();
    private static readonly object serverLock = new();

    private static readonly Dictionary<Vector2i, float[]> clientFlowCache = [];
    private static readonly Dictionary<Vector2i, float[]> serverFlowCache = [];

    public static void OnClientExit()
    {
        clientFlowCache.Clear();
    }

    public static void OnServerExit()
    {
        serverFlowCache.Clear();
    }

    public static float[]? GetFlowVectors(IWorldChunk chunk, ICoreAPI api, int chunkX, int chunkZ)
    {
        Vector2i coords = new(chunkX, chunkZ);

        return api.Side.IsClient()
            ? GetFlow(chunk, coords, clientLock, clientFlowCache)
            : GetFlow(chunk, coords, serverLock, serverFlowCache);
    }

    private static float[]? GetFlow(IWorldChunk chunk, Vector2i coords, object objLock, Dictionary<Vector2i, float[]> cache)
    {
        lock (objLock)
        {
            if (cache.Count > 200) cache.Clear();

            if (cache.TryGetValue(coords, out float[]? flowVectors))
            {
                return flowVectors;
            }

            flowVectors = chunk.GetModdata<float[]>("flowVectors");
            if (flowVectors == null) return null;

            cache[coords] = flowVectors;
            return flowVectors;
        }
    }
}