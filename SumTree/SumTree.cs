using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace SumTree;

/// <summary>
/// A SumTree is a high-performance data structure that directly incorporates the Rope tree structure
/// with integrated summary dimensions. It eliminates the composition overhead by directly implementing
/// the tree structure with integrated summary computation for maximum performance.
/// </summary>
/// <typeparam name="T">The type of elements, must be <see cref="IEquatable{T}"/>.</typeparam>
public readonly struct SumTree<T> : IEnumerable<T>, IReadOnlyList<T>, IEquatable<SumTree<T>>
    where T : IEquatable<T>
{
    /// <summary>
    /// Maximum tree depth allowed.
    /// </summary>
    private const int MaxTreeDepth = 46;
    private static readonly ThreadLocal<int> BalancedCallDepth = new(() => 0);

    /// <summary>
    /// Defines the maximum depth discrepancy between left and right to cause a re-split of one side when balancing.
    /// </summary>
    public const int MaxDepthImbalance = 4;

    /// <summary>
    /// Static size of the maximum length of a leaf node. This is calculated to never require Large Object Heap allocations.
    /// </summary>
    public static readonly int MaxLeafLength = CalculateAlignedBufferLength<T>();

    /// <summary>
    /// Defines the Empty leaf.
    /// </summary>
    public static readonly SumTree<T> Empty = [];

    /// <summary>
    /// Defines the minimum lengths the leaves should be in relation to the depth of the tree.
    /// </summary>
    private static readonly int[] DepthToFibonnaciPlusTwo =
        Enumerable.Range(0, MaxTreeDepth).Select(d => Fibonnaci(d) + 2).ToArray();

    private readonly object data;
    private readonly Dictionary<Type, object>? dimensions;
    private readonly Dictionary<Type, object>? summaries;

    /// <summary>
    /// Creates a new empty SumTree.
    /// </summary>
    public SumTree()
    {
        this.data = ReadOnlyMemory<T>.Empty;
        this.IsBalanced = true;
        this.BufferCount = 1;
        this.dimensions = null;
        this.summaries = null;
    }

    /// <summary>
    /// Creates a new SumTree with a single value.
    /// </summary>
    public SumTree(T value)
    {
        this.data = ValueTuple.Create(value);
        this.IsBalanced = true;
        this.Length = 1;
        this.BufferCount = 1;
        this.dimensions = null;
        this.summaries = null;
    }

    /// <summary>
    /// Creates a new SumTree from memory with dimensions.
    /// </summary>
    public SumTree(ReadOnlyMemory<T> memory, Dictionary<Type, object>? dimensions = null)
    {
        if (memory.IsEmpty)
        {
            this = Empty;
            if (dimensions is { Count: > 0 })
            {
                this.dimensions = new Dictionary<Type, object>(dimensions);
                this.summaries = ComputeEmptySummaries(dimensions);
            }

            return;
        }

        this.data = memory;
        this.IsBalanced = true;
        this.Length = memory.Length;
        this.BufferCount = 1;
        this.dimensions = dimensions?.Count > 0 ? new Dictionary<Type, object>(dimensions) : null;
        this.summaries = this.dimensions != null ? ComputeSummariesForMemory(memory, this.dimensions) : null;
    }

    /// <summary>
    /// Internal constructor for node creation.
    /// </summary>
    private SumTree(
        object data,
        long length,
        int depth,
        int bufferCount,
        bool isBalanced,
        Dictionary<Type, object>? dimensions = null,
        Dictionary<Type, object>? summaries = null)
    {
        this.data = data;
        this.Length = length;
        this.Depth = depth;
        this.BufferCount = bufferCount;
        this.IsBalanced = isBalanced;
        this.dimensions = dimensions;
        this.summaries = summaries;
    }

    /// <summary>
    /// Creates a new SumTree by combining two existing SumTree instances.
    /// This constructor provides an alternative to using the + operator for combining trees.
    /// </summary>
    /// <param name="left">The left SumTree to combine</param>
    /// <param name="right">The right SumTree to combine</param>
    /// <example>
    /// <code>
    /// var left = "Hello".ToSumTree();
    /// var right = " World".ToSumTree();
    /// var combined = new SumTree&lt;char&gt;(left, right);
    /// Console.WriteLine(combined); // Outputs: Hello World
    /// </code>
    /// </example>
    /// <remarks>
    /// This constructor is equivalent to using the + operator: left + right.
    /// The resulting tree maintains all dimensions and summaries from both input trees.
    /// </remarks>
    public SumTree(SumTree<T> left, SumTree<T> right)
    {
        var result = Concat(left, right);
        this.data = result.data;
        this.Length = result.Length;
        this.Depth = result.Depth;
        this.BufferCount = result.BufferCount;
        this.IsBalanced = result.IsBalanced;
        this.dimensions = result.dimensions;
        this.summaries = result.summaries;
    }

    /// <summary>
    /// Gets the number of buffers (leaf nodes) in this tree.
    /// </summary>
    public int BufferCount { get; init; }

    /// <summary>
    /// Gets the left child if this is a node.
    /// </summary>
    public SumTree<T> Left => IsNode ? ((ValueTuple<SumTree<T>, SumTree<T>>)data).Item1 : Empty;

    /// <summary>
    /// Gets the right child if this is a node.
    /// </summary>
    public SumTree<T> Right => IsNode ? ((ValueTuple<SumTree<T>, SumTree<T>>)data).Item2 : Empty;

    /// <summary>
    /// Gets whether this is a node (has children) rather than a leaf.
    /// </summary>
    public bool IsNode => data is ValueTuple<SumTree<T>, SumTree<T>>;

    /// <summary>
    /// Gets the weight (length of left subtree) for balancing.
    /// </summary>
    public long Weight => IsNode ? Left.Length : 0;

    /// <summary>
    /// Gets the total length of elements in this tree.
    /// </summary>
    public long Length { get; init; }

    /// <summary>
    /// Gets whether this tree is empty.
    /// </summary>
    public bool IsEmpty => Length == 0;

    /// <summary>
    /// Gets the depth of this tree.
    /// </summary>
    public int Depth { get; init; }

    /// <summary>
    /// Gets whether this tree is balanced.
    /// </summary>
    public bool IsBalanced { get; init; }

    /// <summary>
    /// Gets the number of registered summary dimensions.
    /// </summary>
    public int DimensionCount => dimensions?.Count ?? 0;

    /// <summary>
    /// Gets the number of elements (same as Length but as int for IReadOnlyList).
    /// </summary>
    public int Count => (int)Math.Min(Length, int.MaxValue);

    /// <summary>
    /// Gets the dimensions dictionary (for internal use by extensions).
    /// </summary>
    internal Dictionary<Type, object>? GetDimensions() => dimensions;

    /// <summary>
    /// Gets the element at the specified index.
    /// </summary>
    public T ElementAt(long index) => this[index];

    /// <summary>
    /// Gets the element at the specified index.
    /// </summary>
    public T this[long index]
    {
        get
        {
            if (index < 0 || index >= Length)
                throw new ArgumentOutOfRangeException(nameof(index));

            return GetElementAtIndex(index);
        }
    }

    /// <summary>
    /// Gets the element at the specified index (IReadOnlyList implementation).
    /// </summary>
    public T this[int index] => this[(long)index];

    /// <summary>
    /// Adds a summary dimension to this SumTree.
    /// </summary>
    public SumTree<T> WithDimension<TSummary>(ISummaryDimension<T, TSummary> dimension)
        where TSummary : IEquatable<TSummary>
    {
        var newDimensions = new Dictionary<Type, object>(dimensions ?? new Dictionary<Type, object>());
        newDimensions[typeof(TSummary)] = dimension;

        var newSummaries = new Dictionary<Type, object>(summaries ?? new Dictionary<Type, object>());
        newSummaries[typeof(TSummary)] = ComputeSummaryForTree(dimension);

        return new SumTree<T>(data, Length, Depth, BufferCount, IsBalanced, newDimensions, newSummaries);
    }

    /// <summary>
    /// Gets the summary for a specific dimension type.
    /// </summary>
    public TSummary GetSummary<TSummary>() where TSummary : IEquatable<TSummary>
    {
        if (summaries == null || !summaries.TryGetValue(typeof(TSummary), out var summary))
            throw new InvalidOperationException(
                $"Dimension for summary type {typeof(TSummary).Name} is not registered.");

        return (TSummary)summary;
    }

    /// <summary>
    /// Tries to get the summary for a specific dimension type.
    /// </summary>
    public bool TryGetSummary<TSummary>(out TSummary summary) where TSummary : IEquatable<TSummary>
    {
        summary = default!;
        if (summaries == null || !summaries.TryGetValue(typeof(TSummary), out var summaryObj))
            return false;

        summary = (TSummary)summaryObj;
        return true;
    }

    /// <summary>
    /// Checks if a dimension is registered for the specified summary type.
    /// </summary>
    public bool HasDimension<TSummary>() where TSummary : IEquatable<TSummary>
    {
        return dimensions != null && dimensions.ContainsKey(typeof(TSummary));
    }

    /// <summary>
    /// Splits the tree at the specified index.
    /// </summary>
    public (SumTree<T> Left, SumTree<T> Right) SplitAt(long index)
    {
        if (index <= 0)
            return (Empty.WithDimensions(dimensions), this);
        if (index >= Length)
            return (this, Empty.WithDimensions(dimensions));

        return SplitAtInternal(index);
    }

    /// <summary>
    /// Inserts an element at the specified index.
    /// </summary>
    public SumTree<T> Insert(long index, T item)
    {
        var (left, right) = SplitAt(index);
        var middle = new SumTree<T>(item).WithDimensions(dimensions);
        return Concat(Concat(left, middle), right);
    }

    /// <summary>
    /// Inserts a range of elements at the specified index.
    /// </summary>
    public SumTree<T> InsertRange(long index, ReadOnlyMemory<T> items)
    {
        if (items.IsEmpty)
            return this;

        var (left, right) = SplitAt(index);
        var middle = new SumTree<T>(items, dimensions);
        return Concat(Concat(left, middle), right);
    }

    /// <summary>
    /// Inserts another SumTree at the specified index.
    /// </summary>
    public SumTree<T> InsertRange(long index, SumTree<T> other)
    {
        if (other.IsEmpty)
            return this;

        var (left, right) = SplitAt(index);
        var middle = other.WithDimensions(dimensions);
        return Concat(Concat(left, middle), right);
    }

    /// <summary>
    /// Removes a range of elements.
    /// </summary>
    public SumTree<T> RemoveRange(long start, long length)
    {
        if (length <= 0 || start >= Length)
            return this;

        var end = Math.Min(start + length, Length);
        var (left, temp) = SplitAt(start);
        var (_, right) = temp.SplitAt(end - start);
        return Concat(left, right);
    }

    /// <summary>
    /// Removes an element at the specified index.
    /// </summary>
    public SumTree<T> RemoveAt(long index)
    {
        return RemoveRange(index, 1);
    }

    /// <summary>
    /// Gets a slice of the tree.
    /// </summary>
    public SumTree<T> Slice(long start, long length)
    {
        if (length <= 0 || start >= Length)
            return Empty.WithDimensions(dimensions);

        var end = Math.Min(start + length, Length);
        var (_, temp) = SplitAt(start);
        var (result, _) = temp.SplitAt(end - start);
        return result;
    }

    /// <summary>
    /// Gets a slice from the starting index to the end.
    /// </summary>
    public SumTree<T> Slice(long start)
    {
        return Slice(start, Length - start);
    }

    /// <summary>
    /// Appends an element to the end.
    /// </summary>
    public SumTree<T> Add(T item)
    {
        return Concat(this, new SumTree<T>(item).WithDimensions(dimensions));
    }

    /// <summary>
    /// Appends another SumTree to the end.
    /// </summary>
    public SumTree<T> AddRange(SumTree<T> other)
    {
        return Concat(this, other.WithDimensions(dimensions));
    }

    /// <summary>
    /// Finds the position where a condition based on a summary becomes true.
    /// </summary>
    public long FindPosition<TSummary>(Func<TSummary, bool> predicate) where TSummary : IEquatable<TSummary>
    {
        if (dimensions == null || !dimensions.TryGetValue(typeof(TSummary), out var dimensionObj))
            throw new InvalidOperationException(
                $"Dimension for summary type {typeof(TSummary).Name} is not registered.");

        var dimension = (ISummaryDimension<T, TSummary>)dimensionObj;
        return FindPositionRecursive(dimension, predicate, dimension.Identity, 0);
    }

    /// <summary>
    /// Concatenates two trees.
    /// </summary>
    public static SumTree<T> operator +(SumTree<T> left, SumTree<T> right)
    {
        return Concat(left, right);
    }

    /// <summary>
    /// Gets an enumerator for the elements.
    /// </summary>
    public IEnumerator<T> GetEnumerator()
    {
        return EnumerateElements().GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>
    /// Determines whether the specified SumTree is equal to the current instance.
    /// </summary>
    public bool Equals(SumTree<T> other)
    {
        if (Length != other.Length) return false;
        if (IsEmpty && other.IsEmpty) return true;

        // Compare element by element
        using var enum1 = GetEnumerator();
        using var enum2 = other.GetEnumerator();

        while (enum1.MoveNext() && enum2.MoveNext())
        {
            if (!enum1.Current.Equals(enum2.Current))
                return false;
        }

        return true;
    }

    /// <summary>
    /// Determines whether the specified object is equal to the current instance.
    /// </summary>
    public override bool Equals(object? obj)
    {
        return obj is SumTree<T> other && Equals(other);
    }

    /// <summary>
    /// Returns a hash code for the current instance.
    /// </summary>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Length);

        // Add a few elements from the tree to the hash for performance
        var count = 0;
        foreach (var element in this)
        {
            hash.Add(element);
            if (++count >= 8) break; // Limit to first 8 elements for performance
        }

        return hash.ToHashCode();
    }

    /// <summary>
    /// Returns a string representation of the SumTree.
    /// </summary>
    public override string ToString()
    {
        // For SumTree<char>, return the actual string content
        if (typeof(T) == typeof(char))
        {
            var array = new T[Length];
            var index = 0;
            foreach (var item in this)
            {
                array[index++] = item;
            }
            return new string((char[])(object)array);
        }

        return $"SumTree<{typeof(T).Name}>[Length={Length}, Depth={Depth}, BufferCount={BufferCount}]";
    }

    public static bool operator ==(SumTree<T> left, SumTree<T> right) => left.Equals(right);
    public static bool operator !=(SumTree<T> left, SumTree<T> right) => !left.Equals(right);

    // Private implementation methods

    private T GetElementAtIndex(long index)
    {
        var current = this;
        var currentIndex = index;

        while (current.IsNode)
        {
            var leftLength = current.Left.Length;
            if (currentIndex < leftLength)
            {
                current = current.Left;
            }
            else
            {
                current = current.Right;
                currentIndex -= leftLength;
            }
        }

        // Leaf node
        return current.data switch
        {
            ReadOnlyMemory<T> memory => memory.Span[(int)currentIndex],
            ValueTuple<T> single => single.Item1,
            _ => throw new InvalidOperationException("Invalid data type")
        };
    }

    private (SumTree<T> Left, SumTree<T> Right) SplitAtInternal(long index)
    {
        var current = this;
        var currentIndex = index;
        var leftParts = new List<SumTree<T>>();
        var rightParts = new List<SumTree<T>>();

        while (current.IsNode)
        {
            var leftLength = current.Left.Length;
            if (currentIndex <= leftLength)
            {
                // Split will happen in left subtree
                rightParts.Add(current.Right);
                current = current.Left;
            }
            else
            {
                // Split will happen in right subtree
                leftParts.Add(current.Left);
                current = current.Right;
                currentIndex -= leftLength;
            }
        }

        // Now we have a leaf node, split it
        var (leafLeft, leafRight) = current.data switch
        {
            ReadOnlyMemory<T> memory => SplitMemory(memory, (int)currentIndex),
            ValueTuple<T> single => currentIndex == 0
                ? (Empty.WithDimensions(dimensions), current)
                : (current, Empty.WithDimensions(dimensions)),
            _ => throw new InvalidOperationException("Invalid data type")
        };

        // Combine left parts with leaf left
        var left = leafLeft;
        for (int i = leftParts.Count - 1; i >= 0; i--)
        {
            left = ConcatDirect(leftParts[i], left);
        }

        // Combine leaf right with right parts
        var right = leafRight;
        for (int i = rightParts.Count - 1; i >= 0; i--)
        {
            right = ConcatDirect(right, rightParts[i]);
        }

        return (left, right);
    }

    private (SumTree<T> Left, SumTree<T> Right) SplitMemory(ReadOnlyMemory<T> memory, int index)
    {
        if (index <= 0)
            return (Empty.WithDimensions(dimensions), this);
        if (index >= memory.Length)
            return (this, Empty.WithDimensions(dimensions));

        var leftMemory = memory.Slice(0, index);
        var rightMemory = memory.Slice(index);

        return (new SumTree<T>(leftMemory, dimensions), new SumTree<T>(rightMemory, dimensions));
    }

    private static SumTree<T> Concat(SumTree<T> left, SumTree<T> right)
    {
        if (left.IsEmpty) return right;
        if (right.IsEmpty) return left;

        var newLength = left.Length + right.Length;
        var newDepth = Math.Max(left.Depth, right.Depth) + 1;
        var newBufferCount = left.BufferCount + right.BufferCount;
        var isBalanced = CalculateIsBalanced(newLength, newDepth);

        // Merge dimensions
        var dimensions = MergeDimensions(left.dimensions, right.dimensions);
        var summaries = dimensions != null ? ComputeCombinedSummaries(left, right, dimensions) : null;

        var result = new SumTree<T>(
            new ValueTuple<SumTree<T>, SumTree<T>>(left, right),
            newLength,
            newDepth,
            newBufferCount,
            isBalanced,
            dimensions,
            summaries
        );

        return result;
    }

    private static SumTree<T> ConcatDirect(SumTree<T> left, SumTree<T> right)
    {
        if (left.IsEmpty) return right;
        if (right.IsEmpty) return left;

        var newLength = left.Length + right.Length;
        var newDepth = Math.Max(left.Depth, right.Depth) + 1;
        var newBufferCount = left.BufferCount + right.BufferCount;
        var isBalanced = CalculateIsBalanced(newLength, newDepth);

        // Merge dimensions
        var dimensions = MergeDimensions(left.dimensions, right.dimensions);
        var summaries = dimensions != null ? ComputeCombinedSummaries(left, right, dimensions) : null;

        return new SumTree<T>(
            new ValueTuple<SumTree<T>, SumTree<T>>(left, right),
            newLength,
            newDepth,
            newBufferCount,
            isBalanced,
            dimensions,
            summaries
        );
    }

    private SumTree<T> WithDimensions(Dictionary<Type, object>? newDimensions)
    {
        if (newDimensions == null || newDimensions.Count == 0)
            return this;

        if (dimensions != null && DimensionsEqual(dimensions, newDimensions))
            return this;

        var newSummaries = ComputeAllSummaries(newDimensions);
        return new SumTree<T>(data, Length, Depth, BufferCount, IsBalanced, newDimensions, newSummaries);
    }

    private SumTree<T> Balanced()
    {
        // Completely disable balancing to prevent infinite recursion
        return this;
    }

    private void CollectLeaves(List<SumTree<T>> leaves)
    {
        if (IsNode)
        {
            Left.CollectLeaves(leaves);
            Right.CollectLeaves(leaves);
        }
        else if (!IsEmpty)
        {
            leaves.Add(this);
        }
    }

    private IEnumerable<T> EnumerateElements()
    {
        var stack = new Stack<SumTree<T>>();
        stack.Push(this);

        while (stack.Count > 0)
        {
            var current = stack.Pop();

            if (current.IsNode)
            {
                // Push right first, then left (so left is processed first)
                stack.Push(current.Right);
                stack.Push(current.Left);
            }
            else
            {
                switch (current.data)
                {
                    case ReadOnlyMemory<T> memory:
                        for (int i = 0; i < memory.Length; i++)
                            yield return memory.Span[i];
                        break;
                    case ValueTuple<T> single:
                        yield return single.Item1;
                        break;
                }
            }
        }
    }

    private long FindPositionRecursive<TSummary>(
        ISummaryDimension<T, TSummary> dimension,
        Func<TSummary, bool> predicate,
        TSummary currentSummary,
        long currentIndex) where TSummary : IEquatable<TSummary>
    {
        if (IsEmpty) return -1;

        if (IsNode)
        {
            // Try left subtree first
            var leftResult = Left.FindPositionRecursive(dimension, predicate, currentSummary, currentIndex);
            if (leftResult >= 0) return leftResult;

            // If not found in left, try right with accumulated summary
            var leftSummary = Left.GetSummaryForDimension(dimension);
            var combinedSummary = dimension.Combine(currentSummary, leftSummary);
            return Right.FindPositionRecursive(dimension, predicate, combinedSummary, currentIndex + Left.Length);
        }

        // Leaf node - check each element
        var summary = currentSummary;
        var index = 0L;

        foreach (var element in EnumerateElements())
        {
            var elementSummary = dimension.SummarizeElement(element);
            var newSummary = dimension.Combine(summary, elementSummary);

            if (predicate(newSummary))
                return currentIndex + index;

            summary = newSummary;
            index++;
        }

        return -1;
    }

    /// <summary>
    /// Gets the summary for the specified dimension.
    /// </summary>
    /// <typeparam name="TSummary">The type of the summary.</typeparam>
    /// <param name="dimension">The dimension to compute the summary for.</param>
    /// <returns>The summary for the dimension.</returns>
    public TSummary GetSummary<TSummary>(ISummaryDimension<T, TSummary> dimension)
        where TSummary : IEquatable<TSummary>
    {
        if (summaries != null && summaries.TryGetValue(typeof(TSummary), out var summary))
            return (TSummary)summary;

        return ComputeSummaryForTree(dimension);
    }

    private TSummary GetSummaryForDimension<TSummary>(ISummaryDimension<T, TSummary> dimension)
        where TSummary : IEquatable<TSummary>
    {
        return GetSummary(dimension);
    }

    private TSummary ComputeSummaryForTree<TSummary>(ISummaryDimension<T, TSummary> dimension)
        where TSummary : IEquatable<TSummary>
    {
        if (IsEmpty) return dimension.Identity;

        if (IsNode)
        {
            var leftSummary = Left.ComputeSummaryForTree(dimension);
            var rightSummary = Right.ComputeSummaryForTree(dimension);
            return dimension.Combine(leftSummary, rightSummary);
        }

        // Leaf node
        return data switch
        {
            ReadOnlyMemory<T> memory => dimension.SummarizeSpan(memory.Span),
            ValueTuple<T> single => dimension.SummarizeElement(single.Item1),
            _ => dimension.Identity
        };
    }

    // Static helper methods

    private static Dictionary<Type, object>? MergeDimensions(
        Dictionary<Type, object>? left,
        Dictionary<Type, object>? right)
    {
        if (left == null) return right;
        if (right == null) return left;

        var result = new Dictionary<Type, object>(left);
        foreach (var kvp in right)
        {
            result[kvp.Key] = kvp.Value;
        }

        return result;
    }

    private static Dictionary<Type, object>? ComputeCombinedSummaries(
        SumTree<T> left,
        SumTree<T> right,
        Dictionary<Type, object> dimensions)
    {
        var result = new Dictionary<Type, object>();

        foreach (var kvp in dimensions)
        {
            var summaryType = kvp.Key;
            var dimension = kvp.Value;

            var leftSummary = left.summaries?.GetValueOrDefault(summaryType);
            var rightSummary = right.summaries?.GetValueOrDefault(summaryType);

            if (leftSummary != null && rightSummary != null)
            {
                // Combine existing summaries
                var combineMethod = dimension.GetType().GetMethod("Combine");
                result[summaryType] = combineMethod!.Invoke(dimension, [leftSummary, rightSummary])!;
            }
            else
            {
                // Recompute summary
                result[summaryType] = ComputeSummaryForDimensionStatic(left, right, dimension);
            }
        }

        return result;
    }

    private static object ComputeSummaryForDimensionStatic(SumTree<T> left, SumTree<T> right, object dimension)
    {
        var dimensionType = dimension.GetType();
        var interfaces = dimensionType.GetInterfaces();
        var summaryInterface = interfaces.FirstOrDefault(i =>
            i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ISummaryDimension<,>));

        if (summaryInterface == null)
            throw new ArgumentException($"Invalid dimension type: {dimensionType}");

        var summaryType = summaryInterface.GetGenericArguments()[1];
        var combineMethod = dimensionType.GetMethod("Combine");
        var identityProperty = dimensionType.GetProperty("Identity");

        var leftSummary = left.summaries?.GetValueOrDefault(summaryType) ?? identityProperty!.GetValue(dimension);
        var rightSummary = right.summaries?.GetValueOrDefault(summaryType) ?? identityProperty!.GetValue(dimension);

        return combineMethod!.Invoke(dimension, [leftSummary, rightSummary])!;
    }

    private Dictionary<Type, object> ComputeAllSummaries(Dictionary<Type, object> dimensions)
    {
        var result = new Dictionary<Type, object>();

        foreach (var kvp in dimensions)
        {
            var summaryType = kvp.Key;
            var dimension = kvp.Value;
            result[summaryType] = ComputeSummaryForDimensionInstance(dimension);
        }

        return result;
    }

    private object ComputeSummaryForDimensionInstance(object dimension)
    {
        var dimensionType = dimension.GetType();
        var interfaces = dimensionType.GetInterfaces();
        var summaryInterface = interfaces.FirstOrDefault(i =>
            i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ISummaryDimension<,>));

        if (summaryInterface == null)
            throw new ArgumentException($"Invalid dimension type: {dimensionType}");

        var method = typeof(SumTree<T>).GetMethod(nameof(ComputeSummaryForTree),
            BindingFlags.NonPublic | BindingFlags.Instance);
        var summaryType = summaryInterface.GetGenericArguments()[1];
        var genericMethod = method!.MakeGenericMethod(summaryType);

        return genericMethod.Invoke(this, [dimension])!;
    }

    private static Dictionary<Type, object> ComputeEmptySummaries(Dictionary<Type, object> dimensions)
    {
        var result = new Dictionary<Type, object>();

        foreach (var kvp in dimensions)
        {
            var dimension = kvp.Value;
            var identityProperty = dimension.GetType().GetProperty("Identity");
            result[kvp.Key] = identityProperty!.GetValue(dimension)!;
        }

        return result;
    }

    private static Dictionary<Type, object> ComputeSummariesForMemory(ReadOnlyMemory<T> memory,
        Dictionary<Type, object> dimensions)
    {
        var result = new Dictionary<Type, object>();

        foreach (var kvp in dimensions)
        {
            var summaryType = kvp.Key;
            var dimension = kvp.Value;

            // Use reflection to call the generic SummarizeSpan method
            var dimensionType = dimension.GetType();
            var interfaces = dimensionType.GetInterfaces();
            var summaryInterface = interfaces.FirstOrDefault(i =>
                i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ISummaryDimension<,>));

            if (summaryInterface != null)
            {
                var method = typeof(SumTree<T>).GetMethod(nameof(ComputeSummaryForMemoryGeneric),
                    BindingFlags.NonPublic | BindingFlags.Static);
                var genericMethod = method!.MakeGenericMethod(summaryInterface.GetGenericArguments()[1]);
                result[summaryType] = genericMethod.Invoke(null, [memory, dimension])!;
            }
        }

        return result;
    }

    private static TSummary ComputeSummaryForMemoryGeneric<TSummary>(ReadOnlyMemory<T> memory,
        ISummaryDimension<T, TSummary> dimension)
        where TSummary : IEquatable<TSummary>
    {
        return dimension.SummarizeSpan(memory.Span);
    }

    private static bool DimensionsEqual(Dictionary<Type, object>? left, Dictionary<Type, object>? right)
    {
        if (left == null && right == null) return true;
        if (left == null || right == null) return false;
        if (left.Count != right.Count) return false;

        foreach (var kvp in left)
        {
            if (!right.TryGetValue(kvp.Key, out var rightValue) || !kvp.Value.Equals(rightValue))
                return false;
        }

        return true;
    }

    private static bool CalculateIsBalanced(long length, int depth)
    {
        if (depth >= DepthToFibonnaciPlusTwo.Length) return false;
        return length >= DepthToFibonnaciPlusTwo[depth];
    }

    private static int Fibonnaci(int n)
    {
        if (n <= 1) return n;
        int a = 0, b = 1;
        for (int i = 2; i <= n; i++)
        {
            int temp = a + b;
            a = b;
            b = temp;
        }

        return b;
    }

    private static SumTree<T> CombineLeaves(List<SumTree<T>> leaves, Dictionary<Type, object>? dimensions)
    {
        if (leaves.Count == 0) return Empty;
        if (leaves.Count == 1) return leaves[0];

        // Simple left-to-right combining without balancing to prevent recursion
        var result = leaves[0];
        for (int i = 1; i < leaves.Count; i++)
        {
            result = ConcatDirect(result, leaves[i]);
        }

        return result;
    }

    /// <summary>
    /// Maximum number of bytes before the GC puts buffers on the large object heap.
    /// </summary>
    private const int LargeObjectHeapBytes = 85_000 - 24;

    /// <summary>
    /// Calculates a CPU-cache aligned buffer size for the given input type.
    /// </summary>
    private static int CalculateAlignedBufferLength<TElement>(int cacheLineSize = 64)
    {
        var elementSize = Unsafe.SizeOf<TElement>();
        var numberOfElements = LargeObjectHeapBytes / elementSize;

        var bufferSize = numberOfElements * elementSize;
        var padding = cacheLineSize - (bufferSize % cacheLineSize);

        if (padding == cacheLineSize)
            padding = 0;

        var alignedBufferSize = bufferSize + padding;
        return alignedBufferSize / elementSize;
    }

    /// <summary>
    /// Math.Pow for integers.
    /// </summary>
    private static int IntPow(int x, uint pow)
    {
        int ret = 1;
        for (var p = 0; p < pow; p++)
        {
            ret *= x;
        }

        return ret;
    }
}
