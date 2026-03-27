using System.Collections.Generic;

namespace Rivers;

public partial class RBush<T>
{
    public class Node : ISpatialData
    {
        internal Node(List<ISpatialData> items, int height)
        {
            Height = height;
            Items = items;
            ResetEnvelope();
        }

        internal void Add(ISpatialData node)
        {
            Items.Add(node);
            Envelope = Envelope.Extend(node.Envelope);
        }

        internal void Remove(ISpatialData node)
        {
            _ = Items.Remove(node);
            ResetEnvelope();
        }

        internal void RemoveRange(int index, int count)
        {
            Items.RemoveRange(index, count);
            ResetEnvelope();
        }

        internal void ResetEnvelope()
        {
            Envelope = GetEnclosingEnvelope(Items);
        }

        internal readonly List<ISpatialData> Items;

        public IReadOnlyList<ISpatialData> Children => Items;

        public int Height { get; }

        public bool IsLeaf => Height == 1;

        public Envelope Envelope { get; private set; } = null!;
    }
}