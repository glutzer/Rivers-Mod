using System.Collections.Generic;
using Vintagestory.API.MathTools;

namespace Rivers;

public class RiverSegment
{
    public Vec2d startPos;
    public Vec2d endPos;

    public Vec2d midPoint; // For bezier.

    public RiverNode riverNode;

    public RiverSegment parent;
    public List<RiverSegment> children = new();
    public RiverSegment[] childrenArray;

    public bool parentInvalid = false;

    public RiverSegment(Vec2d startPos, Vec2d endPos, Vec2d midPoint)
    {
        this.startPos = startPos;
        this.endPos = endPos;
        this.midPoint = midPoint;
    }

    public RiverSegment()
    {
    }
}