using System;
using Vintagestory.API.MathTools;

namespace Rivers;

/// <summary>
/// Represents a start and end point and a size of a river.
/// </summary>
public class RiverNode : IEquatable<RiverNode>
{
    // World coordinates.
    public Vec2d startPos;
    public Vec2d endPos;
    public RiverNode parentNode;

    public float endSize = 0;
    public float startSize = 1;

    // If this has no children.
    public bool end = true;

    // Segments this is composed of internally.
    public RiverSegment[] segments;

    // Speed of which water moves through this river.
    public float speed = 1;

    public bool lake = false;

    public RiverNode(Vec2d startPos, Vec2d endPos, RiverNode parentNode)
    {
        this.startPos = startPos;
        this.endPos = endPos;
        this.parentNode = parentNode;
    }

    public bool Equals(RiverNode other)
    {
        if (other == null) return false;
        return startPos.X == other.startPos.X && startPos.Y == other.startPos.Y && endPos.X == other.endPos.X && endPos.Y == other.endPos.Y;
    }

    public override int GetHashCode()
    {
        return startPos.GetHashCode() + endPos.GetHashCode();
    }

    public override bool Equals(object obj)
    {
        return Equals(obj as RiverNode);
    }
}