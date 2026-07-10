namespace PLang.Tests.App.Fixtures;

/// <summary>
/// Sign adapters over the unified comparison for wrapper-level tests: the old
/// per-type Order/AreEqual members are gone; these route raw wrapper values
/// through Data.CompareValues (the sync core) and map to the legacy sign/bool
/// shapes the assertions read.
/// </summary>
public static class CompareTestOps
{
    public static int Ord(object a, object b)
    {
        var da = new Data("", a, context: global::PLang.Tests.TestApp.SharedContext);
        var db = new Data("", b, context: global::PLang.Tests.TestApp.SharedContext);
        return Map(da.CompareValues(db, a, b));
    }

    /// <summary>Data-level order with the sort policy: nulls last.</summary>
    public static int OrdD(Data a, Data b)
    {
        var va = a.Peek() is global::app.type.item.@null.@this ? null : a.Peek();
        var vb = b.Peek() is global::app.type.item.@null.@this ? null : b.Peek();
        if (va == null && vb == null) return 0;
        if (va == null) return 1;
        if (vb == null) return -1;
        return Map(a.CompareValues(b, va, vb));
    }

    public static bool Eq(object? a, object? b)
    {
        if (a == null || b == null) return a == null && b == null;
        return new Data("", a, context: global::PLang.Tests.TestApp.SharedContext).CompareValues(new Data("", b, context: global::PLang.Tests.TestApp.SharedContext), a, b) == global::app.data.Comparison.Equal;
    }

    private static int Map(global::app.data.Comparison c) => c switch
    {
        global::app.data.Comparison.Less => -1,
        global::app.data.Comparison.Greater => 1,
        global::app.data.Comparison.Equal => 0,
        _ => throw new global::app.data.IncomparableException("values have no order"),
    };
}
