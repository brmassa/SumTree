namespace SumTree;

/// <summary>
/// Represents an edit operation that can be applied to a SumTree.
/// Supports both insert and remove operations for efficient batch editing.
/// </summary>
/// <typeparam name="T">The type of elements in the tree.</typeparam>
public abstract class Edit<T> where T : IEquatable<T>
{
    /// <summary>
    /// Gets the position where this edit should be applied.
    /// </summary>
    public abstract long Position { get; }

    /// <summary>
    /// Gets the key for this edit operation (used for sorting and conflict resolution).
    /// </summary>
    public abstract long Key { get; }

    /// <summary>
    /// Applies this edit to the specified tree.
    /// </summary>
    /// <param name="tree">The tree to apply the edit to.</param>
    /// <returns>A new tree with the edit applied.</returns>
    public abstract SumTree<T> Apply(SumTree<T> tree);

    /// <summary>
    /// Gets the length change that this edit will cause.
    /// </summary>
    public abstract long LengthChange { get; }

    /// <summary>
    /// Determines if this edit conflicts with another edit.
    /// </summary>
    /// <param name="other">The other edit to check against.</param>
    /// <returns>True if the edits conflict, false otherwise.</returns>
    public virtual bool ConflictsWith(Edit<T> other)
    {
        return Position == other.Position;
    }
}

/// <summary>
/// Represents an insert edit operation.
/// </summary>
/// <typeparam name="T">The type of elements in the tree.</typeparam>
public class InsertEdit<T> : Edit<T> where T : IEquatable<T>
{
    private readonly long _position;
    private readonly SumTree<T> _content;

    /// <summary>
    /// Initializes a new insert edit.
    /// </summary>
    /// <param name="position">The position to insert at.</param>
    /// <param name="content">The content to insert.</param>
    public InsertEdit(long position, SumTree<T> content)
    {
        _position = position;
        _content = content;
    }

    /// <summary>
    /// Initializes a new insert edit with a single item.
    /// </summary>
    /// <param name="position">The position to insert at.</param>
    /// <param name="item">The item to insert.</param>
    public InsertEdit(long position, T item)
        : this(position, new SumTree<T>(item))
    {
    }

    /// <summary>
    /// Initializes a new insert edit with multiple items.
    /// </summary>
    /// <param name="position">The position to insert at.</param>
    /// <param name="items">The items to insert.</param>
    public InsertEdit(long position, ReadOnlyMemory<T> items)
        : this(position, new SumTree<T>(items))
    {
    }

    /// <inheritdoc/>
    public override long Position => _position;

    /// <inheritdoc/>
    public override long Key => _position;

    /// <inheritdoc/>
    public override long LengthChange => _content.Length;

    /// <summary>
    /// Gets the content being inserted.
    /// </summary>
    public SumTree<T> Content => _content;

    /// <inheritdoc/>
    public override SumTree<T> Apply(SumTree<T> tree)
    {
        return tree.InsertRange(_position, _content);
    }

    /// <inheritdoc/>
    public override string ToString()
    {
        return $"Insert at {_position}: {_content.Length} items";
    }
}

/// <summary>
/// Represents a remove edit operation.
/// </summary>
/// <typeparam name="T">The type of elements in the tree.</typeparam>
public class RemoveEdit<T> : Edit<T> where T : IEquatable<T>
{
    private readonly long _position;
    private readonly long _length;

    /// <summary>
    /// Initializes a new remove edit.
    /// </summary>
    /// <param name="position">The position to remove from.</param>
    /// <param name="length">The length to remove.</param>
    public RemoveEdit(long position, long length)
    {
        _position = position;
        _length = length;
    }

    /// <inheritdoc/>
    public override long Position => _position;

    /// <inheritdoc/>
    public override long Key => _position;

    /// <inheritdoc/>
    public override long LengthChange => -_length;

    /// <summary>
    /// Gets the length of content being removed.
    /// </summary>
    public long Length => _length;

    /// <inheritdoc/>
    public override SumTree<T> Apply(SumTree<T> tree)
    {
        return tree.RemoveRange(_position, _length);
    }

    /// <inheritdoc/>
    public override bool ConflictsWith(Edit<T> other)
    {
        if (other is RemoveEdit<T> removeEdit)
        {
            // Two remove operations conflict if they overlap
            var thisEnd = _position + _length;
            var otherEnd = removeEdit._position + removeEdit._length;
            return !(_position >= otherEnd || removeEdit._position >= thisEnd);
        }

        if (other is InsertEdit<T> insertEdit)
        {
            // Insert conflicts with remove if it's within the remove range
            return insertEdit.Position >= _position && insertEdit.Position < _position + _length;
        }

        return base.ConflictsWith(other);
    }

