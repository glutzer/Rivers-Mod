using OpenTK.Mathematics;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Rivers;

public class River
{
    // Radius set after for optimizing searches, might be able to be replaced.
    public int Radius { get; set; }

    public Vector2d StartPos { get; private set; }

    public List<RiverNode> nodes = new();

    public River(Vector2d startPos)
    {
        StartPos = startPos;
    }

    /// <summary>
    /// After all nodes have been added, assign sizes.
    /// </summary>
    public void AssignRiverSizes()
    {
        List<RiverNode> riverEndList = nodes.Where(river => river.end == true).ToList();

        foreach (RiverNode river in riverEndList)
        {
            AssignRiverSize(river, 1);
        }
    }

    private static void AssignRiverSize(RiverNode river, float endSize)
    {
        RiverConfig config = RiverConfig.Loaded;

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
}