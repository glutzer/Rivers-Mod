using OpenTK.Mathematics;
using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.ServerMods;

namespace Rivers;

/// <summary>
/// Represents a region of the world where a group of rivers will generate.
/// TODO: A lot of these classes are a mess and have null fields set after their creation. Therefore a lot of redundant null checking is done.
/// TODO: When generating it checks a lot of intersecting lines, use a tree for that instead.
/// </summary>
public class RiverRegion
{
    public ICoreServerAPI sapi;
    public LCGRandom rand = new(0);
    public RiverConfig config;

    public Vector2i regionIndex;
    public Vector2d GlobalRegionStart => new(regionIndex.X * config.RegionSize, regionIndex.Y * config.RegionSize);

    public RiverZone[,] zones;

    // All river starts in this region.
    public List<RiverSegment> riverStarts = new();

    // All separate rivers.
    public List<River> rivers = new();

    // All segments in a tree for quickly fetching the ones needed for testing.
    public RBush<RiverSegment> segmentsForSampling = new();

    // Padded nodes for preventing overlapping generation; also don't need to test intersects with other rivers.
    public RBush<RiverNode> paddedNodeBounds = new();

    /// <summary>
    /// Takes the global chunk position.
    /// Retrieves all segments that must be tested.
    /// </summary>
    public RiverSegment[] GetSegmentsNearChunk(int chunkX, int chunkZ)
    {
        Vector2d chunkStart = new(chunkX * 32, chunkZ * 32);
        chunkStart -= GlobalRegionStart;

        Vector2d chunkEnd = chunkStart + new Vector2d(32);

        return segmentsForSampling.Search(new Envelope(chunkStart.X, chunkStart.Y, chunkEnd.X, chunkEnd.Y)).ToArray();
    }

    public RiverRegion(ICoreServerAPI sapi, int plateX, int plateZ)
    {
        this.sapi = sapi;
        config = RiverConfig.Loaded;
        regionIndex = new Vector2i(plateX, plateZ);

        // Initialize all zones.
        zones = new RiverZone[config.zonesInRegion, config.zonesInRegion];
        GenerateZones(plateX, plateZ);
    }

