using app;

namespace PLang.Tests.Generator;

/// <summary>
/// A missing-or-null variable-name slot must produce a ServiceError with a
/// specific Key, not let a null Variable flow into the handler body where
/// the implicit Variable→string operator would NRE and bubble up as a
/// generic "StepError". The generator emits a MissingRequiredParameter
/// ServiceError when a Data&lt;T : IRawNameResolvable&gt; slot resolves to
/// null Value; this test pins the contract one row per handler.
///
/// One row per handler whose contract this pins. Foreach (loop) is intentionally
/// excluded — its ItemName/KeyName slots are nullable Data&lt;Variable&gt; and the
/// nullable getter is permissive by design (Run() reads `?.Value?.Name ?? "item"`).
/// </summary>
public class MissingVariableNameTests
{
    private global::app.@this _app = null!;

    [Before(Test)]
    public void Setup() => _app = new global::app.@this("/app");

    [After(Test)]
    public async Task TearDown() { await _app.DisposeAsync(); }

    [Test]
    [Arguments("variable", "get", "name")]
    [Arguments("variable", "set", "name")]
    [Arguments("variable", "exists", "name")]
    [Arguments("variable", "remove", "name")]
    [Arguments("list", "add", "listname")]
    [Arguments("list", "any", "listname")]
    [Arguments("list", "contains", "listname")]
    [Arguments("list", "count", "listname")]
    [Arguments("list", "first", "listname")]
    [Arguments("list", "flatten", "listname")]
    [Arguments("list", "get", "listname")]
    [Arguments("list", "group", "listname")]
    [Arguments("list", "indexof", "listname")]
    [Arguments("list", "join", "listname")]
    [Arguments("list", "last", "listname")]
    [Arguments("list", "remove", "listname")]
    [Arguments("list", "reverse", "listname")]
    [Arguments("list", "set", "listname")]
    [Arguments("list", "sort", "listname")]
    [Arguments("list", "unique", "listname")]
    public async Task MissingVariableName_Returns_MissingRequiredParameter_Error(
        string module, string action, string slotName)
    {
        var context = _app.User.Context;
        // Action constructed with the Variable-name slot omitted. list.any/group also
        // require [IsNotNull] Key (and Operator), so we provide those to ensure the
        // missing-listname slot is the failure cause — not a sibling [IsNotNull] check.
        var extras = (module, action) switch
        {
            ("list", "any") => new (string, object?)[] { ("key", "x"), ("operator", "Equals") },
            ("list", "group") => new (string, object?)[] { ("key", "x") },
            _ => System.Array.Empty<(string, object?)>(),
        };
        var act = TestAction.Create(module, action, extras);

        var result = await act.RunAsync(context);

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error).IsNotNull();
        await Assert.That(result.Error!.Key).IsEqualTo("MissingRequiredParameter");
        await Assert.That(result.Error!.Message).Contains(slotName);
    }
}
