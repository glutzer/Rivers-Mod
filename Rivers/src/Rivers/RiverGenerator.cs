using OpenTK.Mathematics;
using System;
using System.Runtime.CompilerServices;
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

        RiverConfig config = RiverConfig.Loaded;

        riverDepth = config.riverDepth * multi;
        baseDepth = config.baseDepth * multi;

        riverDistortionX = new Noise(0, config.riverFrequency, config.riverOctaves, config.riverGain, config.riverLacunarity);
        riverDistortionZ = new Noise(2, config.riverFrequency, config.riverOctaves, config.riverGain, config.riverLacunarity);
        strength = config.riverDistortionStrength;
    }

    public RiverSample SampleRiver(RiverSegment[] segments, double x, double z)
    {
        RiverSample riverSample = new();

        double closestLine = double.PositiveInfinity;

        double distX = riverDistortionX.GetNoise(x, z);
        double distZ = riverDistortionZ.GetNoise(x, z);

        Vector2d point = new(x + (distX * strength), z + (distZ * strength));

        for (int s = 0; s < segments.Length; s++)
        {
            RiverSegment segment = segments[s];

            float riverProjection = RiverMath.GetProjection(point, segment.riverNode.startPos, segment.riverNode.endPos); // How far along the main river for size calculation.
            float riverSize = GameMath.Lerp(segment.riverNode.startSize, segment.riverNode.endSize, riverProjection); // Size of river.
            float segmentProjection = RiverMath.GetProjection(point, segment.startPos, segment.endPos);

            // If halfway into the segment, it's sampling every child.
            // Otherwise, it samples the segment's parent.

            if (segmentProjection > 0.5)
            {
                if (segment.children.Count == 0)
                {
                    riverSample = SampleSelf(segment, riverSample, point, riverSize, ref closestLine);
                }
                else
                {
                    foreach (RiverSegment childSegment in segment.children)
                    {
                        riverSample = SampleConnector(segment, childSegment, segmentProjection, riverSample, point, riverSize, ref closestLine);
                    }
                }
            }
            else
            {
                riverSample = segment.parent == null
                    ? SampleSelf(segment, riverSample, point, riverSize, ref closestLine)
                    : SampleConnector(segment, segment.parent, segmentProjection, riverSample, point, riverSize, ref closestLine);
            }
        }

        return riverSample;
    }

    /// <summary>
    /// Sample self on a segment with no children, or if the parent is null.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public RiverSample SampleSelf(RiverSegment segment, RiverSample riverSample, Vector2d point, float riverSize, ref double closestLine)
    {
        double distance = RiverMath.DistanceToLine(point, segment.startPos, segment.endPos);

        // Calculate bank factor.
        if (distance <= riverSize) // If within bank distance.
        {
            if (segment.riverNode.isLake)
            {
                closestLine = -100;
                riverSample.flowVectorX = 0;
                riverSample.flowVectorZ = 0;
            }

            if (distance < closestLine)
            {
                // If a bank exists, the flow vector is the same as it.
                Vector2d segmentFlowVector = segment.startPos - segment.endPos;
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

            return riverSample;
        }

        distance -= riverSize;

        if (distance < 0) distance = 0;

        riverSample.riverDistance = Math.Min(distance, riverSample.riverDistance); // Lowest distance to the edge of a river.

        return riverSample;
    }

    /// <summary>
    /// Sample a connector. Connector may be null if the segment's parent is null.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public RiverSample SampleConnector(RiverSegment segment, RiverSegment connector, float projection, RiverSample riverSample, Vector2d point, float riverSize, ref double closestLine)
    {
        float midPointProjection;
        Vector2d lerpedStart = new();
        Vector2d lerpedEnd = new();

        midPointProjection = RiverMath.GetProjection(point, segment.midPoint, connector.midPoint);

        // Connector is not null when projected 0.5 into the line, since only the parent can be null.
        if (projection > 0.5)
        {
            // If the parent is invalid, don't curve at all.
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
            if (segment.riverNode.isLake)
            {
                closestLine = -100;
                riverSample.flowVectorX = 0;
                riverSample.flowVectorZ = 0;
            }

            if (distance < closestLine)
            {
                // If a bank exists, the flow vector is the same as it.
                Vector2d segmentFlowVector = lerpedStart - lerpedEnd;
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

            return riverSample;
        }

        distance -= riverSize;

        if (distance < 0) distance = 0;

        riverSample.riverDistance = Math.Min(distance, riverSample.riverDistance); // Lowest distance to the edge of a river.

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