using System;
using System.Runtime.CompilerServices;
using Vintagestory.API.MathTools;

namespace Rivers;

public class RiverMath
{
    // Projection from start to end point.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float GetProjection(Vec2d point, Vec2d start, Vec2d end)
    {
        double dx = end.X - start.X;
        double dy = end.Y - start.Y;
        double v = ((point.X - start.X) * dx) + ((point.Y - start.Y) * dy);
        v /= (dx * dx) + (dy * dy);
        return (float)(v < 0 ? 0 : v > 1 ? 1 : v);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double DistanceToLine(Vec2d point, Vec2d start, Vec2d end)
    {
        if (((start.X - end.X) * (point.X - end.X)) + ((start.Y - end.Y) * (point.Y - end.Y)) <= 0)
        {
            return Math.Sqrt(((point.X - end.X) * (point.X - end.X)) + ((point.Y - end.Y) * (point.Y - end.Y)));
        }

        if (((end.X - start.X) * (point.X - start.X)) + ((end.Y - start.Y) * (point.Y - start.Y)) <= 0)
        {
            return Math.Sqrt(((point.X - start.X) * (point.X - start.X)) + ((point.Y - start.Y) * (point.Y - start.Y)));
        }

        return Math.Abs(((end.Y - start.Y) * point.X) - ((end.X - start.X) * point.Y) + (end.X * start.Y) - (end.Y * start.X)) / Math.Sqrt(((start.Y - end.Y) * (start.Y - end.Y)) + ((start.X - end.X) * (start.X - end.X)));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double InverseLerp(double value, double min, double max)
    {
        if (Math.Abs(max - min) < double.Epsilon)
        {
            return 0f;
        }
        else
        {
            return (value - min) / (max - min);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool LineIntersects(Vec2d startA, Vec2d endA, Vec2d startB, Vec2d endB)
    {
        return ((endB.Y - startA.Y) * (startB.X - startA.X) > (startB.Y - startA.Y) * (endB.X - startA.X)) != ((endB.Y - endA.Y) * (startB.X - endA.X) > (startB.Y - endA.Y) * (endB.X - endA.X)) && ((startB.Y - startA.Y) * (endA.X - startA.X) > (endA.Y - startA.Y) * (startB.X - startA.X)) != ((endB.Y - startA.Y) * (endA.X - startA.X) > (endA.Y - startA.Y) * (endB.X - startA.X));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double NormalToDegrees(Vec2d normal)
    {
        return Math.Atan2(normal.Y, normal.X) * (180 / Math.PI);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec2d DegreesToNormal(double degrees)
    {
        double radians = degrees * (Math.PI / 180);
        return new Vec2d(Math.Cos(radians), Math.Sin(radians));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float DistanceTo(Vec3d posA, Vec3d posB, double xSize, double zSize)
    {
        double x = posA.X - posB.X;
        x /= xSize;

        double y = posA.Y - posB.Y;

        double z = posA.Z - posB.Z;
        z /= zSize;

        return (float)Math.Sqrt((x * x) + (y * y) + (z * z));
    }

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