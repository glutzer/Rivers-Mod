using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Vintagestory.ServerMods;

namespace Rivers;

// Data for large area of rivers.
public class TectonicPlate
{
    public ICoreServerAPI sapi;

    public int plateSize; // Plate size in blocks.
    public Vec2d localPlateCenterPosition = new(); // Local center (plate size / 2).
    public Vec2d globalPlateStart = new();

    public TectonicZone[,] zones; // All regions in the plate.

    public LCGRandom rand;

    // Config.
    public RiverConfig config;

    public List<RiverSegment> riverStarts = new();

    public List<RiverNode> riverNodes = new();

    public List<River> rivers = new();

    public Queue<GenerationRequest> generationQueue = new();

    public TectonicPlate(ICoreServerAPI sapi, int plateX, int plateZ)
    {
        this.sapi = sapi;

        config = RiverConfig.Loaded;

        plateSize = config.zoneSize * config.zonesInPlate;
        localPlateCenterPosition.X = plateSize / 2;
        localPlateCenterPosition.Y = plateSize / 2;

        zones = new TectonicZone[config.zonesInPlate, config.zonesInPlate];
        rand = new LCGRandom(0);

        // Initialize all zones.
        GenerateZones(plateX, plateZ);
    }

    public void GenerateZones(int plateX, int plateZ)
    {
        rand.InitPositionSeed(plateX, plateZ);

        // Width and depth in zones.
        int width = zones.GetLength(0);
        int depth = zones.GetLength(1);

        globalPlateStart.X = plateX * plateSize;
        globalPlateStart.Y = plateZ * plateSize;

        // Calculate if a tile is ocean. If it's land
        for (int x = 0; x < width; x++)
        {
            for (int z = 0; z < depth; z++)
            {
                // Initialize zone with the local point being in the center of the square.
                zones[x, z] = new TectonicZone((x * config.zoneSize) + (config.zoneSize / 2),
                                               (z * config.zoneSize) + (config.zoneSize / 2));

                TectonicZone zone = zones[x, z];
                zone.xIndex = x;
                zone.zIndex = z;

                // Get oceanicity at the center of the zone.
                int oceanPadding = 5;
                int zoneWorldX = (int)(globalPlateStart.X + zones[x, z].localZoneCenterPosition.X);
                int zoneWorldZ = (int)(globalPlateStart.Y + zones[x, z].localZoneCenterPosition.Y);
                int chunkX = zoneWorldX / 32;
                int chunkZ = zoneWorldZ / 32;
                int regionX = chunkX / 16;
                int regionZ = chunkZ / 16;

                GenMaps genMaps = sapi.ModLoader.GetModSystem<GenMaps>();
                int noiseSizeOcean = genMaps.GetField<int>("noiseSizeOcean");

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
                    zone.ocean = true;
                    zone.oceanDistance = -1;
                }
            }
        }

        // Set zone height based on distance from closest ocean tile.
        for (int x = 0; x < width; x++)
        {
            for (int z = 0; z < depth; z++)
            {
                if (zones[x, z].ocean) continue;

                // For every ocean region, calculate distance to the center of the tile.
                double closestDistance = double.MaxValue;
                for (int j = 0; j < width; j++)
                {
                    for (int i = 0; i < depth; i++)
                    {
                        if (!zones[j, i].ocean) continue;
                        double distance = zones[x, z].localZoneCenterPosition.DistanceTo(zones[j, i].localZoneCenterPosition);
                        if (distance < closestDistance) closestDistance = distance;
                    }
                }

                zones[x, z].oceanDistance = closestDistance; // / (plateSize / 2);
            }
        }

        // Generate rivers.
        for (int x = 0; x < width; x++)
        {
            for (int z = 0; z < depth; z++)
            {
                TectonicZone zone = zones[x, z];

                // Only generate from oceans.
                if (!zone.ocean) continue;

                // Check if the ocean borders a coastal zone.
                bool nearLand = false;
                for (int xz = -1; xz < 2; xz++)
                {
                    for (int zz = -1; zz < 2; zz++)
                    {
                        if (zones[Math.Clamp(x + xz, 0, config.zonesInPlate - 1), Math.Clamp(z + zz, 0, config.zonesInPlate - 1)].ocean == false)
                        {
                            zone.coastal = true;
                            nearLand = true;
                            break;
                        }
                    }

                    if (nearLand) break;
                }

                // Chance to seed a river at each coastal tile.
                if (nearLand && rand.NextInt(100) < config.riverSpawnChance)
                {
                    River river = new();

                    // Attempt to go uphill 8 zones.
                    TectonicZone target = FindHighestZone(zone, 8);

                    // Angle river will point towards initially.
                    Vec2d startVector = target.localZoneCenterPosition - zone.localZoneCenterPosition;
                    double startAngle = RiverMath.NormalToDegrees(startVector.Normalize());

                    if (GenerateRiver(startAngle, zone.localZoneCenterPosition, 0, null, river.nodes, config.error))
                    {
                        rivers.Add(river);
                        river.startPos = zone.localZoneCenterPosition;
                    }
                }
            }
        }

