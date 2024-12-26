using OpenTK.Mathematics;
using System;
using Vintagestory.API.MathTools;

namespace Rivers;

/// <summary>
/// Represents a start and end point and a size of a river.
/// </summary>
public class RiverNode : IEquatable<RiverNode>, ISpatialData
{
    // Local coordinates.
    public Vector2d startPos;
    public Vector2d endPos;

    // The beginning of the river may be orphaned.
    public RiverNode? parentNode;

    // The river this node belongs to.
    public River river;

    // Size of the start and end of the river line.
    public float endSize = 0;
    public float startSize = 1;

    // If this has no children.
    public bool end = true;

    // Segments this is composed of internally.
    public RiverSegment[] segments;

    // Speed of which water moves through this river.
    public float speed = 1;

    public bool isLake = false;

    public Envelope Envelope { get; set; }

    public static Envelope GetEnvelope(Vector2d startPos, Vector2d endPos)
    {
        Vector2d minPos = Vector2d.ComponentMin(startPos, endPos);
        Vector2d maxPos = Vector2d.ComponentMax(startPos, endPos);
        minPos -= new Vector2d(RiverConfig.Loaded.riverPaddingBlocks);
        maxPos += new Vector2d(RiverConfig.Loaded.riverPaddingBlocks);
        return new Envelope(minPos.X, minPos.Y, maxPos.X, maxPos.Y);
    }

    public RiverNode(Vector2d startPos, Vector2d endPos, River river, RiverNode? parentNode, LCGRandom rand, RiverSegment[]? customSegmentInit = null)
    {
        this.startPos = startPos;
        this.endPos = endPos;
        this.parentNode = parentNode;
        this.river = river;

        if (customSegmentInit == null)
        {
            segments = BuildRiverSegments(rand);
            ConnectSegments();
            ValidateSegments();
        }
        else
        {
            segments = customSegmentInit;
        }

        Envelope = GetEnvelope(startPos, endPos);
    }

    public bool Equals(RiverNode? other)
    {
        if (other == null) return false;
        return startPos.X == other.startPos.X && startPos.Y == other.startPos.Y && endPos.X == other.endPos.X && endPos.Y == other.endPos.Y;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(startPos, endPos);
    }

    public override bool Equals(object? obj)
    {
        return Equals(obj as RiverNode);
    }

    /// <summary>
    /// Build sub-segments.
    /// </summary>
    public RiverSegment[] BuildRiverSegments(LCGRandom rand)
    {
        RiverConfig config = RiverConfig.Loaded;
        RiverSegment[] newSegments = new RiverSegment[config.segmentsInRiver];

        Vector2d offsetVector = new Vector2d(endPos.X - startPos.X, endPos.Y - startPos.Y).Normalized();
        offsetVector = new Vector2d(-offsetVector.Y, offsetVector.X);

        for (int i = 0; i < newSegments.Length; i++) // For each segment.
        {
            double offset = -config.segmentOffset + (rand.NextDouble() * config.segmentOffset * 2); // Offset segment.

            Vector2d segmentStart;
            Vector2d segmentEnd;

            if (i == 0)
            {
                segmentStart = startPos;
            }
            else
            {
                segmentStart = newSegments[i - 1].endPos;
            }

            if (i == config.segmentsInRiver - 1)
            {
                segmentEnd = endPos;
            }
            else
            {
                segmentEnd = new Vector2d(
                    GameMath.Lerp(startPos.X, endPos.X, (double)(i + 1) / config.segmentsInRiver),
                    GameMath.Lerp(startPos.Y, endPos.Y, (double)(i + 1) / config.segmentsInRiver)
                    );

                segmentEnd.X += offset * offsetVector.X;
                segmentEnd.Y += offset * offsetVector.Y;
            }

            newSegments[i] = new RiverSegment(segmentStart, segmentEnd, this);
        }

        return newSegments;
    }

    /// <summary>
    /// Connect segments to each other and the last parent's segment.
    /// </summary>
    public void ConnectSegments()
    {
        for (int i = 0; i < segments.Length; i++)
        {
            if (i > 0)
            {
                segments[i].parent = segments[i - 1];
                segments[i].parent!.children.Add(segments[i]);
            }
            else
            {
                if (parentNode != null)
                {
                    segments[i].parent = parentNode.segments[^1];
                    segments[i].parent!.children.Add(segments[i]);
                }
            }
        }
    }

    /// <summary>
    /// If a parent is invalid, it will not curve to it.
    /// Do this if the parent doesn't exist or is too acute.
    /// </summary>
    public void ValidateSegments()
    {
        for (int i = 0; i < segments.Length; i++)
        {
            if (i == 0)
            {
                if (parentNode == null)
                {
                    segments[i].parentInvalid = true;
                    continue;
                }
                else
                {
                    float projection1 = RiverMath.GetProjection(segments[i].startPos, segments[i].midPoint, parentNode.segments[RiverConfig.Loaded.segmentsInRiver - 1].midPoint);

                    if (projection1 is < 0.2f or > 0.8f)
                    {
                        segments[i].parentInvalid = true;
                    }

                    continue;
                }
            }

            float projection2 = RiverMath.GetProjection(segments[i].startPos, segments[i].midPoint, segments[i - 1].midPoint);

            if (projection2 is < 0.2f or > 0.8f)
            {
                segments[i].parentInvalid = true;
            }
        }
    }
}