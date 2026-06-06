namespace PLang.Tests;

/// <summary>
/// Test ergonomics for the born-native collections cascade: a typed List/Dictionary
/// literal wraps into the Data&lt;list&gt;/Data&lt;dict&gt; a handler param now expects.
/// Context-free (null) — test literals carry no %var% references to resolve.
/// </summary>
public static class CollectionTestExtensions
{
    public static global::app.data.@this<global::app.type.list.@this> ToListData(this System.Collections.IEnumerable raw)
        => new("", global::app.type.list.@this.FromRaw(raw, null)!);

    public static global::app.data.@this<global::app.type.dict.@this> ToDictData(this System.Collections.IDictionary raw)
    {
        var d = new global::app.type.dict.@this();
        foreach (System.Collections.DictionaryEntry e in raw)
            d.Set(e.Key.ToString()!, e.Value);
        return new("", d);
    }
}
