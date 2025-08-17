using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace Rivers;

public partial class RBush<T> : ISpatialDatabase<T>, ISpatialIndex<T> where T : ISpatialData
{
    private const int DefaultMaxEntries = 9;
    private const int MinimumMaxEntries = 4;
    private const int MinimumMinEntries = 2;
    private const double DefaultFillFactor = 0.4;

    private readonly IEqualityComparer<T> _comparer;
    private readonly int _maxEntries;
    private readonly int _minEntries;

    public Node Root { get; private set; }

    public Envelope Envelope => Root.Envelope;

    public RBush()
        : this(DefaultMaxEntries, EqualityComparer<T>.Default) { }


    public RBush(int maxEntries)
        : this(maxEntries, EqualityComparer<T>.Default) { }


    public RBush(int maxEntries, IEqualityComparer<T> comparer)
    {
        _comparer = comparer;
        _maxEntries = Math.Max(MinimumMaxEntries, maxEntries);
        _minEntries = Math.Max(MinimumMinEntries, (int)Math.Ceiling(_maxEntries * DefaultFillFactor));

        Clear();
    }

    public int Count { get; private set; }


    [MemberNotNull(nameof(Root))]
    public void Clear()
    {
        Root = new Node([], 1);
        Count = 0;
    }

    public IReadOnlyList<T> Search()
    {
        return GetAllChildren([], Root);
    }

    public IReadOnlyList<T> Search(in Envelope boundingBox)
    {
        return DoSearch(boundingBox);
    }

    public void Insert(T item)
    {
        Insert(item, Root.Height);
        Count++;
    }

    public void BulkLoad(IEnumerable<T> items)
    {
        T[] data = items.ToArray();
        if (data.Length == 0) return;

        if (Root.IsLeaf &&
            Root.Items.Count + data.Length < _maxEntries)
        {
            foreach (T? i in data)
                Insert(i);
            return;
        }

        if (data.Length < _minEntries)
        {
            foreach (T? i in data)
                Insert(i);
            return;
        }

        Node dataRoot = BuildTree(data);
        Count += data.Length;

        if (Root.Items.Count == 0)
        {
            Root = dataRoot;
        }
        else if (Root.Height == dataRoot.Height)
        {
            if (Root.Items.Count + dataRoot.Items.Count <= _maxEntries)
            {
                foreach (ISpatialData isd in dataRoot.Items)
                    Root.Add(isd);
            }
            else
            {
                SplitRoot(dataRoot);
            }
        }
        else
        {
            if (Root.Height < dataRoot.Height)
            {
#pragma warning disable IDE0180
                Node tmp = Root;
                Root = dataRoot;
                dataRoot = tmp;
#pragma warning restore IDE0180
            }

            Insert(dataRoot, Root.Height - dataRoot.Height);
        }
    }

    public bool Delete(T item)
    {
        return DoDelete(Root, item);
    }

    private bool DoDelete(Node node, T item)
    {
        if (!node.Envelope.Contains(item.Envelope))
            return false;

        if (node.IsLeaf)
        {
            int cnt = node.Items.RemoveAll(i => _comparer.Equals((T)i, item));
            if (cnt == 0)
                return false;

            Count -= cnt;
            node.ResetEnvelope();
            return true;

        }

        bool flag = false;
        foreach (ISpatialData n in node.Items)
        {
            flag |= DoDelete((Node)n, item);
        }

        if (flag)
            node.ResetEnvelope();

        return flag;
    }
}