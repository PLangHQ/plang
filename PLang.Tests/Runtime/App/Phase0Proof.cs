using app;
using app.error;
using app.type.item.variable;
using app.Utils;
using R2 = global::app.data;

namespace PLang.Tests.App;

/// <summary>
/// Phase 0 proof tests — each test demonstrates a specific phase's behavior
/// with clear input → output mapping for black-box validation.
/// </summary>
public class Phase0Proof : System.IAsyncDisposable
{
    private readonly global::app.@this app = new global::app.@this("/app").Testing();
    public async System.Threading.Tasks.ValueTask DisposeAsync() => await app.DisposeAsync();

    // ================================================================
    // Phase 0.1 — Data.FromError() (renamed from Data.Fail())
    // ================================================================

    [Test]
    public async Task Phase01_DataFromError_CreatesErrorResult()
    {
        // INPUT: create a Data result from an error
        var error = new Error("File not found", "NotFound", 404);
        var result = Data.FromError(error);

        // OUTPUT: Data has error, is not successful
        await result.IsFailure();
        await Assert.That(result.Error).IsNotNull();
        await Assert.That(result.Error!.Message).IsEqualTo("File not found");
        await Assert.That(result.Error!.Status.Code.ToInt32()).IsEqualTo(404);
    }

    [Test]
    public async Task Phase01_DataOk_StillWorks()
    {
        // INPUT: create a successful Data result
        var result = app.actor.list.User.Context.Ok("hello world");

        // OUTPUT: Data has value, is successful
        await result.IsSuccess();
        await Assert.That(result.Error).IsNull();
        await Assert.That((await result.Value())?.ToString()).IsEqualTo("hello world");
    }

    [Test]
    public async Task Phase01_GenericDataFromError()
    {
        // INPUT: generic global::app.data.@this<T>.FromError
        var result = global::app.data.@this<global::app.type.item.text.@this>.FromError(new Error("oops"));

        // OUTPUT: typed error result
        await result.IsFailure();
        await Assert.That(result.Error!.Message).IsEqualTo("oops");
    }

    // ================================================================
    // Phase 0.2 — Error Categories
    // ================================================================







    // ================================================================
    // Phase 0.4 — Type Preservation (list handlers return explicit types)
    // ================================================================

    [Test]
    public async Task Phase04_ListType_IsPreserved()
    {
        // INPUT: Data.Ok with a native list — the list names its own type
        var ctx = app.actor.list.User.Context;
        var listValue = new global::app.type.item.list.@this();
        foreach (var n in new[] { 1, 2, 3 }) listValue.Add(new Data("", n, context: ctx));
        var result = ctx.Ok(listValue);

        // OUTPUT: Type is "list", not the CLR type name
        await Assert.That(result.Type).IsNotNull();
        await Assert.That(result.Type!.Name).IsEqualTo("list");
    }

    [Test]
    public async Task Phase04_ScalarType_AutoInferred()
    {
        // INPUT: Data.Ok with an int value (no explicit type)
        var result = app.actor.list.User.Context.Ok(42);

        // OUTPUT: Type auto-inferred as "int"
        await Assert.That(result.Type).IsNotNull();
        await Assert.That(result.Type!.Name).IsEqualTo("number");
    }
}