    /// <inheritdoc/>
    public override string ToString()
    {
        return $"Remove at {_position}: {_length} items";
    }
}

/// <summary>
/// Represents a replace edit operation (combination of remove and insert).
/// </summary>
/// <typeparam name="T">The type of elements in the tree.</typeparam>
public class ReplaceEdit<T> : Edit<T> where T : IEquatable<T>
{
    private readonly long _position;
    private readonly long _removeLength;
    private readonly SumTree<T> _insertContent;

    /// <summary>
    /// Initializes a new replace edit.
    /// </summary>
    /// <param name="position">The position to replace at.</param>
    /// <param name="removeLength">The length to remove.</param>
    /// <param name="insertContent">The content to insert.</param>
    public ReplaceEdit(long position, long removeLength, SumTree<T> insertContent)
    {
        _position = position;
        _removeLength = removeLength;
        _insertContent = insertContent;
    }

    /// <summary>
    /// Initializes a new replace edit with a single item.
    /// </summary>
    /// <param name="position">The position to replace at.</param>
    /// <param name="removeLength">The length to remove.</param>
    /// <param name="item">The item to insert.</param>
    public ReplaceEdit(long position, long removeLength, T item)
        : this(position, removeLength, new SumTree<T>(item))
    {
    }

    /// <summary>
    /// Initializes a new replace edit with multiple items.
    /// </summary>
    /// <param name="position">The position to replace at.</param>
    /// <param name="removeLength">The length to remove.</param>
    /// <param name="items">The items to insert.</param>
    public ReplaceEdit(long position, long removeLength, ReadOnlyMemory<T> items)
        : this(position, removeLength, new SumTree<T>(items))
    {
    }

    /// <inheritdoc/>
    public override long Position => _position;

    /// <inheritdoc/>
    public override long Key => _position;

    /// <inheritdoc/>
    public override long LengthChange => _insertContent.Length - _removeLength;

    /// <summary>
    /// Gets the length of content being removed.
    /// </summary>
    public long RemoveLength => _removeLength;

    /// <summary>
    /// Gets the content being inserted.
    /// </summary>
    public SumTree<T> InsertContent => _insertContent;

    /// <inheritdoc/>
    public override SumTree<T> Apply(SumTree<T> tree) => tree
        .RemoveRange(_position, _removeLength)
        .InsertRange(_position, _insertContent);

    /// <inheritdoc/>
    public override bool ConflictsWith(Edit<T> other)
    {
        if (other is ReplaceEdit<T> replaceEdit)
        {
            // Two replace operations conflict if they overlap
            var thisEnd = _position + _removeLength;
            var otherEnd = replaceEdit._position + replaceEdit._removeLength;
            return !(_position >= otherEnd || replaceEdit._position >= thisEnd);
        }

        if (other is RemoveEdit<T> removeEdit)
        {
            // Replace conflicts with remove if they overlap
            var thisEnd = _position + _removeLength;
            var otherEnd = removeEdit.Position + removeEdit.Length;
            return !(_position >= otherEnd || removeEdit.Position >= thisEnd);
        }

        if (other is InsertEdit<T> insertEdit)
        {
            // Replace conflicts with insert if insert is within the replace range
            return insertEdit.Position >= _position && insertEdit.Position < _position + _removeLength;
        }

        return base.ConflictsWith(other);
    }

    /// <inheritdoc/>
    public override string ToString()
    {
        return $"Replace at {_position}: remove {_removeLength}, insert {_insertContent.Length} items";
    }
}

/// <summary>
/// Provides utilities for working with edit operations.
/// </summary>
public static class EditOperations
{
    /// <summary>
    /// Applies a sequence of edits to a SumTree in the correct order.
    /// </summary>
    /// <typeparam name="T">The type of elements in the tree.</typeparam>
    /// <param name="tree">The tree to apply edits to.</param>
    /// <param name="edits">The edits to apply.</param>
    /// <returns>A new tree with all edits applied.</returns>
    public static SumTree<T> ApplyEdits<T>(this SumTree<T> tree, IEnumerable<Edit<T>> edits)
        where T : IEquatable<T>
    {
        var sortedEdits = edits.OrderBy(e => e.Key).ToList();

        // Check for conflicts
        for (int i = 0; i < sortedEdits.Count - 1; i++)
        {
            if (sortedEdits[i].ConflictsWith(sortedEdits[i + 1]))
            {
                throw new InvalidOperationException(
                    $"Edit conflict detected between edits at positions {sortedEdits[i].Position} and {sortedEdits[i + 1].Position}");
            }
        }

        // Apply edits in reverse order to maintain position validity
        var result = tree;
        for (int i = sortedEdits.Count - 1; i >= 0; i--)
        {
            result = sortedEdits[i].Apply(result);
        }

        return result;
    }

