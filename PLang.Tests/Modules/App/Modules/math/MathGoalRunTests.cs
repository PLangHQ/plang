using app.actor.context;
using app;

namespace PLang.Tests.App.actions.math;

using Number = global::app.type.number.@this;
using NumberKind = global::app.type.number.NumberKind;

/// <summary>
/// math-module behavior through the real path — Make.Goal -> RealGoalLoad.ViaChannel
/// -> RunGoalAsync, asserting on the returned Data a PLang author observes. Replaces the
/// hand-constructed `new Add{...}.Run()` unit style: operands are born-typed from their
/// values exactly as the runtime types them, and dispatch goes through the engine.
/// </summary>
public class MathGoalRunTests
{
    static async Task<(global::app.@this engine, global::app.actor.context.@this ctx, global::app.data.@this result)>
        Run(global::app.goal.@this spec)
    {
        var engine = TestApp.Create("/app");
        var goal = await RealGoalLoad.ViaChannel(engine, spec);
        engine.Goal.Add(goal);
        var ctx = engine.User.Context;
        var result = await engine.RunGoalAsync(goal, ctx);
        return (engine, ctx, result);
    }

    // One math action as the whole goal — the returned Data is the step result.
    static global::app.goal.@this OneAction(global::app.goal.steps.step.actions.action.@this action)
        => Make.Goal("T", Make.Step("compute", action));

    // The numeric value math returns (Data<number>), through the typed door.
    static async Task<Number?> Num(global::app.data.@this result)
        => await result.Value<Number>();

    // --- Binary A/B ops returning an integer result ---

    [Test]
    [Arguments("subtract", 10, 3, 7)]
    [Arguments("multiply", 6, 7, 42)]
    [Arguments("modulo", 10, 3, 1)]
    [Arguments("min", 5, 3, 3)]
    [Arguments("max", 5, 3, 5)]
    public async Task BinaryAB_ReturnsResult(string op, int a, int b, int expected)
    {
        var (engine, _, result) = await Run(OneAction(
            Make.Action("math", op, ("A", a), ("B", b))));
        await using var _e = engine;
        await result.IsSuccess();
        await Assert.That(await Num(result)).IsEqualTo(expected);
    }

    // --- Add — result value AND derived number kind (Int vs Double) ---

    [Test]
    public async Task Add_IntPlusInt_ReturnsInt()
    {
        var (engine, _, result) = await Run(OneAction(
            Make.Action("math", "add", ("A", 3), ("B", 4))));
        await using var _e = engine;
        await result.IsSuccess();
        await Assert.That(await Num(result)).IsEqualTo(7);
        // plang-types Stage 4: math.* returns Data<number>; the underlying kind
        // is Int (not the CLR `int`).
        await Assert.That((await Num(result))!.Kind).IsEqualTo(NumberKind.Int);
    }

    [Test]
    public async Task Add_IntPlusDouble_ReturnsDouble()
    {
        var (engine, _, result) = await Run(OneAction(
            Make.Action("math", "add", ("A", 3), ("B", 4.5))));
        await using var _e = engine;
        await Assert.That(await Num(result)).IsEqualTo(7.5);
        await Assert.That((await Num(result))!.Kind).IsEqualTo(NumberKind.Double);
    }

    // --- Divide ---

    [Test]
    public async Task Divide_ReturnsQuotient()
    {
        var (engine, _, result) = await Run(OneAction(
            Make.Action("math", "divide", ("A", 10.0), ("B", 3.0))));
        await using var _e = engine;
        await result.IsSuccess();
        var value = Convert.ToDouble(await Num(result));
        await Assert.That(Math.Abs(value - 3.333333) < 0.001).IsTrue();
    }

    [Test]
    public async Task Divide_ByZero_Fails()
    {
        var (engine, _, result) = await Run(OneAction(
            Make.Action("math", "divide", ("A", 10), ("B", 0))));
        await using var _e = engine;
        await result.IsFailure();
    }

    // --- Power ---

    [Test]
    public async Task Power_ReturnsResult()
    {
        var (engine, _, result) = await Run(OneAction(
            Make.Action("math", "power", ("Base", 2), ("Exponent", 10))));
        await using var _e = engine;
        await Assert.That(await Num(result)).IsEqualTo(1024);
    }

    // --- Sqrt ---

    [Test]
    public async Task Sqrt_ReturnsSquareRoot()
    {
        var (engine, _, result) = await Run(OneAction(
            Make.Action("math", "sqrt", ("Value", 16))));
        await using var _e = engine;
        await Assert.That(await Num(result)).IsEqualTo(4.0);
    }

    [Test]
    public async Task Sqrt_NegativeInput_Fails()
    {
        var (engine, _, result) = await Run(OneAction(
            Make.Action("math", "sqrt", ("Value", -1))));
        await using var _e = engine;
        await result.IsFailure();
        // Pin the handler-boundary contract: negative input surfaces as
        // ArithmeticError (via number.Sqrt -> Wrap), one canonical key.
        await Assert.That(result.Error?.Key).IsEqualTo("ArithmeticError");
    }

    // --- Abs ---

    [Test]
    public async Task Abs_ReturnsAbsoluteValue()
    {
        var (engine, _, result) = await Run(OneAction(
            Make.Action("math", "abs", ("Value", -42))));
        await using var _e = engine;
        await Assert.That(await Num(result)).IsEqualTo(42);
    }

    // --- Round ---

    [Test]
    public async Task Round_RoundsToDecimals()
    {
        var (engine, _, result) = await Run(OneAction(
            Make.Action("math", "round", ("Value", 3.14159), ("Decimals", 2))));
        await using var _e = engine;
        await Assert.That(await Num(result)).IsEqualTo(3.14);
    }

    // --- Floor / Ceiling ---

    [Test]
    [Arguments("floor", 3.7, 3.0)]
    [Arguments("ceiling", 3.2, 4.0)]
    public async Task FloorCeiling_RoundsToInteger(string op, double input, double expected)
    {
        var (engine, _, result) = await Run(OneAction(
            Make.Action("math", op, ("Value", input))));
        await using var _e = engine;
        await Assert.That(Convert.ToDouble(await Num(result))).IsEqualTo(expected);
    }

    // --- Random ---

    [Test]
    public async Task Random_ReturnsValueInRange()
    {
        var (engine, _, result) = await Run(OneAction(
            Make.Action("math", "random", ("Min", 1), ("Max", 10))));
        await using var _e = engine;
        await result.IsSuccess();
        var value = Convert.ToInt32(await Num(result));
        await Assert.That(value >= 1 && value <= 10).IsTrue();
    }
}
