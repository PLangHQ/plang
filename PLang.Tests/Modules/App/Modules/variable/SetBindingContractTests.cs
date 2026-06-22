using app.actor.context;
using app;
using app.variable;

namespace PLang.Tests.App.actions.variable;

/// <summary>
/// FLOOR — the parts of variable.set with no PLang language surface, kept as C# units
/// (per Documentation/v0.2/writing-tests.md "the floor"):
///
///   • build-time validation (ValidateBuild) — runs during the build, which Make.Goal bypasses;
///   • the set-binding contract internals — the CLR exit door returning the *same instance*
///     after a pure read (no-copy aliasing, O(1)), slot elevation rebuilding the backing,
///     and the Data&lt;T&gt; vs plain-Data mint shape. A PLang author observes the *consequence*
///     (mutation visibility — see VariableGoalRunTests.Set_ListAlias_…), never the C# reference
///     identity or the minted CLR type, so those stay here.
/// </summary>
public class SetBindingContractTests
{
    private global::app.@this _app = null!;

    [Before(Test)]
    public void Setup() => _app = TestApp.Create("/app");

    [After(Test)]
    public async Task TearDown() => await _app.DisposeAsync();

    // --- ValidateBuild (build-time, sync) ---

    [Test]
    public async Task ValidateBuild_VariableReference_ReturnsNull()
    {
        var parameters = new List<Data>
        {
            new Data("Value", "%myVar%", global::app.type.@this.FromName("int"))
        };
        await Assert.That(global::app.module.variable.Set.ValidateBuild(parameters)).IsNull();
    }

    [Test]
    public async Task ValidateBuild_TypeMismatch_ReturnsError()
    {
        var parameters = new List<Data>
        {
            new Data("Value", "not a number", global::app.type.@this.FromName("int"))
        };
        var result = global::app.module.variable.Set.ValidateBuild(parameters);
        await Assert.That(result).IsNotNull();
        await Assert.That(result!).Contains("type=number");
    }

    [Test]
    public async Task ValidateBuild_ValidTypeMatch_ReturnsNull()
    {
        var parameters = new List<Data>
        {
            new Data("Value", 42, global::app.type.@this.FromName("int"))
        };
        await Assert.That(global::app.module.variable.Set.ValidateBuild(parameters)).IsNull();
    }

    // --- binding contract: CLR exit door same-ref / no-copy (no language surface) ---

    [Test]
    public async Task Set_ListValue_AliasesSourceInstance()
    {
        var context = _app.User.Context;
        var src = new List<object?> { "a", "b" };
        await (await TestAction.Create("variable", "set", ("name", "%list%"), ("value", src)).RunAsync(context)).IsSuccess();
        var lst = (await (await context.Variable.Get("list")).Value()) as global::app.type.list.@this;
        await Assert.That(lst).IsNotNull();
        // The CLR exit door returns the aliased backing itself — same ref, O(1).
        await Assert.That(ReferenceEquals(lst!.Clr<List<object?>>(), src)).IsTrue();
    }

    [Test]
    public async Task Set_ListValue_AliasNotCopied_SourceEditVisible()
    {
        var context = _app.User.Context;
        var src = new List<object?> { "a", "b" };
        await (await TestAction.Create("variable", "set", ("name", "%list%"), ("value", src)).RunAsync(context)).IsSuccess();
        src.Add("c");
        var lst = (await (await context.Variable.Get("list")).Value()) as global::app.type.list.@this;
        await Assert.That(lst!.CountRaw).IsEqualTo(3);
    }

    [Test]
    public async Task Set_ListIndexWrite_ElevatesSlot_ClrRebuilds()
    {
        var context = _app.User.Context;
        var src = new List<object?> { "a", "b", "c" };
        var lst = new global::app.type.list.@this(src) { Context = context };

        lst.SetAt(2, new Data("", 9L));

        await Assert.That(ReferenceEquals(lst.Clr<List<object?>>(), src)).IsFalse();
        await Assert.That((await lst.At(2)!.Value())).IsTypeOf<global::app.type.number.@this>();
    }

    [Test]
    public async Task Set_DictValue_AliasesSourceInstance()
    {
        var context = _app.User.Context;
        var src = new Dictionary<string, object?> { ["k"] = "v" };
        await (await TestAction.Create("variable", "set", ("name", "%d%"), ("value", src)).RunAsync(context)).IsSuccess();
        var d = (await (await context.Variable.Get("d")).Value()) as global::app.type.dict.@this;
        await Assert.That(d).IsNotNull();
        await Assert.That((await d!.Get("k")!.Value())?.ToString()).IsEqualTo("v");
        await Assert.That(ReferenceEquals(d.Clr<Dictionary<string, object?>>(), src)).IsTrue();
    }

    // --- binding contract: minted Data shape (Data<T> vs plain Data) ---

    [Test]
    public async Task Set_ForcedType_String_MintsDataOfText()
    {
        var context = _app.User.Context;
        await (await TestAction.Create("variable", "set", ("name", "%n%"), ("value", 42), ("type", "string")).RunAsync(context)).IsSuccess();
        var stored = await context.Variable.Get("n");
        await Assert.That(stored).IsTypeOf<global::app.data.@this<global::app.type.text.@this>>();
    }

    [Test]
    public async Task Set_NullValue_MintsPlainDataNotGeneric()
    {
        var context = _app.User.Context;
        await (await TestAction.Create("variable", "set", ("name", "%x%"), ("value", null)).RunAsync(context)).IsSuccess();
        var stored = await context.Variable.Get("x");
        // Plain Data (not Data<T>) — null can't be type-inferred.
        await Assert.That(stored.GetType()).IsEqualTo(typeof(global::app.data.@this));
    }

    // --- Action.RunAsync contract: result aliased under %!data% without renaming ---

    [Test]
    public async Task ActionRunAsync_AliasesResultUnderData_DoesNotMutateName()
    {
        // F3-2: RunAsync aliases the handler result under %!data% WITHOUT mutating
        // result.Name. Old code did `result.Name = "!data"; Variables.Put(result)` —
        // corrupting any producer Data shared between %!data% and its source entry.
        var context = _app.User.Context;
        var action = TestAction.Create("variable", "set", ("name", "%myVar%"), ("value", "hello"));
        var result = await action.RunAsync(context);
        await result.IsSuccess();

        var dataVar = await context.Variable.Get("!data");
        var myVar = await context.Variable.Get("myVar");

        await Assert.That(ReferenceEquals(dataVar, result)).IsTrue();
        await Assert.That(ReferenceEquals(myVar, result)).IsTrue();
        await Assert.That(result.Name).IsNotEqualTo("!data");
    }

    [Test]
    public async Task Set_Alias_PropertyWrite_LandsOnAliasOnly()
    {
        var context = _app.User.Context;
        context.Variable.Set("x", "payload");
        await (await TestAction.Create("variable", "set", ("name", "%y%"), ("value", "%x%")).RunAsync(context)).IsSuccess();

        var y = await context.Variable.Get("y");
        y.Properties["NewProp"] = 1L;

        var x = await context.Variable.Get("x");
        await Assert.That(x.Properties.ContainsKey("NewProp")).IsFalse();
        await Assert.That(y.Properties.ContainsKey("NewProp")).IsTrue();
    }
}
