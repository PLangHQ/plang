namespace PLang.Tests.App.DataTests;

public class DataCompareTests
{
    [Test]
    public async Task Compare_IdenticalStrings_MatchTrue()
    {
        var a = new Data("a", "hello");
        var b = new Data("b", "hello");

        var result = a.Compare(b);
        var diff = result.Value as Dictionary<string, object?>;

        await Assert.That(diff).IsNotNull();
        await Assert.That(diff!["match"]).IsEqualTo(true);
    }

    [Test]
    public async Task Compare_DifferentStrings_MatchFalse()
    {
        var a = new Data("a", "hello");
        var b = new Data("b", "world");

        var result = a.Compare(b);
        var diff = result.Value as Dictionary<string, object?>;

        await Assert.That(diff).IsNotNull();
        await Assert.That(diff!["match"]).IsEqualTo(false);
        await Assert.That(diff["expected"]).IsEqualTo("hello");
        await Assert.That(diff["actual"]).IsEqualTo("world");
    }

    [Test]
    public async Task Compare_IdenticalNumbers_MatchTrue()
    {
        var a = new Data("a", 42);
        var b = new Data("b", 42);

        var result = a.Compare(b);
        var diff = result.Value as Dictionary<string, object?>;

        await Assert.That(diff).IsNotNull();
        await Assert.That(diff!["match"]).IsEqualTo(true);
    }

    [Test]
    public async Task Compare_IntVsLong_MatchTrue()
    {
        // JSON numeric boxing: int 42 vs long 42 should match
        var a = new Data("a", (int)42);
        var b = new Data("b", (long)42);

        var result = a.Compare(b);
        var diff = result.Value as Dictionary<string, object?>;

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

        var a = new Data("a", obj);
        var b = new Data("b", new Dictionary<string, object?>(obj));

        var result = a.Compare(b);
        var diff = result.Value as Dictionary<string, object?>;

        await Assert.That(diff).IsNotNull();
        await Assert.That(diff!["match"]).IsEqualTo(true);
    }

    [Test]
    public async Task Compare_DifferentObjects_ShowsFieldDiffs()
    {
        var expected = new Dictionary<string, object?> { ["module"] = "file", ["action"] = "read" };
        var actual = new Dictionary<string, object?> { ["module"] = "file", ["action"] = "save" };

        var a = new Data("a", expected);
        var b = new Data("b", actual);

        var result = a.Compare(b);
        var diff = result.Value as Dictionary<string, object?>;

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

        var a = new Data("a", expected);
        var b = new Data("b", actual);

        var result = a.Compare(b);
        var diff = result.Value as Dictionary<string, object?>;

        await Assert.That(diff).IsNotNull();
        await Assert.That(diff!["match"]).IsEqualTo(false);

        var missing = diff["missingFields"] as List<string>;
        await Assert.That(missing).IsNotNull();
        await Assert.That(missing!).Contains("action");
    }

    [Test]
    public async Task Compare_ExtraField_ReportsExtra()
    {
        var expected = new Dictionary<string, object?> { ["module"] = "file" };
        var actual = new Dictionary<string, object?> { ["module"] = "file", ["extra"] = "value" };

        var a = new Data("a", expected);
        var b = new Data("b", actual);

        var result = a.Compare(b);
        var diff = result.Value as Dictionary<string, object?>;

        await Assert.That(diff).IsNotNull();
        await Assert.That(diff!["match"]).IsEqualTo(false);

        var extra = diff["extraFields"] as List<string>;
        await Assert.That(extra).IsNotNull();
        await Assert.That(extra!).Contains("extra");
    }

    [Test]
    public async Task Compare_NullExpected_NullActual_MatchTrue()
    {
        var expected = new Dictionary<string, object?> { ["field"] = null };
        var actual = new Dictionary<string, object?> { ["field"] = null };

        var a = new Data("a", expected);
        var b = new Data("b", actual);

        var result = a.Compare(b);
        var diff = result.Value as Dictionary<string, object?>;

        await Assert.That(diff).IsNotNull();
        await Assert.That(diff!["match"]).IsEqualTo(true);
    }

    [Test]
    public async Task Compare_NullExpected_MissingActual_MatchTrue()
    {
        // null expected field == missing in actual (for optional .pr fields)
        var expected = new Dictionary<string, object?> { ["module"] = "file", ["onError"] = null };
        var actual = new Dictionary<string, object?> { ["module"] = "file" };

        var a = new Data("a", expected);
        var b = new Data("b", actual);

        var result = a.Compare(b);
        var diff = result.Value as Dictionary<string, object?>;

        await Assert.That(diff).IsNotNull();
        await Assert.That(diff!["match"]).IsEqualTo(true);
    }

    [Test]
    public async Task Compare_Arrays_DifferentLength_MatchFalse()
    {
        var expected = new List<object?> { "a", "b", "c" };
        var actual = new List<object?> { "a", "b" };

        var a = new Data("a", expected);
        var b = new Data("b", actual);

        var result = a.Compare(b);
        var diff = result.Value as Dictionary<string, object?>;

        await Assert.That(diff).IsNotNull();
        await Assert.That(diff!["match"]).IsEqualTo(false);
        await Assert.That(diff["expectedLength"]).IsEqualTo(3);
        await Assert.That(diff["actualLength"]).IsEqualTo(2);
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

        var a = new Data("a", expected);
        var b = new Data("b", actual);

        var result = a.Compare(b);
        var diff = result.Value as Dictionary<string, object?>;

        await Assert.That(diff).IsNotNull();
        await Assert.That(diff!["match"]).IsEqualTo(false);
    }

    [Test]
    public async Task Compare_BooleanValues_MatchTrue()
    {
        var a = new Data("a", true);
        var b = new Data("b", true);

        var result = a.Compare(b);
        var diff = result.Value as Dictionary<string, object?>;

        await Assert.That(diff).IsNotNull();
        await Assert.That(diff!["match"]).IsEqualTo(true);
    }

    [Test]
    public async Task Compare_ReturnIsData()
    {
        var a = new Data("a", "test");
        var b = new Data("b", "test");

        var result = a.Compare(b);

        await Assert.That(result).IsNotNull();
        await Assert.That(result.Name).IsEqualTo("comparison");
        await Assert.That(result.Value).IsNotNull();
    }
}
