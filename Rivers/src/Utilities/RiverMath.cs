using OpenTK.Mathematics;
using System;
using System.Runtime.CompilerServices;

namespace Rivers;

public class RiverMath
{
    // Projection from start to end point.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float GetProjection(Vector2d point, Vector2d start, Vector2d end)
    {
        double dx = end.X - start.X;
        double dy = end.Y - start.Y;
        double v = ((point.X - start.X) * dx) + ((point.Y - start.Y) * dy);
        v /= (dx * dx) + (dy * dy);
        return (float)(v < 0 ? 0 : v > 1 ? 1 : v);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double Map(double value, double fromMin, double fromMax, double toMin, double toMax)
    {
        return (value - fromMin) / (fromMax - fromMin) * (toMax - toMin) + toMin;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double DistanceToLine(Vector2d point, Vector2d start, Vector2d end)
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
    public static bool LineIntersects(Vector2d startA, Vector2d endA, Vector2d startB, Vector2d endB)
    {
        return ((endB.Y - startA.Y) * (startB.X - startA.X) > (startB.Y - startA.Y) * (endB.X - startA.X)) != ((endB.Y - endA.Y) * (startB.X - endA.X) > (startB.Y - endA.Y) * (endB.X - endA.X)) && ((startB.Y - startA.Y) * (endA.X - startA.X) > (endA.Y - startA.Y) * (startB.X - startA.X)) != ((endB.Y - startA.Y) * (endA.X - startA.X) > (endA.Y - startA.Y) * (endB.X - startA.X));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double NormalToDegrees(Vector2d normal)
    {
        return Math.Atan2(normal.Y, normal.X) * (180 / Math.PI);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2d DegreesToNormal(double degrees)
    {
        double radians = degrees * (Math.PI / 180);
        return new Vector2d(Math.Cos(radians), Math.Sin(radians));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float DistanceTo(Vector3d posA, Vector3d posB, double xSize, double zSize)
    {
        double x = posA.X - posB.X;
        x /= xSize;

        double y = posA.Y - posB.Y;

        double z = posA.Z - posB.Z;
        z /= zSize;

        return (float)Math.Sqrt((x * x) + (y * y) + (z * z));
    }
}