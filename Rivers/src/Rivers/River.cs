using System.Collections.Generic;
using Vintagestory.API.MathTools;

namespace Rivers;

public class River
{
    public int radius = 0;

    public Vec2d startPos;

    public List<RiverNode> nodes = new();
}