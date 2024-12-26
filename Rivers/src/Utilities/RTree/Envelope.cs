using System;
using System.Runtime.InteropServices;

namespace Rivers;

[StructLayout(LayoutKind.Sequential)]
public class Envelope
{
    public double MinX;
    public double MinY;
    public double MaxX;
    public double MaxY;

    public Envelope(double MinX, double MinY, double MaxX, double MaxY)
    {
        this.MinX = MinX;
        this.MinY = MinY;
        this.MaxX = MaxX;
        this.MaxY = MaxY;
    }

    public double Area =>
        Math.Max(MaxX - MinX, 0) * Math.Max(MaxY - MinY, 0);

    public double Margin =>
        Math.Max(MaxX - MinX, 0) + Math.Max(MaxY - MinY, 0);

    public Envelope Extend(in Envelope other)
    {
        return new(
            MinX: Math.Min(MinX, other.MinX),
            MinY: Math.Min(MinY, other.MinY),
            MaxX: Math.Max(MaxX, other.MaxX),
            MaxY: Math.Max(MaxY, other.MaxY));
    }

    public Envelope Intersection(in Envelope other)
    {
        return new(
            MinX: Math.Max(MinX, other.MinX),
            MinY: Math.Max(MinY, other.MinY),
            MaxX: Math.Min(MaxX, other.MaxX),
            MaxY: Math.Min(MaxY, other.MaxY));
    }

    public bool Contains(in Envelope other)
    {
        return MinX <= other.MinX &&
        MinY <= other.MinY &&
        MaxX >= other.MaxX &&
        MaxY >= other.MaxY;
    }

    public bool Intersects(in Envelope other)
    {
        return MinX <= other.MaxX &&
        MinY <= other.MaxY &&
        MaxX >= other.MinX &&
        MaxY >= other.MinY;
    }

    public static Envelope InfiniteBounds { get; } =
        new(
            MinX: double.NegativeInfinity,
            MinY: double.NegativeInfinity,
            MaxX: double.PositiveInfinity,
            MaxY: double.PositiveInfinity);

    public static Envelope EmptyBounds { get; } =
        new(
            MinX: double.PositiveInfinity,
            MinY: double.PositiveInfinity,
            MaxX: double.NegativeInfinity,
            MaxY: double.NegativeInfinity);
}