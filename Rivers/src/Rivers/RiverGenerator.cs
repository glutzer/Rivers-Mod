using System;
using System.Collections.Generic;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace Rivers;

public class RiverGenerator
{
    public double riverDepth;
    public double baseDepth;

    public Noise riverDistortionX;
    public Noise riverDistortionZ;

    public int strength;

    public RiverGenerator(ICoreServerAPI sapi)
    {
        double multi = 256f / sapi.WorldManager.MapSizeY;

        riverDepth = RiverConfig.Loaded.riverDepth * multi;
        baseDepth = RiverConfig.Loaded.baseDepth * multi;

        riverDistortionX = new Noise(0, RiverConfig.Loaded.riverFrequency, RiverConfig.Loaded.riverOctaves, RiverConfig.Loaded.riverGain, RiverConfig.Loaded.riverLacunarity);
        riverDistortionZ = new Noise(2, RiverConfig.Loaded.riverFrequency, RiverConfig.Loaded.riverOctaves, RiverConfig.Loaded.riverGain, RiverConfig.Loaded.riverLacunarity);
        strength = RiverConfig.Loaded.riverStrength;
    }

    // Curbs segments to test.
    public void ValidateSegments(RiverSegment[] segments, double maxValleyWidth, double x, double z, List<RiverSegment> valid)
    {
        double distX = riverDistortionX.GetNoise(x, z);
        double distZ = riverDistortionZ.GetNoise(x, z);
        Vec2d point = new(x + (distX * strength), z + (distZ * strength));

        for (int b = 0; b < segments.Length; b++)
        {
            RiverSegment segment = segments[b];

            float riverProjection = RiverMath.GetProjection(point, segment.riverNode.startPos, segment.riverNode.endPos); // How far along the main river for size calculation.

            float riverSize = GameMath.Lerp(segment.riverNode.startSize, segment.riverNode.endSize, riverProjection); // Size of river.

            float segmentProjection = RiverMath.GetProjection(point, segment.startPos, segment.endPos);

            RiverSegment[] connectors;
            if (segmentProjection > 0.5)
            {
                connectors = segment.childrenArray;
            }
            else
            {
                connectors = new RiverSegment[] { segment.parent };
            }

            Vec2d lerpedStart = new();
            Vec2d lerpedEnd = new();

            double minDistance = 5000;

            for (int i = 0; i < connectors.Length; i++)
            {
                RiverSegment connector = connectors[i];

                float midPointProjection;
                if (segment.midPoint != connector.midPoint)
                {
                    midPointProjection = RiverMath.GetProjection(point, segment.midPoint, connector.midPoint);
                }
                else
                {
                    midPointProjection = 1;
                }

                // Projections onto lines with equal length are NaN.
                if (segmentProjection > 0.5)
                {
                    if (connector.parentInvalid) midPointProjection = 0;

                    // Lerp to the halfway point if the river fork doesn't match up.
                    if (connector.riverNode.startSize < segment.riverNode.endSize)
                    {
                        riverSize = GameMath.Lerp(connector.riverNode.startSize, connector.riverNode.endSize, 0.5f);
                    }

                    lerpedStart.X = GameMath.Lerp(segment.midPoint.X, connector.startPos.X, midPointProjection);
                    lerpedStart.Y = GameMath.Lerp(segment.midPoint.Y, connector.startPos.Y, midPointProjection);
                    lerpedEnd.X = GameMath.Lerp(segment.endPos.X, connector.midPoint.X, midPointProjection);
                    lerpedEnd.Y = GameMath.Lerp(segment.endPos.Y, connector.midPoint.Y, midPointProjection);
                }
                else
                {
                    if (segment.parentInvalid) midPointProjection = 0;

                    lerpedStart.X = GameMath.Lerp(segment.startPos.X, connector.midPoint.X, midPointProjection);
                    lerpedStart.Y = GameMath.Lerp(segment.startPos.Y, connector.midPoint.Y, midPointProjection);
                    lerpedEnd.X = GameMath.Lerp(segment.midPoint.X, connector.endPos.X, midPointProjection);
                    lerpedEnd.Y = GameMath.Lerp(segment.midPoint.Y, connector.endPos.Y, midPointProjection);
                }

                double distance = RiverMath.DistanceToLine(point, lerpedStart, lerpedEnd);
                distance -= riverSize;

                minDistance = Math.Min(minDistance, distance);
            }

            if (minDistance <= maxValleyWidth + (strength * 2) + 32 && !valid.Contains(segment))
            {
                valid.Add(segment);
            }
        }
    }

