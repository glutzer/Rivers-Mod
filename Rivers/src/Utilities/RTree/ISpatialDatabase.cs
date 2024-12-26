using System.Collections.Generic;

namespace Rivers;

public interface ISpatialDatabase<T> : ISpatialIndex<T>
{
    public void Insert(T item);

    public bool Delete(T item);

    public void Clear();

    public void BulkLoad(IEnumerable<T> items);
}