    public void GenerateZones(int plateX, int plateZ)
    {
        rand.InitPositionSeed(plateX, plateZ);

        // Width/height of region.
        int width = config.zonesInRegion;

        // Initialize all zones and determine if they are ocean.
        GenMaps genMaps = sapi.ModLoader.GetModSystem<GenMaps>();
        int noiseSizeOcean = genMaps.GetField<int>("noiseSizeOcean");
        for (int x = 0; x < width; x++)
        {
            for (int z = 0; z < width; z++)
            {
                RiverZone zone = zones[x, z] = new RiverZone(
                    (x * config.zoneSize) + (config.zoneSize / 2),
                    (z * config.zoneSize) + (config.zoneSize / 2),
                    x,
                    z);

                // This takes a really long time but I'm not going to figure out why.
                SetZoneOceanicity(zone, genMaps, noiseSizeOcean);
            }
        }

        // Use BFS to get zone height based on distance to ocean tiles.
        Queue<RiverZone> queue = new();
        HashSet<Vector2i> visited = new();
        int[] dx = { -1, 1, 0, 0 };
        int[] dy = { 0, 0, -1, 1 };
        for (int x = 0; x < width; x++)
        {
            for (int z = 0; z < width; z++)
            {
                RiverZone zone = zones[x, z];
                if (zone.oceanZone) continue;
                queue.Clear();
                visited.Clear();
                queue.Enqueue(zone);

                double closestOceanTile = double.MaxValue;

                while (queue.Count > 0)
                {
                    RiverZone current = queue.Dequeue();

                    if (current.oceanZone)
                    {
                        // Ocean zone found, calculate distance.
                        double distance = zone.localZoneCenterPosition.DistanceTo(current.localZoneCenterPosition);
                        closestOceanTile = Math.Min(closestOceanTile, distance);
                        continue;
                    }

                    for (int i = 0; i < 4; i++)
                    {
                        int nx = current.xIndex + dx[i];
                        int nz = current.zIndex + dy[i];
                        if (nx < 0 || nx >= width || nz < 0 || nz >= width) continue; // Adjacent tile is out of bounds.
                        if (visited.Contains(new Vector2i(nx, nz))) continue; // Adjacent tile has already been visited.

                        RiverZone adjacent = zones[nx, nz];

                        // If this tile is farther from the ocean than the current closest tile, skip it.
                        if (zone.localZoneCenterPosition.DistanceTo(adjacent.localZoneCenterPosition) > closestOceanTile) continue;

                        visited.Add(new Vector2i(nx, nz));
                        queue.Enqueue(adjacent);
                    }
                }

                zone.oceanDistance = closestOceanTile;

                // It definitely cannot be farther than 2 regions away, to prevent bugs I don't know of.
                zone.oceanDistance = Math.Min(zone.oceanDistance, config.RegionSize * 2);
            }
        }

        // Finally, generate rivers in every zone.
        Queue<GenerationRequest> generationQueue = new();

        for (int x = 0; x < width; x++)
        {
            for (int z = 0; z < width; z++)
            {
                RiverZone zone = zones[x, z];

                // Only generate from oceans.
                if (!zone.oceanZone) continue;

                // Check if the ocean borders a coastal zone on any 8 tiles.
                // This ocean tile will then be turned into a coastal tile.
                for (int xz = -1; xz < 2; xz++)
                {
                    for (int zz = -1; zz < 2; zz++)
                    {
                        if (zones[Math.Clamp(x + xz, 0, config.zonesInRegion - 1), Math.Clamp(z + zz, 0, config.zonesInRegion - 1)].oceanZone == false)
                        {
                            zone.coastalZone = true;
                            break;
                        }
                    }
                }

                // Chance to seed a river at each coastal tile.
                if (zone.coastalZone && rand.NextFloat() < config.riverSpawnChance)
                {
                    River river = new(zone.localZoneCenterPosition);

                    // Attempt to go uphill 8 zones.
                    RiverZone target = FindHighestZone(zone, 8);

                    // Angle river will point towards initially.
                    Vector2d startVector = target.localZoneCenterPosition - zone.localZoneCenterPosition;
                    double startAngle = RiverMath.NormalToDegrees(startVector.Normalized());

                    if (GenerateRiver(startAngle, zone.localZoneCenterPosition, 0, null, river, config.downhillError, generationQueue))
                    {
                        rivers.Add(river);
                    }
                }
            }
        }

        // Rivers will generate 1 segment at a time from each seed point.

        while (generationQueue.Count > 0)
        {
            GenerationRequest request = generationQueue.Dequeue();
            GenerateRiver(request.angle, request.startPos, request.stage, request.parentNode, request.river, request.errorLevel, generationQueue);
        }

        // Now all nodes/segments have been generated, but not lakes.

        List<RiverNode> endNodes = new();
        List<River> smallRivers = new();

        foreach (River river in rivers)
        {
            endNodes.Clear();

            if (river.nodes.Count < config.minNodes)
            {
                smallRivers.Add(river);
                continue;
            }

            river.AssignRiverSizes();

            int radius = 0;

            foreach (RiverNode node in river.nodes)
            {
                foreach (RiverSegment segment in node.segments)
                {
                    int distFromStart = (int)river.StartPos.DistanceTo(segment.endPos);
                    if (distFromStart > radius) radius = distFromStart;
                }

                // Add a lake.
                if (node.end) endNodes.Add(node);
            }

            // Up to 512 away from the endpoint of the farthest segment. (Valley width + distortion + 32 less than 512).
            river.Radius = radius + 512;

            foreach (RiverNode node in endNodes)
            {
                if (rand.NextFloat() < config.lakeChance)
                {
                    AddLake(50, 75, river, node, RiverMath.NormalToDegrees((node.endPos - node.startPos).Normalized()));
                }
            }
        }

        // Remove rivers which are too small.
        foreach (River river in smallRivers) rivers.Remove(river);

        // Now assemble tree.
        foreach (River river in rivers)
        {
            foreach (RiverNode node in river.nodes)
            {
                foreach (RiverSegment segment in node.segments)
                {
                    segment.InitializeBounds();
                    segmentsForSampling.Insert(segment);
                }
            }
        }
    }

    public class GenerationRequest
    {
        public double angle;
        public Vector2d startPos;
        public int stage;
        public RiverNode parentNode;
        public River river;
        public int errorLevel;

        public GenerationRequest(double angle, Vector2d startPos, int stage, RiverNode parentNode, River river, int errorLevel)
        {
            this.angle = angle;
            this.startPos = startPos;
            this.stage = stage;
            this.parentNode = parentNode;
            this.river = river;
            this.errorLevel = errorLevel;
        }
    }

