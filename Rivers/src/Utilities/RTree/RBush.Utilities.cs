using System;
using System.Collections.Generic;
using System.Linq;

namespace Rivers;

public partial class RBush<T>
{
    #region Sort Functions
    private static readonly IComparer<ISpatialData> s_compareMinX =
        Comparer<ISpatialData>.Create((x, y) => Comparer<double>.Default.Compare(x.Envelope.MinX, y.Envelope.MinX));
    private static readonly IComparer<ISpatialData> s_compareMinY =
        Comparer<ISpatialData>.Create((x, y) => Comparer<double>.Default.Compare(x.Envelope.MinY, y.Envelope.MinY));
    #endregion

    #region Search
    private List<T> DoSearch(in Envelope boundingBox)
    {
        if (!Root.Envelope.Intersects(boundingBox))
            return [];

        List<T> intersections = [];
        Queue<Node> queue = new();
        queue.Enqueue(Root);

        while (queue.Count != 0)
        {
            Node item = queue.Dequeue();

            if (item.IsLeaf)
            {
                foreach (ISpatialData i in item.Items)
                {
                    if (i.Envelope.Intersects(boundingBox))
                        intersections.Add((T)i);
                }
            }
            else
            {
                foreach (ISpatialData i in item.Items)
                {
                    if (i.Envelope.Intersects(boundingBox))
                        queue.Enqueue((Node)i);
                }
            }
        }

        return intersections;
    }
    #endregion

    #region Insert
    private List<Node> FindCoveringArea(in Envelope area, int depth)
    {
        List<Node> path = [];
        Node node = Root;

        while (true)
        {
            path.Add(node);
            if (node.IsLeaf || path.Count == depth) return path;

            ISpatialData next = node.Items[0];
            double nextArea = next.Envelope.Extend(area).Area;

            foreach (ISpatialData i in node.Items)
            {
                double newArea = i.Envelope.Extend(area).Area;
                if (newArea > nextArea)
                    continue;

                if (newArea == nextArea
                    && i.Envelope.Area >= next.Envelope.Area)
                {
                    continue;
                }

                next = i;
                nextArea = newArea;
            }

            node = (next as Node)!;
        }
    }

    private void Insert(ISpatialData data, int depth)
    {
        List<Node> path = FindCoveringArea(data.Envelope, depth);

        Node insertNode = path[^1];
        insertNode.Add(data);

        while (--depth >= 0)
        {
            if (path[depth].Items.Count > _maxEntries)
            {
                Node newNode = SplitNode(path[depth]);
                if (depth == 0)
                    SplitRoot(newNode);
                else
                    path[depth - 1].Add(newNode);
            }
            else
            {
                path[depth].ResetEnvelope();
            }
        }
    }

    #region SplitNode
    private void SplitRoot(Node newNode)
    {
        List<ISpatialData> items =
        [
            Root,
            newNode
        ];

        Root = new Node(items, Root.Height + 1);
    }

    private Node SplitNode(Node node)
    {
        SortChildren(node);

        int splitPoint = GetBestSplitIndex(node.Items);
        List<ISpatialData> newChildren = node.Items.Skip(splitPoint).ToList();
        node.RemoveRange(splitPoint, node.Items.Count - splitPoint);
        return new Node(newChildren, node.Height);
    }

    #region SortChildren
    private void SortChildren(Node node)
    {
        node.Items.Sort(s_compareMinX);
        double splitsByX = GetPotentialSplitMargins(node.Items);
        node.Items.Sort(s_compareMinY);
        double splitsByY = GetPotentialSplitMargins(node.Items);

        if (splitsByX < splitsByY)
            node.Items.Sort(s_compareMinX);
    }

    private double GetPotentialSplitMargins(List<ISpatialData> children)
    {
        return GetPotentialEnclosingMargins(children) +
        GetPotentialEnclosingMargins(children.AsEnumerable().Reverse().ToList());
    }

