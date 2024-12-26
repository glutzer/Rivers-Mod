using OpenTK.Mathematics;
using System;
using System.Runtime.CompilerServices;

namespace Rivers;

internal static class VectorExtensions
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double DistanceTo(this Vector2d vector, Vector2d other)
    {
        return Math.Sqrt(Math.Pow(vector.X - other.X, 2) + Math.Pow(vector.Y - other.Y, 2));
    }
}