namespace PLang.Tests.App.DataTests;

public class DataDiffTests : System.IAsyncDisposable
{
    private readonly global::app.@this _app = global::PLang.Tests.TestApp.Create("/tmp/DataDiffTests-" + System.Guid.NewGuid().ToString("N")[..6]);
    public async System.Threading.Tasks.ValueTask DisposeAsync() => await _app.DisposeAsync();

    [Test]
    public async Task Compare_IdenticalStrings_MatchTrue()
    {
        var a = _app.Data("a", "hello");
        var b = _app.Data("b", "hello");

        var result = await a.Diff(b);
        var diff = global::app.type.item.@this.Lower<Dictionary<string, object?>>(await result.Value());

        await Assert.That(diff).IsNotNull();
        await Assert.That(diff!["match"]).IsEqualTo(true);
    }

    [Test]
    public async Task Compare_DifferentStrings_MatchFalse()
    {
        var a = _app.Data("a", "hello");
        var b = _app.Data("b", "world");

        var result = await a.Diff(b);
        var diff = global::app.type.item.@this.Lower<Dictionary<string, object?>>(await result.Value());

        await Assert.That(diff).IsNotNull();
        await Assert.That(diff!["match"]).IsEqualTo(false);
        await Assert.That(diff["expected"]).IsEqualTo("hello");
        await Assert.That(diff["actual"]).IsEqualTo("world");
    }

    [Test]
    public async Task Compare_IdenticalNumbers_MatchTrue()
    {
        var a = _app.Data("a", 42);
        var b = _app.Data("b", 42);

        var result = await a.Diff(b);
        var diff = global::app.type.item.@this.Lower<Dictionary<string, object?>>(await result.Value());

        await Assert.That(diff).IsNotNull();
        await Assert.That(diff!["match"]).IsEqualTo(true);
    }

    [Test]
    public async Task Compare_IntVsLong_MatchTrue()
    {
        // JSON numeric boxing: int 42 vs long 42 should match
        var a = _app.Data("a", (int)42);
        var b = _app.Data("b", (long)42);

        var result = await a.Diff(b);
        var diff = global::app.type.item.@this.Lower<Dictionary<string, object?>>(await result.Value());

        await Assert.That(diff).IsNotNull();
        await Assert.That(diff!["match"]).IsEqualTo(true);
    }

    [Test]
    public async Task Compare_IdenticalObjects_MatchTrue()
    {
        var obj = new Dictionary<string, object?>
        {
            ["module"] = "file",
            ["action"] = "read",
            ["parameters"] = new List<object?> { "path" }
        };

        var a = _app.Data("a", obj);
        var b = _app.Data("b", new Dictionary<string, object?>(obj));

        var result = await a.Diff(b);
        var diff = global::app.type.item.@this.Lower<Dictionary<string, object?>>(await result.Value());

        await Assert.That(diff).IsNotNull();
        await Assert.That(diff!["match"]).IsEqualTo(true);
    }

    [Test]
    public async Task Compare_DifferentObjects_ShowsFieldDiffs()
    {
        var expected = new Dictionary<string, object?> { ["module"] = "file", ["action"] = "read" };
        var actual = new Dictionary<string, object?> { ["module"] = "file", ["action"] = "save" };

        var a = _app.Data("a", expected);
        var b = _app.Data("b", actual);

        var result = await a.Diff(b);
        var diff = global::app.type.item.@this.Lower<Dictionary<string, object?>>(await result.Value());

        await Assert.That(diff).IsNotNull();
        await Assert.That(diff!["match"]).IsEqualTo(false);

        var fields = diff["fields"] as Dictionary<string, object?>;
        await Assert.That(fields).IsNotNull();

        var moduleDiff = fields!["module"] as Dictionary<string, object?>;
        await Assert.That(moduleDiff!["match"]).IsEqualTo(true);

        var actionDiff = fields["action"] as Dictionary<string, object?>;
        await Assert.That(actionDiff!["match"]).IsEqualTo(false);
        await Assert.That(actionDiff["expected"]).IsEqualTo("read");
        await Assert.That(actionDiff["actual"]).IsEqualTo("save");
    }

    [Test]
    public async Task Compare_MissingField_ReportsMissing()
    {
        var expected = new Dictionary<string, object?> { ["module"] = "file", ["action"] = "read" };
        var actual = new Dictionary<string, object?> { ["module"] = "file" };

        var a = _app.Data("a", expected);
        var b = _app.Data("b", actual);

        var result = await a.Diff(b);
        var diff = global::app.type.item.@this.Lower<Dictionary<string, object?>>(await result.Value());

        await Assert.That(diff).IsNotNull();
        await Assert.That(diff!["match"]).IsEqualTo(false);

        // The field list round-trips as a native list, so read it as a sequence
        // rather than casting to the CLR List<string> it was built from.
        var missing = (diff["missingFields"] as System.Collections.IEnumerable)?
            .Cast<object?>().Select(x => x?.ToString()).ToList();
        await Assert.That(missing).IsNotNull();
        await Assert.That(missing!).Contains("action");
    }

