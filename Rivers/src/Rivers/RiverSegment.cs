using OpenTK.Mathematics;
using System;
using System.Collections.Generic;

namespace Rivers;

public class RiverSegment : ISpatialData
{
    public Vector2d startPos;
    public Vector2d endPos;

    public Vector2d midPoint; // For bezier.

    public RiverNode riverNode;

    // May be orphaned.
    public RiverSegment? parent;

    public List<RiverSegment> children = new();

    // Curve differently when parent is at an invalid angle.
    public bool parentInvalid = false;

    public RiverSegment(Vector2d startPos, Vector2d endPos, RiverNode riverNode)
    {
        this.startPos = startPos;
        this.endPos = endPos;

        midPoint = startPos + ((endPos - startPos) / 2);

        this.riverNode = riverNode;
    }

    public Envelope Envelope { get; set; } = null!;

    /// <summary>
    /// Called after parent and children have been finalized.
    /// River size should be assigned.
    /// </summary>
    public void InitializeBounds()
    {
        Vector2d minPos = Vector2d.ComponentMin(startPos, endPos);
        Vector2d maxPos = Vector2d.ComponentMax(startPos, endPos);

        if (parent != null)
        {
            minPos = Vector2d.ComponentMin(minPos, parent.midPoint);
            maxPos = Vector2d.ComponentMax(maxPos, parent.midPoint);
        }

        foreach (RiverSegment child in children)
        {
            minPos = Vector2d.ComponentMin(minPos, child.midPoint);
            maxPos = Vector2d.ComponentMax(maxPos, child.midPoint);
        }

        // Add valley width + max size of the river at this node.

        float nodeMaxSize = Math.Max(riverNode.startSize, riverNode.endSize);

        minPos -= new Vector2d(RiverConfig.Loaded.maxValleyWidth + nodeMaxSize);
        maxPos += new Vector2d(RiverConfig.Loaded.maxValleyWidth + nodeMaxSize);

        Envelope = new Envelope(minPos.X, minPos.Y, maxPos.X, maxPos.Y);
    }
}