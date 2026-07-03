namespace PLang.Tests;

/// <summary>
/// Test ergonomics for the born-native collections cascade: a typed List/Dictionary
/// literal wraps into the Data&lt;list&gt;/Data&lt;dict&gt; a handler param now expects.
/// Context-free (null) — test literals carry no %var% references to resolve.
/// </summary>
public static class CollectionTestExtensions
{
    public static global::app.data.@this<global::app.type.list.@this> ToListData(this System.Collections.IEnumerable raw, global::app.actor.context.@this? context = null)
    {
        context ??= global::PLang.Tests.TestApp.SharedContext;
        return new("", global::app.type.list.@this.FromRaw(raw, context)!, context: context);
    }

    public static global::app.data.@this<global::app.type.list.@this<T>> ToListData<T>(this System.Collections.IEnumerable raw, global::app.actor.context.@this? context = null)
        where T : global::app.type.item.@this
    {
        context ??= global::PLang.Tests.TestApp.SharedContext;
        var l = new global::app.type.list.@this<T>(context);
        foreach (var i in raw)
            l.Add(i is global::app.data.@this d ? d : new global::app.data.@this("", i, context: context));
        return new("", l, context: context);
    }

    public static global::app.data.@this<global::app.type.dict.@this> ToDictData(this System.Collections.IDictionary raw, global::app.actor.context.@this? context = null)
    {
        context ??= global::PLang.Tests.TestApp.SharedContext;
        // Born WITH context — dict.Context propagates to every entry, so lazy Slot
        // materialization borns its entry values with a wired scope.
        var d = new global::app.type.dict.@this(context);
        foreach (System.Collections.DictionaryEntry e in raw)
            d.Set(e.Key.ToString()!, e.Value);
        return new("", d, context: context);
    }
}
