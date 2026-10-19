namespace SumTree;

static class SnapshotMemory
{
    /// <summary>Copies mutable storage once at the API boundary; strings are already immutable.</summary>
    internal static ReadOnlyMemory<T> Copy<T>(ReadOnlyMemory<T> memory)
    {
        if (memory.IsEmpty)
        {
            return ReadOnlyMemory<T>.Empty;
        }

        if (typeof(T) == typeof(char) && MemoryMarshal.TryGetString(
                Unsafe.As<ReadOnlyMemory<T>, ReadOnlyMemory<char>>(ref memory), out _, out _, out _))
        {
            return memory;
        }

        return memory.ToArray();
    }
}
