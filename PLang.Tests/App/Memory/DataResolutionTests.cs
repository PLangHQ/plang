using global::App.Variables;

namespace PLang.Tests.App.Memory;

/// <summary>
/// Tests for Data.Value resolution, lazy factory, and setter contracts.
/// These guard against regressions during future Data refactoring.
/// </summary>
public class DataResolutionTests
{
    private global::App.@this _app = null!;
    private global::App.Actor.Context.@this Ctx => _app.Context;

    [Before(Test)]
    public void Setup()
    {
        _app = new global::App.@this("/app");
    }

    [After(Test)]
    public async Task Cleanup() => await _app.DisposeAsync();

    #region NeedsResolution — snapshot-once semantics

    // Resolution happens once on first access, then the resolved container is
    // cached. This keeps list.add / dict writes stable — container identity is
    // preserved across repeated .Value reads. Subsequent changes to referenced
    // variables do NOT re-propagate; the snapshot was taken at first read.
    [Test]
    public async Task Value_List_SnapshotsOnFirstAccess()
    {
        Ctx.Variables.Set("myVar", "first");

        var list = new List<object?> { "%myVar%" };
        var data = new Data("param", list) { NeedsResolution = true, Context = Ctx };

        var result1 = data.Value as List<object?>;
        await Assert.That(result1![0]).IsEqualTo("first");

        Ctx.Variables.Set("myVar", "second");

        var result2 = data.Value as List<object?>;
        await Assert.That(result2![0]).IsEqualTo("first");           // still the snapshot
        await Assert.That(object.ReferenceEquals(result1, result2)).IsTrue(); // same list ref
    }

    [Test]
    public async Task Value_Dict_SnapshotsOnFirstAccess()
    {
        Ctx.Variables.Set("host", "localhost");

        var dict = new Dictionary<string, object?> { ["url"] = "http://%host%/api" };
        var data = new Data("param", dict) { NeedsResolution = true, Context = Ctx };

        var result1 = data.Value as Dictionary<string, object?>;
        await Assert.That(result1!["url"]).IsEqualTo("http://localhost/api");

        Ctx.Variables.Set("host", "production.example.com");

        var result2 = data.Value as Dictionary<string, object?>;
        await Assert.That(result2!["url"]).IsEqualTo("http://localhost/api");   // snapshot
        await Assert.That(object.ReferenceEquals(result1, result2)).IsTrue();
    }

    [Test]
    public async Task Value_NestedListDict_SnapshotsDeep()
    {
        Ctx.Variables.Set("content", "hello world");

        var list = new List<object?>
        {
            new Dictionary<string, object?> { ["Role"] = "user", ["Content"] = "%content%" }
        };
        var data = new Data("messages", list) { NeedsResolution = true, Context = Ctx };

        var msg1 = (data.Value as List<object?>)![0] as Dictionary<string, object?>;
        await Assert.That(msg1!["Content"]).IsEqualTo("hello world");

        Ctx.Variables.Set("content", "goodbye world");

        var msg2 = (data.Value as List<object?>)![0] as Dictionary<string, object?>;
        await Assert.That(msg2!["Content"]).IsEqualTo("hello world");   // snapshot
    }

    #endregion

    #region NeedsResolution — edge cases

    [Test]
    public async Task Value_NeedsResolution_False_LeavesVarRefsLiteral()
    {
        Ctx.Variables.Set("x", "resolved");

        var list = new List<object?> { "%x%" };
        var data = new Data("param", list) { NeedsResolution = false, Context = Ctx };

        var result = data.Value as List<object?>;
        await Assert.That(result![0]).IsEqualTo("%x%");
    }

    [Test]
    public async Task Value_NeedsResolution_NullValue_ReturnsNull()
    {
        var data = new Data("param", null) { NeedsResolution = true, Context = Ctx };

        await Assert.That(data.Value).IsNull();
    }

    [Test]
    public async Task Value_NeedsResolution_NoContext_SkipsResolution()
    {
        // Without a context, resolution can't happen — return raw value
        var list = new List<object?> { "%x%" };
        var data = new Data("param", list) { NeedsResolution = true };
        // Context is null (not set)

        var result = data.Value as List<object?>;
        await Assert.That(result![0]).IsEqualTo("%x%");
    }

    [Test]
    public async Task Value_NeedsResolution_StringValue_NotResolvedByGetter()
    {
        // String values with %var% are NOT resolved by the Value getter —
        // only IList and IDictionary trigger ResolveDeep. String resolution
        // happens in the generated __ResolveData code instead.
        Ctx.Variables.Set("x", "resolved");

        var data = new Data("param", "%x%") { NeedsResolution = true, Context = Ctx };

        // Value getter returns the raw string — no resolution
        await Assert.That(data.Value).IsEqualTo("%x%");
    }

