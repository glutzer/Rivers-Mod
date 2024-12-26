using System.Collections.Generic;

namespace Rivers;

public partial class RBush<T>
{
    public class Node : ISpatialData
    {
        private Envelope _envelope = null!;

        internal Node(List<ISpatialData> items, int height)
        {
            Height = height;
            Items = items;
            ResetEnvelope();
        }

        internal void Add(ISpatialData node)
        {
            Items.Add(node);
            _envelope = Envelope.Extend(node.Envelope);
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
            _envelope = GetEnclosingEnvelope(Items);
        }

        internal readonly List<ISpatialData> Items;

        public IReadOnlyList<ISpatialData> Children => Items;

        public int Height { get; }

        public bool IsLeaf => Height == 1;

        public Envelope Envelope => _envelope;
    }
}