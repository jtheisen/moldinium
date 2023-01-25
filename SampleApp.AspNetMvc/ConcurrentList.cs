using System.Collections;
using System.Collections.Immutable;
using System.Threading;

namespace Moldinium.Misc;

class LockHelper : IDisposable
{
    SpinLock spinLock;

    Int32 depth;

    public LockHelper Lock()
    {
        if (++depth > 1)
        {
            if (!spinLock.IsHeldByCurrentThread) throw new Exception($"Expected to hold the lock");

            return this;
        }

        Boolean lockTaken = false;

        spinLock.Enter(ref lockTaken);

        if (!lockTaken) throw new Exception("Can't take lock");

        return this;
    }

    public void Dispose()
    {
        if (--depth > 0) return;

        spinLock.Exit();
    }
}

/// <summary>
/// A simple but likely quite inefficient implementation of a concurrent list that
/// is meant only for demonstration purposes.
/// </summary>
public class ConcurrentList<T> : IList<T>
{
    IImmutableList<T> list = ImmutableList<T>.Empty;

    LockHelper locker = new LockHelper();
	
	public T this[int index]
    {
        get => list[index];
        set
        {
            using var _ = locker.Lock();

            list = list.SetItem(index, value);
        }
    }

    public int Count => list.Count;

    public bool IsReadOnly => false;

    public void Add(T item)
    {
        using var _ = locker.Lock();

        list = list.Add(item);
    }

    public void Clear()
    {
        using var _ = locker.Lock();

        list = list.Clear();
    }

    public bool Contains(T item) => list.Contains(item);

    public void CopyTo(T[] array, int arrayIndex) => list.ToArray().CopyTo(array, arrayIndex);

    public IEnumerator<T> GetEnumerator() => list.GetEnumerator();

    public int IndexOf(T item) => list.IndexOf(item);

    public void Insert(int index, T item)
    {
        using var _ = locker.Lock();

        list = list.Insert(index, item);
    }

    public bool Remove(T item)
    {
        using var _ = locker.Lock();

        var i = list.IndexOf(item);

        if (i < 0)
        {
            return false;
        }
        else
        {
            list = list.RemoveAt(i);

            return true;
        }
    }

    public void RemoveAt(int index)
    {
        using var _ = locker.Lock();

        list.RemoveAt(index);
    }

    IEnumerator IEnumerable.GetEnumerator() => list.GetEnumerator();
}