    /// <summary>
    /// Applies a sequence of edits to a SumTree, adjusting positions for preceding edits.
    /// </summary>
    /// <typeparam name="T">The type of elements in the tree.</typeparam>
    /// <param name="tree">The tree to apply edits to.</param>
    /// <param name="edits">The edits to apply.</param>
    /// <returns>A new tree with all edits applied.</returns>
    public static SumTree<T> ApplyEditsWithAdjustment<T>(this SumTree<T> tree, IEnumerable<Edit<T>> edits)
        where T : IEquatable<T>
    {
        var sortedEdits = edits.OrderBy(e => e.Key).ToList();

        // Adjust positions based on preceding edits
        var adjustedEdits = new List<Edit<T>>();
        long totalOffset = 0;

        foreach (var edit in sortedEdits)
        {
            var adjustedPosition = edit.Position + totalOffset;

            Edit<T> adjustedEdit = edit switch
            {
                InsertEdit<T> insert => new InsertEdit<T>(adjustedPosition, insert.Content),
                RemoveEdit<T> remove => new RemoveEdit<T>(adjustedPosition, remove.Length),
                ReplaceEdit<T> replace => new ReplaceEdit<T>(adjustedPosition, replace.RemoveLength,
                    replace.InsertContent),
                _ => throw new NotSupportedException($"Edit type {edit.GetType().Name} is not supported")
            };

            adjustedEdits.Add(adjustedEdit);
            totalOffset += edit.LengthChange;
        }

        // Apply adjusted edits in reverse order
        var result = tree;
        for (int i = adjustedEdits.Count - 1; i >= 0; i--)
        {
            result = adjustedEdits[i].Apply(result);
        }

        return result;
    }

    /// <summary>
    /// Merges overlapping or adjacent edits into single operations where possible.
    /// </summary>
    /// <typeparam name="T">The type of elements in the tree.</typeparam>
    /// <param name="edits">The edits to merge.</param>
    /// <returns>A list of merged edits.</returns>
    public static List<Edit<T>> MergeEdits<T>(IEnumerable<Edit<T>> edits)
        where T : IEquatable<T>
    {
        var sortedEdits = edits.OrderBy(e => e.Key).ToList();
        var merged = new List<Edit<T>>();

        foreach (var edit in sortedEdits)
        {
            if (merged.Count == 0)
            {
                merged.Add(edit);
                continue;
            }

            var lastEdit = merged[merged.Count - 1];

            // Try to merge with the last edit
            if (TryMergeEdits(lastEdit, edit, out var mergedEdit))
            {
                merged[merged.Count - 1] = mergedEdit;
            }
            else
            {
                merged.Add(edit);
            }
        }

        return merged;
    }

    /// <summary>
    /// Tries to merge two edits into a single edit.
    /// </summary>
    /// <typeparam name="T">The type of elements in the tree.</typeparam>
    /// <param name="first">The first edit.</param>
    /// <param name="second">The second edit.</param>
    /// <param name="merged">The merged edit if successful.</param>
    /// <returns>True if the edits were merged, false otherwise.</returns>
    private static bool TryMergeEdits<T>(Edit<T> first, Edit<T> second, out Edit<T> merged)
        where T : IEquatable<T>
    {
        merged = null!;

        // Can only merge adjacent or overlapping edits
        if (first is InsertEdit<T> firstInsert && second is InsertEdit<T> secondInsert)
        {
            if (firstInsert.Position + firstInsert.Content.Length == secondInsert.Position)
            {
                var combinedContent = firstInsert.Content + secondInsert.Content;
                merged = new InsertEdit<T>(firstInsert.Position, combinedContent);
                return true;
            }
        }

        if (first is RemoveEdit<T> firstRemove && second is RemoveEdit<T> secondRemove)
        {
            if (firstRemove.Position + firstRemove.Length == secondRemove.Position)
            {
                merged = new RemoveEdit<T>(firstRemove.Position, firstRemove.Length + secondRemove.Length);
                return true;
            }
        }

        return false;
    }
}