        while (generationQueue.Count > 0)
        {
            GenerationRequest request = generationQueue.Dequeue();
            GenerateRiver(request.angle, request.startPos, request.stage, request.parentRiver, request.nodeList, request.errorLevel);
        }

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

            AssignRiverSizes(river.nodes);
            BuildRiverSegments(river.nodes);
            ConnectSegments(river.nodes);
            ValidateSegments(river.nodes);

            int radius = 0;

            foreach (RiverNode node in river.nodes)
            {
                foreach (RiverSegment segment in node.segments)
                {
                    int distFromStart = (int)river.startPos.DistanceTo(segment.endPos);
                    if (distFromStart > radius) radius = distFromStart;
                }

                // Add a lake.
                if (node.end)
                {
                    endNodes.Add(node);
                }
            }

            // Up to 512 away from the endpoint of the farthest segment. (Valley width + distortion + 32 less than 512).
            river.radius = radius + 512;

            foreach (RiverNode node in endNodes)
            {
                if (rand.NextInt(100) < config.lakeChance)
                {
                    AddLake(50, 75, river.nodes, node, RiverMath.NormalToDegrees((node.endPos - node.startPos).Normalize()));
                }
            }
        }

        foreach (River river in smallRivers) rivers.Remove(river);

        riverNodes.Clear();
    }

    public struct GenerationRequest
    {
        public double angle;
        public Vec2d startPos;
        public int stage;
        public RiverNode parentRiver;
        public List<RiverNode> nodeList;
        public int errorLevel;

        public GenerationRequest(double angle, Vec2d startPos, int stage, RiverNode parentRiver, List<RiverNode> nodeList, int errorLevel)
        {
            this.angle = angle;
            this.startPos = startPos;
            this.stage = stage;
            this.parentRiver = parentRiver;
            this.nodeList = nodeList;
            this.errorLevel = errorLevel;
        }
    }

    public bool GenerateRiver(double angle, Vec2d startPos, int stage, RiverNode parentRiver, List<RiverNode> nodeList, int errorLevel)
    {
        // If this branch exceeds max nodes, return.
        if (stage > config.maxNodes) return false;

        Vec2d normal = RiverMath.DegreesToNormal(angle);
        Vec2d endPos = startPos + (normal * (config.minLength + rand.NextInt(config.lengthVariation)));

        // Invalid if this intersects any existing pieces.
        bool intersecting = false;

        // For intersection calculation make it slightly longer.
        Vec2d delta = endPos - startPos;
        delta *= 0.5;
        delta += endPos;

        foreach (RiverNode node in riverNodes)
        {
            if (startPos == node.endPos || startPos == node.startPos) continue; // Don't intersect with self or siblings.

            if (RiverMath.LineIntersects(startPos, delta, node.startPos, node.endPos))
            {
                intersecting = true;
                break;
            }
        }

        // Don't go out of bounds.
        if (endPos.X < 0 || endPos.X > plateSize || endPos.Y < 0 || endPos.Y > plateSize)
        {
            intersecting = true;
        }

        // Don't go downhill.
        double startDist = GetZoneAt(startPos.X, startPos.Y).oceanDistance;
        double endDist = GetZoneAt(endPos.X, endPos.Y).oceanDistance;
        if (startDist > endDist)
        {
            if (errorLevel == 0) intersecting = true;
            errorLevel--;
        }

        // Don't go into oceans after a couple tries.
        if (GetZoneAt(endPos.X, endPos.Y).ocean && stage > 2) intersecting = true;

        // Going into ocean, bending over 90 degrees from original, going farther than target: return.
        //|| Math.Abs(initialAngle - angle) > 90
        if (intersecting)
        {
            return false;
        }

        RiverNode riverNode = new(startPos, endPos, parentRiver);
        nodeList.Add(riverNode);
        riverNodes.Add(riverNode);

        // A node has come from this river so it's no longer an end.
        if (parentRiver != null) parentRiver.end = false;

        // Chance for a river to split into 2 rivers
        if (rand.NextInt(100) < config.riverSplitChance && parentRiver != null)
        {
            double angle1 = angle + (config.minForkAngle + rand.NextInt(config.forkVaration));
            double angle2 = angle - (config.minForkAngle + rand.NextInt(config.forkVaration));

            generationQueue.Enqueue(new GenerationRequest(angle1, endPos, stage + 1, riverNode, nodeList, errorLevel));
            generationQueue.Enqueue(new GenerationRequest(angle2, endPos, stage + 1, riverNode, nodeList, errorLevel));
            return true;
        }
        else
        {
            int sign = 0;
            while (sign == 0) sign = -1 + rand.NextInt(3);

            double angle1 = angle - (rand.NextInt(config.normalAngle) * sign);

            generationQueue.Enqueue(new GenerationRequest(angle1, endPos, stage + 1, riverNode, nodeList, errorLevel));
            return true;
        }
    }

    public void AssignRiverSizes(List<RiverNode> riverList)
    {
        List<RiverNode> riverEndList = riverList.Where(river => river.end == true).ToList();

        foreach (RiverNode river in riverEndList)
        {
            AssignRiverSize(river, 1);
        }
    }

    public void AssignRiverSize(RiverNode river, float endSize)
    {
        if (river.startSize <= endSize) // If the endSize is less than the startSize this river hasn't been generated yet or a bigger river is ready to generate.
        {
            river.endSize = endSize;
            river.startSize = endSize + config.riverGrowth;

            // River must end at atleast the min size.
            river.startSize = Math.Max(river.startSize, config.minSize);

            // River can't be larger than max size.
            river.startSize = Math.Min(river.startSize, config.maxSize);

            if (river.parentNode != null)
            {
                AssignRiverSize(river.parentNode, river.startSize);
            }
        }
    }

    public void BuildRiverSegments(List<RiverNode> riverList)
    {
        // Assign segments to each river.
        foreach (RiverNode river in riverList)
        {
            river.segments = new RiverSegment[config.segmentsInRiver];

            Vec2d offsetVector = new Vec2d(river.endPos.X - river.startPos.X, river.endPos.Y - river.startPos.Y).Normalize();
            offsetVector = new Vec2d(-offsetVector.Y, offsetVector.X);

            for (int i = 0; i < river.segments.Length; i++) // For each segment.
            {
                double offset = -config.segmentOffset + (rand.NextDouble() * config.segmentOffset * 2); // Offset segment.

                river.segments[i] = new RiverSegment();

                if (i == 0)
                {
                    river.segments[i].startPos = river.startPos;
                }
                else
                {
                    river.segments[i].startPos = river.segments[i - 1].endPos;
                }

                if (i == config.segmentsInRiver - 1)
                {
                    river.segments[i].endPos = river.endPos;
                }
                else
                {
                    river.segments[i].endPos = new Vec2d(
                        GameMath.Lerp(river.startPos.X, river.endPos.X, (double)(i + 1) / config.segmentsInRiver),
                        GameMath.Lerp(river.startPos.Y, river.endPos.Y, (double)(i + 1) / config.segmentsInRiver)
                        );

                    river.segments[i].endPos.X += offset * offsetVector.X;
                    river.segments[i].endPos.Y += offset * offsetVector.Y;
                }

                river.segments[i].riverNode = river;

                river.segments[i].midPoint = river.segments[i].startPos + ((river.segments[i].endPos - river.segments[i].startPos) * 0.5);
            }
        }
    }

    public void ConnectSegments(List<RiverNode> riverNodeList)
    {
        foreach (RiverNode node in riverNodeList)
        {
            // Make sure all rivers flow into each other smoothly.
            /*
            if (node.parentNode?.endSize > node.startSize)
            {
                node.startSize = node.parentNode.endSize;
            }
            */

            for (int i = 0; i < node.segments.Length; i++)
            {
                if (i == 0)
                {
                    if (node.parentNode != null)
                    {
                        node.segments[i].parent = node.parentNode.segments[^1]; // Make the last segment in the parent river the parent of this.
                        node.parentNode.segments[config.segmentsInRiver - 1].children.Add(node.segments[i]); // Add this to the children of that river.
                    }
                    else
                    {
                        // This river is the mouth at the ocean.
                        riverStarts.Add(node.segments[i]);
                        node.segments[i].parent = node.segments[i];
                    }

                    continue;
                }

                node.segments[i].parent = node.segments[i - 1]; // Make the parent the last segment.
                node.segments[i - 1].children.Add(node.segments[i]); // Add this to its children.
            }
        }

        // If the river segment has no children, make it its own child.
        foreach (RiverNode node in riverNodeList)
        {
            for (int i = 0; i < node.segments.Length; i++)
            {
                if (node.segments[i].children.Count == 0)
                {
                    node.segments[i].children.Add(node.segments[i]);
                }
            }
        }
    }

    // Checks if a curve is too steep to interpolate.
    public void ValidateSegments(List<RiverNode> riverNodeList)
    {
        foreach (RiverNode node in riverNodeList)
        {
            foreach (RiverSegment segment in node.segments)
            {
                // If it's the last segment invalidate it.
                if (segment.children[0] == segment)
                {
                    segment.parentInvalid = true;
                    continue;
                }

                if (segment.parent == segment)
                {
                    segment.parentInvalid = true;
                    continue;
                }

                int index = node.segments.IndexOf(segment);

                if (index == 0)
                {
                    float projection1 = RiverMath.GetProjection(segment.startPos, segment.midPoint, node.parentNode.segments[config.segmentsInRiver - 1].midPoint);

                    if (projection1 < 0.2 || projection1 > 0.8)
                    {
                        segment.parentInvalid = true;
                    }

                    continue;
                }

                float projection2 = RiverMath.GetProjection(segment.startPos, segment.midPoint, node.segments[index - 1].midPoint);

                if (projection2 < 0.2 || projection2 > 0.8)
                {
                    segment.parentInvalid = true;
                }
            }

            foreach (RiverSegment segment in node.segments)
            {
                segment.childrenArray = segment.children.ToArray();
                segment.children = null;
            }
        }
    }
    public void AddLake(int minSize, int maxSize, List<RiverNode> nodeList, RiverNode parent, double angle)
    {
        // Set start of the parent to the min size.
        parent.endSize = config.minSize / 2;

        int lakeSize = rand.NextInt(maxSize - minSize) + minSize;

        Vec2d delta = parent.endPos + (RiverMath.DegreesToNormal(angle) * 100);

        // Make lakes 2 nodes?

        RiverNode lakeNode = new(parent.endPos, delta, null)
        {
            startSize = parent.endSize,
            endSize = lakeSize,
            segments = new RiverSegment[1],
            lake = true
        };

        // Lake only has 1 segment.

        lakeNode.segments[0] = new(lakeNode.startPos, lakeNode.endPos, lakeNode.startPos + ((lakeNode.endPos - lakeNode.startPos) / 2))
        {
            parent = parent.segments[config.segmentsInRiver - 1]
        };

        // Child of self.
        lakeNode.segments[0].children.Add(lakeNode.segments[0]);
        lakeNode.segments[0].childrenArray = lakeNode.segments[0].children.ToArray();
        lakeNode.segments[0].children = null;
        lakeNode.segments[0].parentInvalid = true;

        // Doesn't move.
        lakeNode.speed = 0;

        lakeNode.segments[0].riverNode = lakeNode;

        parent.segments[config.segmentsInRiver - 1].childrenArray = new RiverSegment[] { lakeNode.segments[0] };
        parent.segments[config.segmentsInRiver - 1].parentInvalid = false;

        nodeList.Add(lakeNode);
    }

    public List<TectonicZone> GetZonesAround(int localZoneX, int localZoneZ, int radius = 1)
    {
        List<TectonicZone> zonesListerino = new();

        for (int x = -radius; x <= radius; x++)
        {
            for (int z = -radius; z <= radius; z++)
            {
                if (localZoneX + x < 0 || localZoneX + x > config.zonesInPlate - 1 || localZoneZ + z < 0 || localZoneZ + z > config.zonesInPlate - 1) continue;

                zonesListerino.Add(zones[localZoneX + x, localZoneZ + z]);
            }
        }

        return zonesListerino;
    }

    public TectonicZone GetZoneAt(double localX, double localZ)
    {
        int zx = (int)Math.Clamp(localX / config.zoneSize, 0, config.zonesInPlate - 1);
        int zz = (int)Math.Clamp(localZ / config.zoneSize, 0, config.zonesInPlate - 1);

        return zones[zx, zz];
    }

    public TectonicZone FindHighestZone(TectonicZone zone, int hops)
    {
        TectonicZone[] array = GetZonesAround(zone.xIndex, zone.zIndex, 1).OrderByDescending(x => x.oceanDistance).ToArray();

        if (array[0] == zone || hops == 0)
        {
            return zone;
        }
        else
        {
            return FindHighestZone(array[0], hops - 1);
        }
    }
}