using app.data;

namespace PLang.Tests.App.DataTests;

/// <summary>
/// Test helper — Normalize emits an object / dict as the native <c>dict</c> value
/// type now (collections hold Data). These tests pin the filter behavior (which
/// properties travel, masking, lowercasing) and read the children as a flat
/// <c>List&lt;Data&gt;</c>; this projects the dict's entries to that shape so the
/// existing assertions keep working against the new container.
/// </summary>
internal static class NormalizeTestExtensions
{
    public static List<Data> Children(this object? normalized) => normalized switch
    {
        app.type.item.dict.@this d => d.Entries.ToList(),
        app.type.item.list.@this l => l.Items.ToList(),
        List<Data> list => list,
        null => new List<Data>(),
        _ => throw new System.InvalidOperationException(
            $"Normalize result is not an object shape: {normalized.GetType().FullName}"),
    };
}