    public RiverSample SampleRiver(RiverSegment[] segments, double x, double z)
    {
        RiverSample riverSample = new();

        if (segments.Length == 0) return riverSample;

        double closestLine = double.PositiveInfinity;

        double distX = riverDistortionX.GetNoise(x, z);
        double distZ = riverDistortionZ.GetNoise(x, z);

        Vec2d point = new(x + (distX * strength), z + (distZ * strength));

        for (int s = 0; s < segments.Length; s++)
        {
            RiverSegment segment = segments[s];

            float riverProjection = RiverMath.GetProjection(point, segment.riverNode.startPos, segment.riverNode.endPos); // How far along the main river for size calculation.

            float riverSize = GameMath.Lerp(segment.riverNode.startSize, segment.riverNode.endSize, riverProjection); // Size of river.

            float segmentProjection = RiverMath.GetProjection(point, segment.startPos, segment.endPos);

            RiverSegment[] connectors;
            if (segmentProjection > 0.5)
            {
                connectors = segment.childrenArray;
            }
            else
            {
                connectors = new RiverSegment[] { segment.parent };
            }

            Vec2d lerpedStart = new();
            Vec2d lerpedEnd = new();

            for (int i = 0; i < connectors.Length; i++)
            {
                RiverSegment connector = connectors[i];

                float midPointProjection;
                if (segment.midPoint != connector.midPoint)
                {
                    midPointProjection = RiverMath.GetProjection(point, segment.midPoint, connector.midPoint);
                }
                else
                {
                    midPointProjection = 1;
                }

                // Projections onto lines with equal length are NaN.
                if (segmentProjection > 0.5)
                {
                    if (connector.parentInvalid) midPointProjection = 0;

                    // Lerp to the halfway point if the river fork doesn't match up.
                    if (connector.riverNode.startSize < segment.riverNode.endSize)
                    {
                        riverSize = GameMath.Lerp(connector.riverNode.startSize, connector.riverNode.endSize, 0.5f);
                    }

                    lerpedStart.X = GameMath.Lerp(segment.midPoint.X, connector.startPos.X, midPointProjection);
                    lerpedStart.Y = GameMath.Lerp(segment.midPoint.Y, connector.startPos.Y, midPointProjection);
                    lerpedEnd.X = GameMath.Lerp(segment.endPos.X, connector.midPoint.X, midPointProjection);
                    lerpedEnd.Y = GameMath.Lerp(segment.endPos.Y, connector.midPoint.Y, midPointProjection);
                }
                else
                {
                    if (segment.parentInvalid) midPointProjection = 0;

                    lerpedStart.X = GameMath.Lerp(segment.startPos.X, connector.midPoint.X, midPointProjection);
                    lerpedStart.Y = GameMath.Lerp(segment.startPos.Y, connector.midPoint.Y, midPointProjection);
                    lerpedEnd.X = GameMath.Lerp(segment.midPoint.X, connector.endPos.X, midPointProjection);
                    lerpedEnd.Y = GameMath.Lerp(segment.midPoint.Y, connector.endPos.Y, midPointProjection);
                }

                double distance = RiverMath.DistanceToLine(point, lerpedStart, lerpedEnd);

                // Calculate bank factor.
                if (distance <= riverSize) // If within bank distance.
                {
                    if (segment.riverNode.lake)
                    {
                        closestLine = -100;
                        riverSample.flowVectorX = 0;
                        riverSample.flowVectorZ = 0;
                    }

                    if (distance < closestLine)
                    {
                        // If a bank exists, the flow vector is the same as it.
                        Vec2d segmentFlowVector = lerpedStart - lerpedEnd;
                        segmentFlowVector.Normalize();

                        // Round the flow to group together sets of water.
                        segmentFlowVector.X = Math.Round(segmentFlowVector.X, 1);
                        segmentFlowVector.Y = Math.Round(segmentFlowVector.Y, 1);

                        segmentFlowVector.Normalize();

                        riverSample.flowVectorX = (float)segmentFlowVector.X * segment.riverNode.speed;
                        riverSample.flowVectorZ = (float)segmentFlowVector.Y * segment.riverNode.speed;

                        closestLine = distance;
                    }

                    riverSample.riverDistance = 0;

                    double lerp = RiverMath.InverseLerp(distance, riverSize, 0);
                    lerp = Math.Sqrt(1 - Math.Pow(1 - lerp, 2));

                    riverSample.bankFactor = Math.Max(Math.Max(Math.Sqrt(riverSize) * riverDepth, baseDepth) * lerp, riverSample.bankFactor); // Deepest bank.

                    continue;
                }

                distance -= riverSize;

                if (distance < 0) distance = 0;

                riverSample.riverDistance = Math.Min(distance, riverSample.riverDistance); // Lowest distance to the edge of a river.
            }
        }

        return riverSample;
    }
}

public struct RiverSample
{
    public double riverDistance;
    public double bankFactor;

    public float flowVectorX;
    public float flowVectorZ;

    public RiverSample()
    {
        riverDistance = 5000;
        bankFactor = 0;
        flowVectorX = -100; // Initialized at -100 for checks. Nothing will move this fast.
    }
}