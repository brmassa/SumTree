using System.Collections;

namespace SumTree.Cursors;

/// <summary>
/// A cursor implementation for efficient navigation through a SumTree.
/// Provides O(log n) seeking and O(1) traversal operations.
/// </summary>
/// <typeparam name="T">The type of elements in the tree.</typeparam>
/// <typeparam name="TDimension">The dimension type used for seeking.</typeparam>
public class SumTreeCursor<T, TDimension> : ICursor<T, TDimension>
    where T : IEquatable<T>
    where TDimension : IEquatable<TDimension>
{
    private readonly SumTree<T> _tree;
    private readonly ISummaryDimension<T, TDimension> _dimension;
    private readonly Stack<StackEntry> _stack;
    private TDimension _position;
    private bool _didSeek;
    private bool _atEnd;

    /// <summary>
    /// Represents a stack entry for tree navigation.
    /// </summary>
    private struct StackEntry
    {
        public SumTree<T> Tree { get; }
        public int Index { get; set; }
        public TDimension Position { get; set; }

        public StackEntry(SumTree<T> tree, int index, TDimension position)
        {
            Tree = tree;
            Index = index;
            Position = position;
        }
    }

    /// <summary>
    /// Initializes a new cursor for the specified tree and dimension.
    /// </summary>
    /// <param name="tree">The tree to navigate.</param>
    /// <param name="dimension">The dimension to use for seeking.</param>
    public SumTreeCursor(SumTree<T> tree, ISummaryDimension<T, TDimension> dimension)
    {
        _tree = tree;
        _dimension = dimension ?? throw new ArgumentNullException(nameof(dimension));
        _stack = new Stack<StackEntry>();
        _position = dimension.Identity;
        _didSeek = false;
        _atEnd = tree.IsEmpty;

        Reset();
    }

    /// <inheritdoc/>
    public T? Item => GetCurrentItem();

    /// <inheritdoc/>
    public TDimension? ItemSummary => GetCurrentItemSummary();

    /// <inheritdoc/>
    public bool IsAtStart => !_didSeek || (_position.Equals(_dimension.Identity) && _stack.Count <= 1);

    /// <inheritdoc/>
    public bool IsAtEnd => _atEnd;

    /// <inheritdoc/>
    public TDimension Position => _position;

    /// <inheritdoc/>
    public TDimension End => _tree.GetSummary(_dimension);

    /// <inheritdoc/>
    public void Start()
    {
        Reset();
        _didSeek = true;
    }

    /// <inheritdoc/>
    public bool Next()
    {
        if (_atEnd) return false;

        if (!_didSeek)
        {
            Start();
            return !_atEnd;
        }

        return NextInternal();
    }

    /// <inheritdoc/>
    public bool Previous()
    {
        if (IsAtStart) return false;
        return PreviousInternal();
    }

    /// <inheritdoc/>
    public void Seek(TDimension target, Bias bias = Bias.Left)
    {
        SeekInternal(target, bias);
        _didSeek = true;
    }

    /// <inheritdoc/>
    public void SeekForward(TDimension target, Bias bias = Bias.Left)
    {
        if (_dimension.Compare(target, _position) <= 0) return;
        SeekInternal(target, bias);
    }

    /// <inheritdoc/>
    public SumTree<T> Slice(TDimension target, Bias bias = Bias.Left)
    {
        var startPosition = _position;
        var endCursor = Clone();
        endCursor.Seek(target, bias);

        var startIndex = FindIndexForPosition(startPosition);
        var endIndex = FindIndexForPosition(endCursor.Position);

        // For Bias.Left, we want to include the target position in the slice
        // So we need to add 1 to the end index if we're not at the end
        if (bias == Bias.Left && endIndex < _tree.Length)
        {
            endIndex++;
        }

        if (startIndex >= endIndex) return SumTree<T>.Empty;

        return _tree.Slice(startIndex, endIndex - startIndex);
    }

    /// <inheritdoc/>
    public SumTree<T> Suffix()
    {
        var startIndex = FindIndexForPosition(_position);
        if (startIndex >= _tree.Length) return SumTree<T>.Empty;

        return _tree.Slice(startIndex);
    }

    /// <inheritdoc/>
    public TDimension Summary()
    {
        var suffix = Suffix();
        return suffix.GetSummary(_dimension);
    }

    /// <inheritdoc/>
    public bool SearchBackward(Func<TDimension, bool> predicate)
    {
        var tempPosition = _position;

        while (!IsAtStart)
        {
            // Check if the current position satisfies the predicate before moving
            if (predicate(_position))
            {
                return true;
            }

            if (!PreviousInternal()) break;
        }

        // Check the start position as well
        if (predicate(_position))
        {
            return true;
        }

        // Restore position if not found
        Seek(tempPosition);
        return false;
    }

    /// <inheritdoc/>
    public bool SearchForward(Func<TDimension, bool> predicate)
    {
        var tempPosition = _position;

        while (!_atEnd)
        {
            // Check if including the current character would satisfy the predicate
            var currentItem = Item;
            if (currentItem != null)
            {
                var itemSummary = _dimension.SummarizeElement(currentItem);
                var positionAfterItem = _dimension.Combine(_position, itemSummary);

                if (predicate(positionAfterItem))
                {
                    // The current character satisfies the predicate, so return true
                    // without advancing the cursor
                    return true;
                }
            }

            if (!NextInternal()) break;
        }

        // Restore position if not found
        Seek(tempPosition);
        return false;
    }

    /// <inheritdoc/>
    public ICursor<T, TDimension> Clone()
    {
        var clone = new SumTreeCursor<T, TDimension>(_tree, _dimension);
        clone.Seek(_position);
        return clone;
    }


    /// <inheritdoc/>
    public void Reset()
    {
        _stack.Clear();
        _position = _dimension.Identity;
        _atEnd = _tree.IsEmpty;
        _didSeek = false;

        if (!_tree.IsEmpty)
        {
            _stack.Push(new StackEntry(_tree, 0, _dimension.Identity));
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _stack.Clear();
    }

    private T? GetCurrentItem()
    {
        if (_atEnd || _stack.Count == 0) return default(T?);

        var current = _stack.Peek();
        if (current.Tree.IsNode)
        {
            // Navigate to leaf
            NavigateToLeaf();
            if (_stack.Count == 0) return default;
            current = _stack.Peek();
        }

        if (current.Index < current.Tree.Length)
        {
            return current.Tree.ElementAt(current.Index);
        }

        return default(T?);
    }

    private TDimension? GetCurrentItemSummary()
    {
        var item = GetCurrentItem();
        if (item == null) return default;

        return _dimension.SummarizeElement(item);
    }

    private bool NextInternal()
    {
        if (_stack.Count == 0) return false;

        var current = _stack.Pop();
        current.Index++;

        // Update position
        if (current.Index <= current.Tree.Length)
        {
            var item = current.Tree.ElementAt(current.Index - 1);
            var itemSummary = _dimension.SummarizeElement(item);
            _position = _dimension.Combine(_position, itemSummary);

        }

        if (current.Index < current.Tree.Length)
        {
            _stack.Push(current);
            return true;
        }

        // Move to next sibling or parent
        while (_stack.Count > 0)
        {
            var parent = _stack.Pop();
            parent.Index++;

            if (parent.Index < (parent.Tree.IsNode ? 2 : parent.Tree.Length))
            {
                _stack.Push(parent);
                return true;
            }
        }

        _atEnd = true;
        return false;
    }

    private bool PreviousInternal()
    {
        if (_stack.Count == 0) return false;

        var current = _stack.Peek();
        if (current.Index > 0)
        {
            // Move backward within current leaf
            var updated = _stack.Pop();
            updated.Index--;

            // Recalculate position from start of current leaf
            var leafPosition = updated.Position;
            for (var i = 0; i < updated.Index; i++)
            {
                var item = updated.Tree.ElementAt(i);
                var itemSummary = _dimension.SummarizeElement(item);
                leafPosition = _dimension.Combine(leafPosition, itemSummary);
            }

            _position = leafPosition;
            _stack.Push(updated);
            return true;
        }

        // Need to move to previous node
        if (_stack.Count == 0) return false;

        _stack.Pop(); // Remove current leaf

        // Navigate up the tree to find previous position
        while (_stack.Count > 0)
        {
            var parent = _stack.Pop();
            if (parent.Index > 0)
            {
                // Go to previous child
                parent.Index--;
                _stack.Push(parent);

                // Navigate to rightmost position in the previous subtree
                NavigateToRightmostLeaf();
                return true;
            }
        }

        // Reached the start
        return false;
    }

    private void NavigateToRightmostLeaf()
    {
        while (_stack.Count > 0)
        {
            var current = _stack.Peek();
            if (current.Tree.IsNode)
            {
                var entry = _stack.Pop();
                if (entry.Index == 0)
                {
                    // Go to left child
                    _stack.Push(new StackEntry(current.Tree.Left, 0, entry.Position));
                }
                else
                {
                    // Go to right child
                    var leftSummary = current.Tree.Left.GetSummary(_dimension);
                    var leftEnd = _dimension.Combine(entry.Position, leftSummary);
                    _stack.Push(new StackEntry(current.Tree.Right, 1, leftEnd));
                }
            }
            else
            {
                // Reached a leaf, position at the last element
                var leaf = _stack.Pop();
                var lastIndex = (int)leaf.Tree.Length - 1;

                // Calculate position up to (but not including) last element
                var leafPosition = leaf.Position;
                for (var i = 0; i < lastIndex; i++)
                {
                    var item = leaf.Tree.ElementAt(i);
                    var itemSummary = _dimension.SummarizeElement(item);
                    leafPosition = _dimension.Combine(leafPosition, itemSummary);
                }

                _position = leafPosition;
                _stack.Push(new StackEntry(leaf.Tree, lastIndex, leafPosition));
                break;
            }
        }
    }

    private void SeekInternal(TDimension target, Bias bias)
    {
        _stack.Clear();
        _position = _dimension.Identity;
        _atEnd = false;

        if (_tree.IsEmpty)
        {
            _atEnd = true;
            return;
        }

        SeekInTree(_tree, target, bias, _dimension.Identity);
    }

    private void SeekInTree(SumTree<T> tree, TDimension target, Bias bias, TDimension currentPosition)
    {
        if (tree.IsNode)
        {
            var leftSummary = tree.Left.GetSummary(_dimension);
            var leftEnd = _dimension.Combine(currentPosition, leftSummary);

            var comparison = _dimension.Compare(target, leftEnd);
            if (comparison < 0 || (comparison == 0 && bias == Bias.Left))
            {
                _stack.Push(new StackEntry(tree, 0, currentPosition));
                SeekInTree(tree.Left, target, bias, currentPosition);
            }
            else
            {
                _stack.Push(new StackEntry(tree, 1, leftEnd));
                SeekInTree(tree.Right, target, bias, leftEnd);
            }
        }
        else
        {
            // Find position in leaf
            var leafPosition = currentPosition;
            var index = 0;

            for (var i = 0; i < tree.Length; i++)
            {
                var item = tree.ElementAt(i);
                var itemSummary = _dimension.SummarizeElement(item);
                var newPosition = _dimension.Combine(leafPosition, itemSummary);

                var comparison = _dimension.Compare(target, newPosition);
                if (comparison < 0 || (comparison == 0 && bias == Bias.Left))
                {
                    break;
                }

                leafPosition = newPosition;
                index = i + 1;
            }

            _stack.Push(new StackEntry(tree, index, leafPosition));
            _position = leafPosition;
            _atEnd = index >= tree.Length;
        }
    }

    private void NavigateToLeaf()
    {
        while (_stack.Count > 0)
        {
            var current = _stack.Peek();
            if (!current.Tree.IsNode) break;

            var entry = _stack.Pop();
            if (entry.Index == 0)
            {
                _stack.Push(new StackEntry(current.Tree.Left, 0, entry.Position));
            }
            else
            {
                var leftSummary = current.Tree.Left.GetSummary(_dimension);
                var leftEnd = _dimension.Combine(entry.Position, leftSummary);
                _stack.Push(new StackEntry(current.Tree.Right, 0, leftEnd));
            }
        }
    }

    private long FindIndexForPosition(TDimension position)
    {
        // Simple linear search for now - could be optimized
        long index = 0;
        var currentPosition = _dimension.Identity;

        while (index < _tree.Length && _dimension.Compare(currentPosition, position) < 0)
        {
            var item = _tree.ElementAt(index);
            var itemSummary = _dimension.SummarizeElement(item);
            currentPosition = _dimension.Combine(currentPosition, itemSummary);
            index++;
        }

        return index;
    }
}
