using OpenTK.Mathematics;
using ProtoBuf;
using ProtoBuf.Meta;
using System.Runtime.CompilerServices;

namespace Rivers;

/// <summary>
/// One zone in a region of rivers, like a civ tile.
/// </summary>
[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public class RiverZone
{
    [ModuleInitializer]
    internal static void Init()
    {
        RuntimeTypeModel.Default.Add(typeof(Vector2d), false)
            .Add("X")
            .Add("Y");
    }

    // Center of region.
    public Vector2d localZoneCenterPosition;

    // Distance from closest ocean tile.
    public double oceanDistance;

    // River generation info.
    public int xIndex;
    public int zIndex;

    public bool coastalZone;
    public bool oceanZone;

    public RiverZone(int centerPositionX, int centerPositionZ, int xIndex, int zIndex)
    {
        localZoneCenterPosition = new Vector2d(centerPositionX, centerPositionZ);
        this.xIndex = xIndex;
        this.zIndex = zIndex;
    }

    public RiverZone()
    {

    }
}