using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using data = global::app.data.@this;
using type = global::app.type.@this;

namespace PLang.Tests.App.LazyDeserialize.AccessResolutionTests;

// Navigation (`%x.field%`) materialises through the known type's reader;
// `kind` says how. If the type is unknown there is **no guessing** — the
// caller gets a clear error and is told to add `as <type>`.
public class NavigationAccessTests
{
    private static global::app.@this NewApp()
        => new(System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang-nav-" + System.Guid.NewGuid().ToString("N")[..8]));

    [Test] public async Task Navigation_KnownType_MaterialisesViaReader_AndNavigates()
    {
        await using var app = NewApp();
        var ctx = app.User.Context;
        var d = data.FromRaw("{\"port\":8080}", type.Create("object", "json", context: ctx), ctx, "cfg");
        await Assert.That((await (await d.GetChild("port")).Value())?.ToString()).IsEqualTo("8080");
        await Assert.That(d.MaterializeCount).IsEqualTo(1); // navigation materialized via the reader
    }

    // Architect 829785fbe — the type's *shape* decides the navigation model.
    // `object` navigates by key.
    [Test] public async Task Navigation_ObjectShape_NavigatesByKey()
    {
        await using var app = NewApp();
        var ctx = app.User.Context;
        var d = data.FromRaw("{\"host\":\"localhost\"}", type.Create("object", "json", context: ctx), ctx, "cfg");
        await Assert.That((await (await d.GetChild("host")).Value())?.ToString()).IsEqualTo("localhost");
    }

    // `table` navigates by row/column — `%t.rows[0].name%` — not flat key lookup.
    [Test] public async Task Navigation_TableShape_NavigatesByRowColumn()
    {
        await using var app = NewApp();
        var ctx = app.User.Context;
        var d = data.FromRaw("name,age\nAda,36\n", type.Create("table", "csv", context: ctx), ctx, "t");
        var cell = (await (await (await d.GetChild("rows")).GetChild("0")).GetChild("name"));
        await Assert.That((await cell.Value())?.ToString()).IsEqualTo("Ada");
    }

    // The contract error — type-unknown navigation does not guess.
    [Test] public async Task Navigation_TypeUnknown_ProducesAddAsTypeError()
    {
        var d = data.Ok("{\"port\":8080}"); // authored string, no type stamp
        var r = await d.GetChild("port");
        await Assert.That(r.Success).IsFalse();
        await Assert.That(r.Error).IsNotNull();
    }

    // Independent #18 — the exact phrasing is the LLM teaching surface. Pin the
    // literal substrings `add ` and `as ` so they stay the contract.
    [Test] public async Task Navigation_TypeUnknownErrorMessage_ContainsLiteralAsType()
    {
        var d = data.Ok("{\"port\":8080}");
        var r = await d.GetChild("port");
        await Assert.That(r.Error!.Message).Contains("add ");
        await Assert.That(r.Error!.Message).Contains("as ");
    }

    // An authored dict value is already structured — navigation walks it
    // directly without invoking the reader.
    [Test] public async Task Navigation_OnAuthoredDictValue_DoesNotTriggerReader()
    {
        var dict = new System.Collections.Generic.Dictionary<string, object?> { ["port"] = 8080L };
        var d = data.Ok(dict);
        await Assert.That((await (await d.GetChild("port")).Value())?.ToString()).IsEqualTo("8080");
        await Assert.That(d.MaterializeCount).IsEqualTo(0);
    }
}
