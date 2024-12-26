using OpenTK.Mathematics;

namespace Rivers;

/// <summary>
/// One zone in a region of rivers, like a civ tile.
/// </summary>
public class RiverZone
{
    // Center of region.
    public Vector2d localZoneCenterPosition;

    // Distance from closest ocean tile.
    public double oceanDistance;

    // River generation info.
    public readonly int xIndex;
    public readonly int zIndex;

    public bool coastalZone;
    public bool oceanZone;

    public RiverZone(int centerPositionX, int centerPositionZ, int xIndex, int zIndex)
    {
        localZoneCenterPosition = new Vector2d(centerPositionX, centerPositionZ);
        this.xIndex = xIndex;
        this.zIndex = zIndex;
    }
}