    [Test]
    public async Task Value_NeedsResolution_UndefinedVariable_ResolvesToNull()
    {
        // %undefinedVar% in a list resolves to null (variable doesn't exist)
        var list = new List<object?> { "%undefinedVar%" };
        var data = new Data("param", list) { NeedsResolution = true, Context = Ctx };

        var result = data.Value as List<object?>;
        await Assert.That(result![0]).IsNull();
    }

    [Test]
    public async Task Value_NeedsResolution_MultipleVarsInString_Interpolates()
    {
        Ctx.Variables.Set("a", "hello");
        Ctx.Variables.Set("b", "world");

        var list = new List<object?> { "%a% %b%!" };
        var data = new Data("param", list) { NeedsResolution = true, Context = Ctx };

        var result = data.Value as List<object?>;
        await Assert.That(result![0]).IsEqualTo("hello world!");
    }

    [Test]
    public async Task Value_NeedsResolution_FullVarMatch_ReturnsObject()
    {
        // When a list entry is exactly "%var%", ResolveDeep returns the actual
        // object (not stringified) — preserves type
        var myList = new List<object?> { 1, 2, 3 };
        Ctx.Variables.Set("nums", myList);

        var list = new List<object?> { "%nums%" };
        var data = new Data("param", list) { NeedsResolution = true, Context = Ctx };

        var result = data.Value as List<object?>;
        await Assert.That(result![0]).IsEqualTo(myList);
    }

    #endregion

    #region Lazy SetValue factory

    [Test]
    public async Task SetValue_Factory_InvokedOnFirstAccess()
    {
        int callCount = 0;
        var data = new Data("lazy");
        data.SetValue(() => { callCount++; return "computed"; });

        await Assert.That(callCount).IsEqualTo(0);
        await Assert.That(data.Value).IsEqualTo("computed");
        await Assert.That(callCount).IsEqualTo(1);
    }

    [Test]
    public async Task SetValue_Factory_CachedAfterFirstAccess()
    {
        int callCount = 0;
        var data = new Data("lazy");
        data.SetValue(() => { callCount++; return "computed"; });

        _ = data.Value; // first
        _ = data.Value; // second
        await Assert.That(callCount).IsEqualTo(1);
    }

    [Test]
    public async Task SetValue_Factory_ThenResolution_FactoryFirst()
    {
        // When both factory and NeedsResolution are set, factory fires first,
        // then resolution processes the factory's result
        Ctx.Variables.Set("x", "world");

        var data = new Data("param") { NeedsResolution = true, Context = Ctx };
        data.SetValue(() => new List<object?> { "hello %x%" });

        var result = data.Value as List<object?>;
        await Assert.That(result![0]).IsEqualTo("hello world");
    }

    #endregion

    #region Value setter contracts

    [Test]
    public async Task ValueSetter_ClearsExplicitType_SoItRederives()
    {
        var data = new Data("x", "42", global::App.Data.Type.FromName("int"));
        await Assert.That(data.Type!.Value).IsEqualTo("int");

        // Setting a new value clears the cached type — it re-derives from the new value
        data.Value = "new value";
        await Assert.That(data.Type!.Value).IsEqualTo("string");
    }

    [Test]
    public async Task ValueSetter_ClearsRawValue()
    {
        Ctx.Variables.Set("v", "original");

        var list = new List<object?> { "%v%" };
        var data = new Data("param", list) { NeedsResolution = true, Context = Ctx };

        // Trigger resolution (creates _rawValue)
        _ = data.Value;

        // Set new value — raw cache must be cleared
        data.Value = new List<object?> { "%v%-new" };
        data.NeedsResolution = true;

        Ctx.Variables.Set("v", "updated");
        var result = data.Value as List<object?>;
        await Assert.That(result![0]).IsEqualTo("updated-new");
    }

    [Test]
    public async Task ValueSetter_ClearsFactory()
    {
        var data = new Data("x");
        data.SetValue(() => "from factory");

        // Override with direct value before factory fires
        data.Value = "direct";

        await Assert.That(data.Value).IsEqualTo("direct");
    }

    [Test]
    public async Task ValueSetter_UpdatesTimestamp()
    {
        var data = new Data("x", "initial");
        var before = data.Updated;

        await Task.Delay(5);
        data.Value = "changed";

        await Assert.That(data.Updated).IsGreaterThan(before);
    }

    #endregion
}