    public bool GenerateRiver(double angle, Vector2d startPos, int stage, RiverNode? parentNode, River river, int errorLevel, Queue<GenerationRequest> generationQueue)
    {
        // If this branch exceeds max nodes, return.
        if (stage > config.maxNodes) return false;

        Vector2d normal = RiverMath.DegreesToNormal(angle);
        Vector2d endPos = startPos + (normal * (config.minLength + rand.NextInt(config.lengthVariation)));

        // Invalid if this intersects any existing pieces.
        bool intersecting = false;

        // For intersection calculation make it slightly longer.
        Vector2d delta = endPos - startPos;
        delta *= 0.5;
        delta += endPos;

        foreach (RiverNode node in river.nodes) // Only check own nodes. Bounds will be checked for other rivers.
        {
            // Don't intersect with self or siblings.
            if (startPos == node.endPos || startPos == node.startPos) continue;

            if (RiverMath.LineIntersects(startPos, delta, node.startPos, node.endPos))
            {
                intersecting = true;
                break;
            }
        }

        // Don't go out of bounds.
        if (endPos.X < 0 || endPos.X > config.RegionSize || endPos.Y < 0 || endPos.Y > config.RegionSize) intersecting = true;

        // Don't go downhill.
        double startDist = GetZoneAt(startPos.X, startPos.Y).oceanDistance;
        double endDist = GetZoneAt(endPos.X, endPos.Y).oceanDistance;
        if (startDist > endDist)
        {
            if (errorLevel == 0) intersecting = true;
            errorLevel--;
        }

        // Don't go into oceans after a couple tries.
        if (GetZoneAt(endPos.X, endPos.Y).oceanZone && stage > 2) intersecting = true;

        // Going into ocean, bending over 90 degrees from original, going farther than target: return.
        if (intersecting) return false;

        // Create envelope for testing R-tree.

        List<RiverNode> overlappingSegments = paddedNodeBounds.Search(RiverNode.GetEnvelope(startPos, endPos)).ToList();
        foreach (RiverNode overlappingNode in overlappingSegments)
        {
            if (overlappingNode.river != river) return false; // Too close to another river, return.
        }

        RiverNode riverNode = new(startPos, endPos, river, parentNode, rand);

        paddedNodeBounds.Insert(riverNode);
        river.nodes.Add(riverNode);

        // A node has come from this river so it's no longer an end.
        if (parentNode != null) parentNode.end = false;

        // Chance for a river to split into 2 rivers
        if (rand.NextFloat() < config.riverSplitChance && parentNode != null)
        {
            double angle1 = angle + (config.minForkAngle + rand.NextInt(config.forkVariation));
            double angle2 = angle - (config.minForkAngle + rand.NextInt(config.forkVariation));

            generationQueue.Enqueue(new GenerationRequest(angle1, endPos, stage + 1, riverNode, river, errorLevel));
            generationQueue.Enqueue(new GenerationRequest(angle2, endPos, stage + 1, riverNode, river, errorLevel));
            return true;
        }
        else
        {
            int sign = 0;
            while (sign == 0) sign = -1 + rand.NextInt(3);

            double angle1 = angle - (rand.NextInt(config.normalAngle) * sign);

            generationQueue.Enqueue(new GenerationRequest(angle1, endPos, stage + 1, riverNode, river, errorLevel));
            return true;
        }
    }

    /// <summary>
    /// Add a "lake" to the end of a river, which is a non-moving river.
    /// Lakes are not tested for overlaps.
    /// </summary>
    public void AddLake(int minSize, int maxSize, River river, RiverNode parent, double angle)
    {
        // Set start of the parent to the min size.
        parent.endSize = config.minSize / 2;

        int lakeSize = rand.NextInt(maxSize - minSize) + minSize;

        Vector2d delta = parent.endPos + (RiverMath.DegreesToNormal(angle) * 100);

        RiverNode lakeNode = new(parent.endPos, delta, river, null, rand, new RiverSegment[1])
        {
            startSize = parent.endSize,
            endSize = lakeSize,
            isLake = true
        };

        // Lake only has 1 segment.
        lakeNode.segments[0] = new(lakeNode.startPos, lakeNode.endPos, lakeNode)
        {
            parent = parent.segments[config.segmentsInRiver - 1], // Parent is the last segment in the parent node.
                                                                  // Child of self.
            parentInvalid = true // Invalid so no curve is done to the lake.
        };

        // Doesn't move.
        lakeNode.speed = 0;

        parent.segments[config.segmentsInRiver - 1].children.Add(lakeNode.segments[0]);

        // This is no longer invalid since it has a child.
        parent.segments[config.segmentsInRiver - 1].parentInvalid = false;

        river.nodes.Add(lakeNode);
    }

