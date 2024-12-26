using System.Runtime.CompilerServices;

namespace Rivers;

public class ChunkMath
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int ChunkIndex3d(int x, int y, int z)
    {
        return (((y * 32) + z) * 32) + x;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int ChunkIndex2d(int x, int z)
    {
        return (z * 32) + x;
    }
}