using app.actor.context;
using app;
using app.variable;

namespace PLang.Tests.App.actions.variable;

/// <summary>
/// Pilot: the variable-module behaviors expressed through the real path a PLang
/// author hits — PR built with <see cref="Make"/>, loaded via <see cref="RealGoalLoad"/>
/// (the .pr read boundary), and run through the engine. Replaces the hand-constructed
/// <c>new Set{...}.Run()</c> / <c>TestAction.RunAsync</c> unit style.
///
/// Each test proves the full stack: born-typing, %var% resolution, Data&lt;T&gt; wiring,
/// source-gen guards, dispatch — none of which the unit style exercised.
/// </summary>
public class VariableGoalRunTests
{
    private static async Task<(global::app.@this engine, global::app.actor.context.@this ctx, global::app.data.@this result)>
        Run(global::app.goal.@this spec)
    {
        var engine = TestApp.Create("/app");
        var goal = await RealGoalLoad.ViaChannel(engine, spec);
        engine.Goal.Add(goal);
        var ctx = engine.User.Context;
        var result = await engine.RunGoalAsync(goal, ctx);
        return (engine, ctx, result);
    }

    // ---- set ----

    [Test]
    public async Task Set_SetsVariable()
    {
        var (engine, ctx, result) = await Run(Make.Goal("T",
            Make.Step("set %testVar% = \"testValue\"",
                Make.Action("variable", "set", Make.Param("Name", "testVar", "variable"), ("Value", "testValue")))));
        await using var _ = engine;
        await result.IsSuccess();
        await Assert.That(await ctx.Variable.GetValue("testVar")).IsEqualTo("testValue");
        // F3-1: set returns the stored value, not an empty Data.Ok() (powers %!data% capture).
        await Assert.That((await result.Value())?.ToString()).IsEqualTo("testValue");
    }

    [Test]
    public async Task Set_WithType_SetsTypeInfo()
    {
        var (engine, ctx, result) = await Run(Make.Goal("T",
            Make.Step("set %count% = 42",
                Make.Action("variable", "set", Make.Param("Name", "count", "variable"), ("Value", 42), ("Type", "int")))));
        await using var _ = engine;
        await result.IsSuccess();
        var stored = await ctx.Variable.Get("count");
        await Assert.That(stored!.Type!.Name).IsEqualTo("number");
    }

    [Test]
    public async Task Set_AsDefault_DoesNotOverwriteExisting()
    {
        var (engine, ctx, result) = await Run(Make.Goal("T",
            Make.Step("set %x% = \"original\"",
                Make.Action("variable", "set", Make.Param("Name", "x", "variable"), ("Value", "original"))),
            Make.Step("set %x% = \"default\" as default",
                Make.Action("variable", "set", Make.Param("Name", "x", "variable"), ("Value", "default"), ("AsDefault", true)))));
        await using var _ = engine;
        await result.IsSuccess();
        await Assert.That(await ctx.Variable.GetValue("x")).IsEqualTo("original");
        // F3-1: AsDefault hitting an existing var returns the existing value, not empty.
        await Assert.That((await result.Value())?.ToString()).IsEqualTo("original");
    }

    [Test]
    public async Task Set_AsDefault_SetsWhenNotExists()
    {
        var (engine, ctx, result) = await Run(Make.Goal("T",
            Make.Step("set %y% = \"default\" as default",
                Make.Action("variable", "set", Make.Param("Name", "y", "variable"), ("Value", "default"), ("AsDefault", true)))));
        await using var _ = engine;
        await result.IsSuccess();
        await Assert.That(await ctx.Variable.GetValue("y")).IsEqualTo("default");
    }

    // ---- type inference ----

    [Test]
    [Arguments("hello", "text")]
    [Arguments(42, "number")]
    [Arguments(42L, "number")]
    [Arguments(3.14, "number")]
    [Arguments(true, "bool")]
    public async Task Set_InfersType(object value, string expectedTypeName)
    {
        var (engine, ctx, result) = await Run(Make.Goal("T",
            Make.Step("set %v%",
                Make.Action("variable", "set", Make.Param("Name", "v", "variable"), ("Value", value)))));
        await using var _ = engine;
        await result.IsSuccess();
        var stored = await ctx.Variable.Get("v");
        await Assert.That(stored!.Type!.Name).IsEqualTo(expectedTypeName);
    }