    /// <summary>
    /// Get a list of all zones around a zone.
    /// </summary>
    public List<RiverZone> GetZonesAround(int localZoneX, int localZoneZ, int radius = 1)
    {
        List<RiverZone> zonesListerino = new();

        for (int x = -radius; x <= radius; x++)
        {
            for (int z = -radius; z <= radius; z++)
            {
                if (localZoneX + x < 0 || localZoneX + x > config.zonesInRegion - 1 || localZoneZ + z < 0 || localZoneZ + z > config.zonesInRegion - 1) continue;

                zonesListerino.Add(zones[localZoneX + x, localZoneZ + z]);
            }
        }

        return zonesListerino;
    }

    /// <summary>
    /// Get zone at the local position of this region.
    /// </summary>
    public RiverZone GetZoneAt(double localX, double localZ)
    {
        int zx = (int)Math.Clamp(localX / config.zoneSize, 0, config.zonesInRegion - 1);
        int zz = (int)Math.Clamp(localZ / config.zoneSize, 0, config.zonesInRegion - 1);

        return zones[zx, zz];
    }

    /// <summary>
    /// Get highest adjacent zone to this zone.
    /// Hops is the amount of times it may look for a higher zone recursively from the next highest.
    /// </summary>
    public RiverZone FindHighestZone(RiverZone zone, int hops)
    {
        RiverZone[] array = GetZonesAround(zone.xIndex, zone.zIndex, 1).OrderByDescending(x => x.oceanDistance).ToArray();

        return array[0] == zone || hops == 0 ? zone : FindHighestZone(array[0], hops - 1);
    }

    /// <summary>
    /// Determines if a zone is an ocean tile or not and sets it.
    /// </summary>
    public void SetZoneOceanicity(RiverZone zone, GenMaps genMaps, int noiseSizeOcean)
    {
        // Get oceanicity at the center of the zone.
        int oceanPadding = 5;
        int zoneWorldX = (int)(GlobalRegionStart.X + zone.localZoneCenterPosition.X);
        int zoneWorldZ = (int)(GlobalRegionStart.Y + zone.localZoneCenterPosition.Y);
        int chunkX = zoneWorldX / 32;
        int chunkZ = zoneWorldZ / 32;
        int regionX = chunkX / 16;
        int regionZ = chunkZ / 16;

        IntDataMap2D oceanMap = new()
        {
            Size = noiseSizeOcean + (2 * oceanPadding),
            TopLeftPadding = oceanPadding,
            BottomRightPadding = oceanPadding,
            Data = genMaps.GetField<MapLayerBase>("oceanGen").GenLayer((regionX * noiseSizeOcean) - oceanPadding,
                                                                            (regionZ * noiseSizeOcean) - oceanPadding,
                                                                            noiseSizeOcean + (2 * oceanPadding),
                                                                            noiseSizeOcean + (2 * oceanPadding))
        };

        int rlX = chunkX % 16;
        int rlZ = chunkZ % 16;

        int localX = zoneWorldX % 32;
        int localZ = zoneWorldZ % 32;

        float chunkBlockDelta = 1.0f / 16;

        float oceanFactor = (float)oceanMap.InnerSize / 16;
        int oceanUpLeft = oceanMap.GetUnpaddedInt((int)(rlX * oceanFactor), (int)(rlZ * oceanFactor));
        int oceanUpRight = oceanMap.GetUnpaddedInt((int)((rlX * oceanFactor) + oceanFactor), (int)(rlZ * oceanFactor));
        int oceanBotLeft = oceanMap.GetUnpaddedInt((int)(rlX * oceanFactor), (int)((rlZ * oceanFactor) + oceanFactor));
        int oceanBotRight = oceanMap.GetUnpaddedInt((int)((rlX * oceanFactor) + oceanFactor), (int)((rlZ * oceanFactor) + oceanFactor));
        float oceanicityFactor = sapi.WorldManager.MapSizeY / 256 * 0.33333f;

        double zoneOceanicity = GameMath.BiLerp(oceanUpLeft, oceanUpRight, oceanBotLeft, oceanBotRight, localX * chunkBlockDelta, localZ * chunkBlockDelta) * oceanicityFactor;

        if (zoneOceanicity > config.oceanThreshold)
        {
            zone.oceanZone = true;
            zone.oceanDistance = -1;
        }
    }
}