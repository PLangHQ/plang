namespace PLang.Tests.App.DataTests;

// Contract tests: Data.Value is read-only post-construction — no side effects, no
// resolution of unstamped placeholders, no caching surprises. Scalars ride verbatim;
// raw CLR collections become the native dict/list value type on store (a copy), so
// identity is to the native instance, not the raw input collection.

public class DataValueRawTests
{
    private global::app.@this _app = null!;

    [Before(Test)]
    public void Setup() => _app = global::PLang.Tests.TestApp.Create("/app");

    [After(Test)]
    public async Task TearDown() { await _app.DisposeAsync(); }

    // .Value returns whatever the constructor set — no transformation, no Variables lookup.
    [Test]
    public async Task Value_AfterConstruction_ReturnsRawAsSet()
    {
        var data = _app.Data("greeting", "hello world");
        await Assert.That((await data.Value())?.ToString()).IsEqualTo("hello world");
    }

    // Reading .Value 1000 times → no work performed; the native value instance is
    // stable across reads (Peek never re-mints).
    [Test]
    public async Task Value_ReadRepeatedly_NoSideEffects()
    {
        var data = _app.Data("list", new List<object?> { "a", "b", "c" });
        var native = data.Peek();

        for (int i = 0; i < 1000; i++)
        {
            await data.Value();
            await Assert.That(ReferenceEquals(data.Peek(), native)).IsTrue();
        }
    }

    // .Value on a string with "%var%" content → returns the literal "%var%" string, NOT a substituted value.
    [Test]
    public async Task Value_StringWithVarPlaceholder_ReturnsRawNotSubstituted()
    {
        _app.User.Context.Variable.Set("name", "world");
        var data = new Data("greeting", "Hello %name%", context: _app.User.Context);

        await Assert.That((await data.Value())?.ToString()).IsEqualTo("Hello %name%");
    }

    // .Value on a List<object?> with nested %var% items → returns the original list reference, items unchanged.
    [Test]
    public async Task Value_ListWithVarPlaceholders_ReturnsRawListUnchanged()
    {
        _app.User.Context.Variable.Set("x", "actual");
        var raw = new List<object?> { "%x%", "literal", "%x%" };
        var data = new Data("list", raw, context: _app.User.Context);

        // The list rides as native; reading it does not resolve the unstamped
        // %x% placeholders — they stay literal.
        var read = Lower<List<object?>>(await data.Value());
        await Assert.That((string)read![0]!).IsEqualTo("%x%");
        await Assert.That((string)read![2]!).IsEqualTo("%x%");
    }

    // .Value on a Dictionary with nested %var% values → returns original dict, no substitution.
    [Test]
    public async Task Value_DictWithVarPlaceholders_ReturnsRawDictUnchanged()
    {
        _app.User.Context.Variable.Set("user", "alice");
        var raw = new Dictionary<string, object?> { ["name"] = "%user%", ["role"] = "admin" };
        var data = new Data("dict", raw, context: _app.User.Context);

        // The dict rides as native; reading it does not resolve the unstamped
        // %user% placeholder — it stays literal.
        var read = Lower<Dictionary<string, object?>>(await data.Value());
        await Assert.That((string)read!["name"]!).IsEqualTo("%user%");
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
        var stored = _app.Data("greeting", "Hello %name%");
        var action = new PrAction
        {
            Module = "test",
            ActionName = "fixture",
            Parameter = new List<Data> { stored }
        };

        var found = action.GetParameter("greeting", _app.User.Context);

        await Assert.That(ReferenceEquals(found, stored)).IsTrue();
        await Assert.That((await found.Value())?.ToString()).IsEqualTo("Hello %name%");
    }

    // Two parallel readers of the same Data → no race, no shared mutation, same .Value reference.
    [Test]
    public async Task Value_ConcurrentReads_NoRace()
    {
        var data = _app.Data("list", new List<object?> { 1, 2, 3 });
        var native = data.Peek();   // the native list value, set once at store

        var tasks = Enumerable.Range(0, 100).Select(_ => Task.Run(async () =>
        {
            for (int i = 0; i < 100; i++)
            {
                var _v = await data.Value();
            }
            return ReferenceEquals(data.Peek(), native);
        })).ToArray();

        var results = await Task.WhenAll(tasks);
        await Assert.That(results.All(r => r)).IsTrue();
    }
}
