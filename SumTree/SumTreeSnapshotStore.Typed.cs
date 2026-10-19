namespace SumTree;

/// <summary>Atomically publishes typed snapshots; coordinate read/edit/publish sequences between writers.</summary>
/// <remarks>Elements, summaries, and any borrowed storage must satisfy the typed tree's immutability contract.</remarks>
public sealed class SumTreeSnapshotStore<T, TSummary, TOps>
    where T : IEquatable<T>
    where TSummary : struct
    where TOps : struct, ISummaryOperations<T, TSummary>
{
    Snapshot _current;

    public SumTreeSnapshotStore(SumTree<T, TSummary, TOps> initial) => _current = new(initial);
    public SumTree<T, TSummary, TOps> Current => Volatile.Read(ref _current).Tree;
    public void Publish(SumTree<T, TSummary, TOps> tree) => Volatile.Write(ref _current, new Snapshot(tree));

    sealed class Snapshot(SumTree<T, TSummary, TOps> tree)
    {
        internal SumTree<T, TSummary, TOps> Tree { get; } = tree;
    }
}
