namespace PLang.Tests.App.DataTests;

// Contract tests for v4's central architectural sharpening:
// Data.Value is RAW — read-only post-construction, no side effects, no resolution, no caching.
// Data flows through the system unchanged at the .Value level; resolution lives in As<T>(context).

public class DataValueRawTests
{
    private global::app.@this _app = null!;

    [Before(Test)]
    public void Setup() => _app = new global::app.@this("/app");

    [After(Test)]
    public async Task TearDown() { await _app.DisposeAsync(); }

    // .Value returns whatever the constructor set — no transformation, no Variables lookup.
    [Test]
    public async Task Value_AfterConstruction_ReturnsRawAsSet()
    {
        var data = new Data("greeting", "hello world");
        await Assert.That(data.Value).IsEqualTo("hello world");
    }

    // Reading .Value 1000 times → no work performed; same reference returned every time.
    [Test]
    public async Task Value_ReadRepeatedly_NoSideEffects()
    {
        var raw = new List<object?> { "a", "b", "c" };
        var data = new Data("list", raw);

        for (int i = 0; i < 1000; i++)
        {
            await Assert.That(ReferenceEquals(data.Value, raw)).IsTrue();
        }
    }

    // .Value on a string with "%var%" content → returns the literal "%var%" string, NOT a substituted value.
    [Test]
    public async Task Value_StringWithVarPlaceholder_ReturnsRawNotSubstituted()
    {
        _app.User.Context.Variable.Set("name", "world");
        var data = new Data("greeting", "Hello %name%") { Context = _app.User.Context };

        await Assert.That(data.Value).IsEqualTo("Hello %name%");
    }

    // .Value on a List<object?> with nested %var% items → returns the original list reference, items unchanged.
    [Test]
    public async Task Value_ListWithVarPlaceholders_ReturnsRawListUnchanged()
    {
        _app.User.Context.Variable.Set("x", "actual");
        var raw = new List<object?> { "%x%", "literal", "%x%" };
        var data = new Data("list", raw) { Context = _app.User.Context };

        var read = await data.Value();
        await Assert.That(ReferenceEquals(read, raw)).IsTrue();
        await Assert.That((string)((List<object?>)read!)[0]!).IsEqualTo("%x%");
        await Assert.That((string)((List<object?>)read!)[2]!).IsEqualTo("%x%");
    }

    // .Value on a Dictionary with nested %var% values → returns original dict, no substitution.
    [Test]
    public async Task Value_DictWithVarPlaceholders_ReturnsRawDictUnchanged()
    {
        _app.User.Context.Variable.Set("user", "alice");
        var raw = new Dictionary<string, object?> { ["name"] = "%user%", ["role"] = "admin" };
        var data = new Data("dict", raw) { Context = _app.User.Context };

        var read = await data.Value();
        await Assert.That(ReferenceEquals(read, raw)).IsTrue();
        await Assert.That((string)((Dictionary<string, object?>)read!)["name"]!).IsEqualTo("%user%");
    }

    // .Value reads do NOT depend on Context/Variables — even with no context attached, .Value works.
    [Test]
    public async Task Value_NoContextAttached_StillReadable()
    {
        var data = new Data("greeting", "Hello %name%");
        // No context attached.
        await Assert.That(data.Value).IsEqualTo("Hello %name%");
    }

    // After v4: Data has no _resolved field. Reflection check guards against re-introduction.
    [Test]
    public async Task Data_HasNoResolvedField_AfterV4()
    {
        var fields = typeof(Data).GetFields(
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        await Assert.That(fields.Any(f => f.Name == "_resolved")).IsFalse();
        await Assert.That(fields.Any(f => f.Name == "_rawValue")).IsFalse();
    }

    // After v4: Data has no ResetResolution method.
    [Test]
    public async Task Data_HasNoResetResolutionMethod_AfterV4()
    {
        var method = typeof(Data).GetMethod("ResetResolution",
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        await Assert.That(method).IsNull();
    }

    // After v4: Data has no IsDeferredActionTemplate gate (the carve-out moved into As<T>'s walker).
    [Test]
    public async Task Data_HasNoIsDeferredActionTemplateGate_AfterV4()
    {
        var method = typeof(Data).GetMethod("IsDeferredActionTemplate",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        await Assert.That(method).IsNull();
    }

    // Data flows through Action.GetParameter unchanged — the same Data instance is returned.
    [Test]
    public async Task DataFlow_ThroughGetParameter_ReferenceIdentityPreserved()
    {
        var stored = new Data("greeting", "Hello %name%");
        var action = new PrAction
        {
            Module = "test",
            ActionName = "fixture",
            Parameters = new List<Data> { stored }
        };

        var found = action.GetParameter("greeting", _app.User.Context);

        await Assert.That(ReferenceEquals(found, stored)).IsTrue();
        await Assert.That(found.Value).IsEqualTo("Hello %name%");
    }

    // Two parallel readers of the same Data → no race, no shared mutation, same .Value reference.
    [Test]
    public async Task Value_ConcurrentReads_NoRace()
    {
        var raw = new List<object?> { 1, 2, 3 };
        var data = new Data("list", raw);

        var tasks = Enumerable.Range(0, 100).Select(_ => Task.Run(() =>
        {
            for (int i = 0; i < 100; i++)
            {
                var _v = data.Value;
            }
            return ReferenceEquals(data.Value, raw);
        })).ToArray();

        var results = await Task.WhenAll(tasks);
        await Assert.That(results.All(r => r)).IsTrue();
    }
}
