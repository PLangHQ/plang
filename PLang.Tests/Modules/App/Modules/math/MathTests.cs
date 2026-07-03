using app.actor.context;
using app;
using app.variable;
using app.module.math;

namespace PLang.Tests.App.actions.math;

public class MathTests
{
    private (global::app.actor.context.@this context, Variables memory) CreateContext()
    {
        var app = TestApp.Create("/app");
        return (app.User.Context, app.User.Context.Variable);
    }

    // --- Add ---

    [Test]
    public async Task Add_IntPlusInt_ReturnsInt()
    {
        var (context, _) = CreateContext();

        var action = new Add(context) { A = new global::app.data.@this("", 3, context: context), B = new global::app.data.@this("", 4, context: context)};
        await action.Attach(null, context);
        var result = await action.Run();

        await result.IsSuccess();
        await Assert.That((await result.Value())).IsEqualTo(7);
        // plang-types Stage 4: math.* returns Data<number>; the underlying kind
        // is Int (not the CLR `int`).
        await Assert.That((await result.Value())!.Kind).IsEqualTo(global::app.type.number.NumberKind.Int);
    }

    [Test]
    public async Task Add_IntPlusDouble_ReturnsDouble()
    {
        var (context, _) = CreateContext();

        var action = new Add(context) { A = new global::app.data.@this("", 3, context: context), B = new global::app.data.@this("", 4.5, context: context)};
        await action.Attach(null, context);
        var result = await action.Run();

        await Assert.That((await result.Value())).IsEqualTo(7.5);
        await Assert.That((await result.Value())!.Kind).IsEqualTo(global::app.type.number.NumberKind.Double);
    }

    // --- Subtract ---

    [Test]
    public async Task Subtract_ReturnsCorrectResult()
    {
        var (context, _) = CreateContext();

        var action = new Subtract(context) { A = new global::app.data.@this("", 10, context: context), B = new global::app.data.@this("", 3, context: context)};
        await action.Attach(null, context);
        var result = await action.Run();

        await Assert.That((await result.Value())).IsEqualTo(7);
    }

    // --- Multiply ---

    [Test]
    public async Task Multiply_ReturnsProduct()
    {
        var (context, _) = CreateContext();

        var action = new Multiply(context) { A = new global::app.data.@this("", 6, context: context), B = new global::app.data.@this("", 7, context: context)};
        await action.Attach(null, context);
        var result = await action.Run();

        await Assert.That((await result.Value())).IsEqualTo(42);
    }

    // --- Divide ---

    [Test]
    public async Task Divide_ReturnsQuotient()
    {
        var (context, _) = CreateContext();

        var action = new Divide(context) { A = new global::app.data.@this("", 10.0, context: context), B = new global::app.data.@this("", 3.0, context: context)};
        await action.Attach(null, context);
        var result = await action.Run();

        await result.IsSuccess();
        var value = Convert.ToDouble((await result.Value()));
        await Assert.That(Math.Abs(value - 3.333333) < 0.001).IsTrue();
    }

    [Test]
    public async Task Divide_ByZero_Fails()
    {
        var (context, _) = CreateContext();

        var action = new Divide(context) { A = new global::app.data.@this("", 10, context: context), B = new global::app.data.@this("", 0, context: context)};
        await action.Attach(null, context);
        var result = await action.Run();

        await result.IsFailure();
    }

    // --- Modulo ---

    [Test]
    public async Task Modulo_ReturnsRemainder()
    {
        var (context, _) = CreateContext();

        var action = new Modulo(context) { A = new global::app.data.@this("", 10, context: context), B = new global::app.data.@this("", 3, context: context)};
        await action.Attach(null, context);
        var result = await action.Run();

        await Assert.That((await result.Value())).IsEqualTo(1);
    }

    // --- Power ---