    [Test]
    public async Task Set_InfersDateTimeType()
    {
        var (engine, ctx, result) = await Run(Make.Goal("T",
            Make.Step("set %t%",
                Make.Action("variable", "set", Make.Param("Name", "t", "variable"),
                    ("Value", new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc))))));
        await using var _ = engine;
        await result.IsSuccess();
        await Assert.That((await ctx.Variable.Get("t"))!.Type!.Name).IsEqualTo("datetime");
    }

    [Test]
    public async Task Set_ForcedType_ConversionFailure_Fails()
    {
        var (engine, _, result) = await Run(Make.Goal("T",
            Make.Step("set %n% = \"abc\" (int)",
                Make.Action("variable", "set", Make.Param("Name", "n", "variable"), ("Value", "abc"), ("Type", "int")))));
        await using var _e = engine;
        await Assert.That(result.Success).IsFalse();
    }

    [Test]
    public async Task Set_ForcedType_Success_ConvertsValueAndType()
    {
        // int 42 forced to string → value "42", derived type "text".
        var (engine, ctx, result) = await Run(Make.Goal("T",
            Make.Step("set %n% = 42 (string)",
                Make.Action("variable", "set", Make.Param("Name", "n", "variable"), ("Value", 42), ("Type", "string")))));
        await using var _ = engine;
        await result.IsSuccess();
        var stored = await ctx.Variable.Get("n");
        await Assert.That(stored!.Type!.Name).IsEqualTo("text");
        await Assert.That((await stored.Value())?.ToString()).IsEqualTo("42");
    }

    [Test]
    public async Task Set_NullValue_StoresEmpty()
    {
        var (engine, ctx, result) = await Run(Make.Goal("T",
            Make.Step("set %x% = null",
                Make.Action("variable", "set", Make.Param("Name", "x", "variable"), ("Value", null)))));
        await using var _ = engine;
        await result.IsSuccess();
        var stored = await ctx.Variable.Get("x");
        await Assert.That(await stored!.IsEmpty()).IsTrue();
    }

    [Test]
    public async Task Set_ListAlias_InPlaceAddVisibleThroughBothNames()
    {
        // The [1,2,3] rule: set %y% = %x% shares the list instance — an add through
        // one name is visible through the other. Pure language behavior.
        var (engine, ctx, result) = await Run(Make.Goal("T",
            Make.Step("set %x% = [1, 2]",
                Make.Action("variable", "set", Make.Param("Name", "x", "variable"),
                    ("Value", new List<object?> { 1L, 2L }))),
            Make.Step("set %y% = %x%",
                Make.Action("variable", "set", Make.Param("Name", "y", "variable"), ("Value", "%x%"))),
            Make.Step("add 3 to %x%",
                Make.Action("list", "add", Make.Param("ListName", "x", "variable"), ("Value", 3)))));
        await using var _ = engine;
        await result.IsSuccess();
        var yList = (await (await ctx.Variable.Get("y"))!.Value()) as global::app.type.list.@this;
        await Assert.That(yList).IsNotNull();
        await Assert.That(yList!.CountRaw).IsEqualTo(3);
        await Assert.That((await yList.At(2)!.Value())?.ToString()).IsEqualTo("3");
    }

    // ---- get ----

    [Test]
    public async Task Get_ReturnsRawValue()
    {
        var (engine, ctx, result) = await Run(Make.Goal("T",
            Make.Step("set %testVar% = \"testValue\"",
                Make.Action("variable", "set", Make.Param("Name", "testVar", "variable"), ("Value", "testValue"))),
            Make.Step("get %testVar%",
                Make.Action("variable", "get", Make.Param("Name", "testVar", "variable")))));
        await using var _ = engine;
        await result.IsSuccess();
        await Assert.That((await result.Value())?.ToString()).IsEqualTo("testValue");
    }

    [Test]
    public async Task Get_Nonexistent_IsEmpty()
    {
        var (engine, _, result) = await Run(Make.Goal("T",
            Make.Step("get %nope%",
                Make.Action("variable", "get", Make.Param("Name", "nope", "variable")))));
        await using var _e = engine;
        await result.IsSuccess();
        await Assert.That(await (await result.Value())!.IsEmpty()).IsTrue();
    }

    // ---- exists ----

    [Test]
    public async Task Exists_Existing_True()
    {
        var (engine, _, result) = await Run(Make.Goal("T",
            Make.Step("set %testVar% = \"v\"",
                Make.Action("variable", "set", Make.Param("Name", "testVar", "variable"), ("Value", "v"))),
            Make.Step("does %testVar% exist",
                Make.Action("variable", "exists", Make.Param("Name", "testVar", "variable")))));
        await using var _e = engine;
        await result.IsSuccess();
        await Assert.That(await result.ToBooleanAsync()).IsTrue();
    }

    [Test]
    public async Task Exists_Nonexistent_False()
    {
        var (engine, _, result) = await Run(Make.Goal("T",
            Make.Step("does %nope% exist",
                Make.Action("variable", "exists", Make.Param("Name", "nope", "variable")))));
        await using var _e = engine;
        await result.IsSuccess();
        await Assert.That(await result.ToBooleanAsync()).IsFalse();
    }

    // ---- remove ----

    [Test]
    public async Task Remove_RemovesVariable()
    {
        var (engine, ctx, result) = await Run(Make.Goal("T",
            Make.Step("set %testVar% = \"v\"",
                Make.Action("variable", "set", Make.Param("Name", "testVar", "variable"), ("Value", "v"))),
            Make.Step("remove %testVar%",
                Make.Action("variable", "remove", Make.Param("Name", "testVar", "variable")))));
        await using var _ = engine;
        await result.IsSuccess();
        await Assert.That(ctx.Variable.Contains("testVar")).IsFalse();
    }

    [Test]
    public async Task Remove_Nonexistent_Succeeds()
    {
        var (engine, _, result) = await Run(Make.Goal("T",
            Make.Step("remove %nope%",
                Make.Action("variable", "remove", Make.Param("Name", "nope", "variable")))));
        await using var _e = engine;
        await result.IsSuccess();
    }

    // ---- clear ----

    [Test]
    public async Task Clear_ClearsUserVariables_PreservesSystem()
    {
        var (engine, ctx, result) = await Run(Make.Goal("T",
            Make.Step("set %userVar% = \"v\"",
                Make.Action("variable", "set", Make.Param("Name", "userVar", "variable"), ("Value", "v"))),
            Make.Step("clear variables",
                Make.Action("variable", "clear"))));
        await using var _ = engine;
        await result.IsSuccess();
        await Assert.That(ctx.Variable.Contains("userVar")).IsFalse();
        await Assert.That(ctx.Variable.Contains("Now")).IsTrue();
        await Assert.That(ctx.Variable.Contains("GUID")).IsTrue();
    }
}
