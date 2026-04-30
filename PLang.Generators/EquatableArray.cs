using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace PLang.Generators;

/// <summary>
/// Value-equal wrapper around T[] for use in IIncrementalGenerator pipeline stages.
/// IIncrementalGenerator caches by structural equality; List&lt;T&gt; uses reference
/// equality, so two lists with identical contents are seen as different and the cache
/// always misses. EquatableArray's Equals/GetHashCode walk the elements, so the cache
/// hits when contents match.
/// </summary>
public readonly struct EquatableArray<T> : IEquatable<EquatableArray<T>>, IEnumerable<T>
    where T : IEquatable<T>
{
    public static readonly EquatableArray<T> Empty = new(Array.Empty<T>());

    private readonly T[]? _array;

    public EquatableArray(T[] array) => _array = array;

    public EquatableArray(IEnumerable<T> source) => _array = source.ToArray();

    public ReadOnlySpan<T> AsSpan() => new(_array);

    public T[] AsArray() => _array ?? Array.Empty<T>();

    public int Count => _array?.Length ?? 0;

    public T this[int index] => _array![index];

    public bool Equals(EquatableArray<T> other)
    {
        if (_array is null) return other._array is null;
        if (other._array is null) return false;
        return _array.AsSpan().SequenceEqual(other._array.AsSpan());
    }

    public override bool Equals(object? obj) => obj is EquatableArray<T> other && Equals(other);

    public override int GetHashCode()
    {
        if (_array is null) return 0;
        var hash = 17;
        unchecked
        {
            foreach (var item in _array)
            {
                hash = hash * 31 + (item?.GetHashCode() ?? 0);
            }
        }
        return hash;
    }

    public IEnumerator<T> GetEnumerator() => ((IEnumerable<T>)(_array ?? Array.Empty<T>())).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public static bool operator ==(EquatableArray<T> left, EquatableArray<T> right) => left.Equals(right);
    public static bool operator !=(EquatableArray<T> left, EquatableArray<T> right) => !left.Equals(right);
}

public static class EquatableArrayExtensions
{
    public static EquatableArray<T> ToEquatableArray<T>(this IEnumerable<T> source) where T : IEquatable<T>
        => new(source);
}
