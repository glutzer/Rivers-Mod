using Vintagestory.API.MathTools;

namespace Rivers;

public class TectonicZone
{
    // Center of region.
    public Vec2d localZoneCenterPosition = new();

    // Distance from closest ocean tile.
    public double oceanDistance;
    public bool coastal;

    // River generation info.
    public int xIndex = 0;
    public int zIndex = 0;

    public bool ocean = false;

    public TectonicZone(int centerPositionX, int centerPositionZ)
    {
        localZoneCenterPosition = new Vec2d(centerPositionX, centerPositionZ);
    }
}