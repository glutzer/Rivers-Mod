using System.Collections.Generic;

namespace Rivers;

public interface ISpatialIndex<out T>
{
    public IReadOnlyList<T> Search();

    public IReadOnlyList<T> Search(in Envelope boundingBox);
}