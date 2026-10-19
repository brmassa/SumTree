namespace SumTree;

/// <summary>Publishes complete SumTree snapshots atomically to concurrent readers.</summary>
/// <remarks>
/// A reader captures <see cref="Current"/> once and can keep that version while a writer
/// publishes new versions. Elements and summaries must be immutable, and dimensions must
/// be pure and thread-safe. Borrowed memory must remain valid and unchanged.
/// Publication does not merge edits: coordinate read/edit/publish sequences with a single
/// writer or an external lock when multiple writers derive changes from the current tree.
/// </remarks>
public sealed class SumTreeSnapshotStore<T> where T : IEquatable<T>
{
    Snapshot _current;

    public SumTreeSnapshotStore(SumTree<T> initial)
    {
        _current = new Snapshot(initial);
    }

    /// <summary>Gets one complete published version, with acquire memory ordering.</summary>
    public SumTree<T> Current => Volatile.Read(ref _current).Tree;

    /// <summary>Publishes a complete version, with release memory ordering.</summary>
    public void Publish(SumTree<T> tree) => Volatile.Write(ref _current, new Snapshot(tree));

    sealed class Snapshot(SumTree<T> tree)
    {
        internal SumTree<T> Tree { get; } = tree;
    }
}