    [Test]
    public async Task Compare_ExtraField_ReportsExtra()
    {
        var expected = new Dictionary<string, object?> { ["module"] = "file" };
        var actual = new Dictionary<string, object?> { ["module"] = "file", ["extra"] = "value" };

        var a = _app.Data("a", expected);
        var b = _app.Data("b", actual);

        var result = await a.Diff(b);
        var diff = global::app.type.item.@this.Lower<Dictionary<string, object?>>(await result.Value());

        await Assert.That(diff).IsNotNull();
        await Assert.That(diff!["match"]).IsEqualTo(false);

        // The field list round-trips as a native list, so read it as a sequence
        // rather than casting to the CLR List<string> it was built from.
        var extra = (diff["extraFields"] as System.Collections.IEnumerable)?
            .Cast<object?>().Select(x => x?.ToString()).ToList();
        await Assert.That(extra).IsNotNull();
        await Assert.That(extra!).Contains("extra");
    }

    [Test]
    public async Task Compare_NullExpected_NullActual_MatchTrue()
    {
        var expected = new Dictionary<string, object?> { ["field"] = null };
        var actual = new Dictionary<string, object?> { ["field"] = null };

        var a = _app.Data("a", expected);
        var b = _app.Data("b", actual);

        var result = await a.Diff(b);
        var diff = global::app.type.item.@this.Lower<Dictionary<string, object?>>(await result.Value());

        await Assert.That(diff).IsNotNull();
        await Assert.That(diff!["match"]).IsEqualTo(true);
    }

    [Test]
    public async Task Compare_NullExpected_MissingActual_MatchTrue()
    {
        // null expected field == missing in actual (for optional .pr fields)
        var expected = new Dictionary<string, object?> { ["module"] = "file", ["onError"] = null };
        var actual = new Dictionary<string, object?> { ["module"] = "file" };

        var a = _app.Data("a", expected);
        var b = _app.Data("b", actual);

        var result = await a.Diff(b);
        var diff = global::app.type.item.@this.Lower<Dictionary<string, object?>>(await result.Value());

        await Assert.That(diff).IsNotNull();
        await Assert.That(diff!["match"]).IsEqualTo(true);
    }

    [Test]
    public async Task Compare_Arrays_DifferentLength_MatchFalse()
    {
        var expected = new List<object?> { "a", "b", "c" };
        var actual = new List<object?> { "a", "b" };

        var a = _app.Data("a", expected);
        var b = _app.Data("b", actual);

        var result = await a.Diff(b);
        var diff = global::app.type.item.@this.Lower<Dictionary<string, object?>>(await result.Value());

        await Assert.That(diff).IsNotNull();
        await Assert.That(diff!["match"]).IsEqualTo(false);
        // Numbers round-trip through the value model as the number type, so read
        // the count rather than asserting a boxed CLR int.
        await Assert.That(System.Convert.ToInt32(diff["expectedLength"])).IsEqualTo(3);
        await Assert.That(System.Convert.ToInt32(diff["actualLength"])).IsEqualTo(2);
    }

    [Test]
    public async Task Compare_NestedObjects_DeepDiff()
    {
        var expected = new Dictionary<string, object?>
        {
            ["step"] = new Dictionary<string, object?>
            {
                ["actions"] = new List<object?>
                {
                    new Dictionary<string, object?> { ["module"] = "file", ["action"] = "read" }
                }
            }
        };
        var actual = new Dictionary<string, object?>
        {
            ["step"] = new Dictionary<string, object?>
            {
                ["actions"] = new List<object?>
                {
                    new Dictionary<string, object?> { ["module"] = "file", ["action"] = "write" }
                }
            }
        };

        var a = _app.Data("a", expected);
        var b = _app.Data("b", actual);

        var result = await a.Diff(b);
        var diff = global::app.type.item.@this.Lower<Dictionary<string, object?>>(await result.Value());

        await Assert.That(diff).IsNotNull();
        await Assert.That(diff!["match"]).IsEqualTo(false);
    }

    [Test]
    public async Task Compare_BooleanValues_MatchTrue()
    {
        var a = _app.Data("a", true);
        var b = _app.Data("b", true);

        var result = await a.Diff(b);
        var diff = global::app.type.item.@this.Lower<Dictionary<string, object?>>(await result.Value());

        await Assert.That(diff).IsNotNull();
        await Assert.That(diff!["match"]).IsEqualTo(true);
    }

    [Test]
    public async Task Compare_ReturnIsData()
    {
        var a = _app.Data("a", "test");
        var b = _app.Data("b", "test");

        var result = await a.Diff(b);

        await Assert.That(result).IsNotNull();
        await Assert.That(result.Name).IsEqualTo("comparison");
        await Assert.That((await result.Value())).IsNotNull();
    }
}