    private double GetPotentialEnclosingMargins(List<ISpatialData> children)
    {
        Envelope envelope = Envelope.EmptyBounds;
        int i = 0;
        for (; i < _minEntries; i++)
        {
            envelope = envelope.Extend(children[i].Envelope);
        }

        double totalMargin = envelope.Margin;
        for (; i < children.Count - _minEntries; i++)
        {
            envelope = envelope.Extend(children[i].Envelope);
            totalMargin += envelope.Margin;
        }

        return totalMargin;
    }
    #endregion

    private int GetBestSplitIndex(List<ISpatialData> children)
    {
        return Enumerable.Range(_minEntries, children.Count - _minEntries)
            .Select(i =>
            {
                Envelope leftEnvelope = GetEnclosingEnvelope(children.Take(i));
                Envelope rightEnvelope = GetEnclosingEnvelope(children.Skip(i));

                double overlap = leftEnvelope.Intersection(rightEnvelope).Area;
                double totalArea = leftEnvelope.Area + rightEnvelope.Area;
                return new { i, overlap, totalArea };
            })
            .OrderBy(x => x.overlap)
            .ThenBy(x => x.totalArea)
            .Select(x => x.i)
            .First();
    }
    #endregion
    #endregion

    #region BuildTree
    private Node BuildTree(T[] data)
    {
        int treeHeight = GetDepth(data.Length);
        int rootMaxEntries = (int)Math.Ceiling(data.Length / Math.Pow(_maxEntries, treeHeight - 1));
        return BuildNodes(new ArraySegment<T>(data), treeHeight, rootMaxEntries);
    }

    private int GetDepth(int numNodes)
    {
        return (int)Math.Ceiling(Math.Log(numNodes) / Math.Log(_maxEntries));
    }

    private Node BuildNodes(ArraySegment<T> data, int height, int maxEntries)
    {
        if (data.Count <= maxEntries)
        {
            List<ISpatialData> list =
            [
                BuildNodes(data, height - 1, _maxEntries)
            ];

            return height == 1
                ? new Node(data.Cast<ISpatialData>().ToList(), height)
                : new Node(
                    list,
                    height);
        }

        ArraySegment<T> byX = new(data.OrderBy(i => i.Envelope.MinX).ToArray());

        int nodeSize = (data.Count + (maxEntries - 1)) / maxEntries;
        int subSortLength = nodeSize * (int)Math.Ceiling(Math.Sqrt(maxEntries));

        List<ISpatialData> children = new(maxEntries);
        foreach (ArraySegment<T> subData in Chunk(byX, subSortLength))
        {
            ArraySegment<T> byY = new(subData.OrderBy(d => d.Envelope.MinY).ToArray());

            foreach (ArraySegment<T> nodeData in Chunk(byY, nodeSize))
            {
                children.Add(BuildNodes(nodeData, height - 1, _maxEntries));
            }
        }

        return new Node(children, height);
    }

    private static IEnumerable<ArraySegment<T>> Chunk(ArraySegment<T> values, int chunkSize)
    {
        int start = 0;
        while (start < values.Count)
        {
            int len = Math.Min(values.Count - start, chunkSize);
            yield return new ArraySegment<T>(values.Array!, values.Offset + start, len);
            start += chunkSize;
        }
    }
    #endregion

    private static Envelope GetEnclosingEnvelope(IEnumerable<ISpatialData> items)
    {
        Envelope envelope = Envelope.EmptyBounds;
        foreach (ISpatialData data in items)
            envelope = envelope.Extend(data.Envelope);

        return envelope;
    }

    private static List<T> GetAllChildren(List<T> list, Node n)
    {
        if (n.IsLeaf)
        {
            list.AddRange(n.Items.Cast<T>());
        }
        else
        {
            foreach (Node node in n.Items.Cast<Node>())
                _ = GetAllChildren(list, node);
        }

        return list;
    }
}