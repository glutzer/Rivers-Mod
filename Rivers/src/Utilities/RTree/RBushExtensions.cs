using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

namespace Rivers;

public static class RBushExtensions
{
    [StructLayout(LayoutKind.Sequential)]
    private record struct ItemDistance<T>(T Item, double Distance);

    public static IReadOnlyList<T> Knn<T>(
        this ISpatialIndex<T> tree,
        int k,
        double x,
        double y,
        double? maxDistance = null,
        Func<T, bool>? predicate = null)
        where T : ISpatialData
    {
        ArgumentNullException.ThrowIfNull(tree);

        IReadOnlyList<T> items = maxDistance == null
            ? tree.Search()
            : tree.Search(
                new Envelope(
                    MinX: x - maxDistance.Value,
                    MinY: y - maxDistance.Value,
                    MaxX: x + maxDistance.Value,
                    MaxY: y + maxDistance.Value));

        IEnumerable<ItemDistance<T>> distances = items
            .Select(i => new ItemDistance<T>(i, i.Envelope.DistanceTo(x, y)))
            .OrderBy(i => i.Distance)
            .AsEnumerable();

        if (maxDistance.HasValue)
            distances = distances.TakeWhile(i => i.Distance <= maxDistance.Value);

        if (predicate != null)
            distances = distances.Where(i => predicate(i.Item));

        if (k > 0)
            distances = distances.Take(k);

        return distances
            .Select(i => i.Item)
            .ToList();
    }

    public static double DistanceTo(this Envelope envelope, double x, double y)
    {
        double dX = AxisDistance(x, envelope.MinX, envelope.MaxX);
        double dY = AxisDistance(y, envelope.MinY, envelope.MaxY);
        return Math.Sqrt((dX * dX) + (dY * dY));

        static double AxisDistance(double p, double min, double max)
        {
            return p < min ? min - p :
           p > max ? p - max :
           0;
        }
    }
}