    [Test]
    public async Task Power_ReturnsResult()
    {
        var (context, _) = CreateContext();

        var action = new Power(context) { Base = new global::app.data.@this("", 2, context: context), Exponent = new global::app.data.@this("", 10, context: context)};
        await action.Attach(null, context);
        var result = await action.Run();

        await Assert.That((await result.Value())).IsEqualTo(1024);
    }

    // --- Sqrt ---

    [Test]
    public async Task Sqrt_ReturnsSquareRoot()
    {
        var (context, _) = CreateContext();

        var action = new Sqrt(context) { Value = new global::app.data.@this("", 16, context: context)};
        await action.Attach(null, context);
        var result = await action.Run();

        await Assert.That((await result.Value())).IsEqualTo(4.0);
    }

    [Test]
    public async Task Sqrt_NegativeInput_Fails()
    {
        var (context, _) = CreateContext();

        var action = new Sqrt(context) { Value = new global::app.data.@this("", -1, context: context)};
        await action.Attach(null, context);
        var result = await action.Run();

        await result.IsFailure();
        // Pin the handler-boundary contract: negative input surfaces as
        // ArithmeticError (via number.Sqrt → Wrap), one canonical key.
        await Assert.That(result.Error?.Key).IsEqualTo("ArithmeticError");
    }

    // --- Abs ---

    [Test]
    public async Task Abs_ReturnsAbsoluteValue()
    {
        var (context, _) = CreateContext();

        var action = new Abs(context) { Value = new global::app.data.@this("", -42, context: context)};
        await action.Attach(null, context);
        var result = await action.Run();

        await Assert.That((await result.Value())).IsEqualTo(42);
    }

    // --- Round ---

    [Test]
    public async Task Round_RoundsToDecimals()
    {
        var (context, _) = CreateContext();

        var action = new Round(context) { Value = new global::app.data.@this("", 3.14159, context: context), Decimals = (global::app.type.number.@this)2 };
        await action.Attach(null, context);
        var result = await action.Run();

        await Assert.That((await result.Value())).IsEqualTo(3.14);
    }

    // --- Floor / Ceiling ---

    [Test]
    public async Task Floor_RoundsDown()
    {
        var (context, _) = CreateContext();

        var action = new Floor(context) { Value = new global::app.data.@this("", 3.7, context: context)};
        await action.Attach(null, context);
        var result = await action.Run();

        await Assert.That(Convert.ToDouble((await result.Value()))).IsEqualTo(3.0);
    }

    [Test]
    public async Task Ceiling_RoundsUp()
    {
        var (context, _) = CreateContext();

        var action = new Ceiling(context) { Value = new global::app.data.@this("", 3.2, context: context)};
        await action.Attach(null, context);
        var result = await action.Run();

        await Assert.That(Convert.ToDouble((await result.Value()))).IsEqualTo(4.0);
    }

    // --- Min / Max ---

    [Test]
    public async Task Min_ReturnsSmaller()
    {
        var (context, _) = CreateContext();

        var action = new Min(context) { A = new global::app.data.@this("", 5, context: context), B = new global::app.data.@this("", 3, context: context)};
        await action.Attach(null, context);
        var result = await action.Run();

        await Assert.That((await result.Value())).IsEqualTo(3);
    }

    [Test]
    public async Task Max_ReturnsLarger()
    {
        var (context, _) = CreateContext();

        var action = new Max(context) { A = new global::app.data.@this("", 5, context: context), B = new global::app.data.@this("", 3, context: context)};
        await action.Attach(null, context);
        var result = await action.Run();

        await Assert.That((await result.Value())).IsEqualTo(5);
    }

    // --- Random ---

    [Test]
    public async Task Random_ReturnsValueInRange()
    {
        var (context, _) = CreateContext();

        var action = new global::app.module.math.Random(context) { Min = (global::app.type.number.@this)1, Max = (global::app.type.number.@this)10 };
        await action.Attach(null, context);
        var result = await action.Run();

        await result.IsSuccess();
        var value = Convert.ToInt32((await result.Value()));
        await Assert.That(value >= 1 && value <= 10).IsTrue();
    }
}
