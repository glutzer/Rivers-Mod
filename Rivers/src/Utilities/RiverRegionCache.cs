using System;
using System.Collections.Concurrent;
using System.Threading;
using Vintagestory.API.Server;

namespace Rivers;

public static class RiverRegionCache
{
    private static readonly ConcurrentDictionary<(int X, int Z), Lazy<RiverRegion>> regions = [];

    public static RiverRegion GetOrCreate(ICoreServerAPI sapi, int plateX, int plateZ)
    {
        Lazy<RiverRegion> region = regions.GetOrAdd((plateX, plateZ), key =>
            new Lazy<RiverRegion>(
                () => new RiverRegion(sapi, key.X, key.Z),
                LazyThreadSafetyMode.ExecutionAndPublication
            )
        );

        return region.Value;
    }

    public static void Clear()
    {
        regions.Clear();
    }
}