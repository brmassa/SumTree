namespace SumTree.TreeMap;

/// <summary>
/// An optional value: either a present <see cref="Value"/> or nothing.
/// Used as the common result type for lookups across TreeMap and TreeSet.
/// </summary>
/// <typeparam name="T">The type of the optional value.</typeparam>
public readonly struct Maybe<T>
{
    /// <summary>
    /// Gets whether a value is present.
    /// </summary>
    public bool HasValue { get; }

    /// <summary>
    /// Gets the present value, or the default when <see cref="HasValue"/> is false.
    /// </summary>
    public T Value { get; }

    /// <summary>
    /// Initializes a present Maybe wrapping the given value.
    /// </summary>
    /// <param name="value">The wrapped value.</param>
    public Maybe(T value)
    {
        HasValue = true;
        Value = value;
    }

    /// <summary>
    /// Gets an empty Maybe.
    /// </summary>
    public static Maybe<T> None => new();
}
