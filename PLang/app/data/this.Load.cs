using System.Collections;

namespace app.data;

/// <summary>
/// Async pre-materialization pass for lazy reference fundamentals
/// (<see cref="ILoadable"/>). Run once at the serialize chokepoint — above the
/// serializer's STJ converter wall (<c>Wire.Write</c> / <c>JsonConverter&lt;T&gt;.Write</c>
/// is sync by the System.Text.Json contract, so nothing below it can <c>await</c>).
/// By the time the sync renderers read <c>image.Bytes</c>, the content is in
/// memory.
///
/// <para>Walks the same value shapes <see cref="NormalizeValue(object?, View, HashSet{object}, int)"/>
/// walks (nested Data, dictionaries, lists), awaiting <see cref="ILoadable.LoadAsync"/>
/// on each reference fundamental it finds. Idempotent and cheap when nothing is
/// lazy: an already-loaded image returns from its load seam immediately, and a
/// graph with no <see cref="ILoadable"/> is a pure walk.</para>
/// </summary>
public partial class @this
{
    /// <summary>
    /// Materialize every lazy reference fundamental reachable from
    /// <see cref="Value"/>. Returns <c>null</c> on success, or an error
    /// <see cref="@this"/> (key <c>StrictKindMismatch</c>) when a strict
    /// reference fundamental's loaded content does not match its declared kind —
    /// surfaced here, before any bytes reach the stream, so the caller gets a
    /// clean failure instead of a torn write. Read failures (missing file,
    /// denied path) propagate as exceptions to the serializer's own catch.
    /// </summary>
    public async System.Threading.Tasks.Task<@this?> Load()
    {
        var visited = new HashSet<object>(System.Collections.Generic.ReferenceEqualityComparer.Instance);
        try
        {
            await LoadValue(Value, visited, depth: 0);
            return null;
        }
        catch (StrictKindMismatchException ex)
        {
            return FromError(new global::app.error.Error(ex.Message, "StrictKindMismatch", 400) { Exception = ex });
        }
    }

    private static async System.Threading.Tasks.Task LoadValue(object? value, HashSet<object> visited, int depth)
    {
        if (depth > MaxNormalizeDepth)
            throw new NormalizeException(
                $"Load depth exceeded cap ({MaxNormalizeDepth}). Likely an unbounded structure.",
                "LoadMaxDepthExceeded");

        // Tree-native leaves carry no lazy content.
        if (value is null || value is string || value is byte[]
            || value is System.ValueType)
            return;

        // Reference fundamental — pull its content into memory (and run the
        // strict check, which throws here on mismatch).
        if (value is ILoadable loadable)
        {
            await loadable.LoadAsync();
            return;
        }

        // Nested Data: recurse its Value.
        if (value is @this nested)
        {
            if (!visited.Add(nested)) return;
            try { await LoadValue(nested.Value, visited, depth + 1); }
            finally { visited.Remove(nested); }
            return;
        }

        // Dictionaries: recurse each value.
        if (value is IDictionary dict)
        {
            if (!visited.Add(value)) return;
            try
            {
                foreach (DictionaryEntry e in dict)
                    await LoadValue(e.Value, visited, depth + 1);
            }
            finally { visited.Remove(value); }
            return;
        }

        // Lists / arrays: recurse each item.
        if (value is IEnumerable enumerable)
        {
            if (!visited.Add(value)) return;
            try
            {
                foreach (var item in enumerable)
                    await LoadValue(item, visited, depth + 1);
            }
            finally { visited.Remove(value); }
        }

        // Any other domain object carries no lazy content in the PLang value
        // model (reference fundamentals arrive as the value itself or inside a
        // dict/list) — no reflection walk.
    }
}
