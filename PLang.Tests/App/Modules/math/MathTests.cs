using app.actor.context;
using app;
using app.variable;
using app.modules.math;

namespace PLang.Tests.App.actions.math;

public class MathTests
{
    private (global::app.actor.context.@this context, Variables memory) CreateContext()
    {
        var app = new global::app.@this("/app");
        return (app.User.Context, app.User.Context.Variables);
    }

    // --- Add ---

    [Test]
    public async Task Add_IntPlusInt_ReturnsInt()
    {
        var (context, _) = CreateContext();

        var action = new Add { Context = context, A = new global::app.data.@this("", 3), B = new global::app.data.@this("", 4)};
        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Value).IsEqualTo(7);
        // plang-types Stage 4: math.* returns Data<number>; the underlying kind
        // is Int (not the CLR `int`).
        await Assert.That(result.Value!.Kind).IsEqualTo(global::app.type.number.NumberKind.Int);
    }

    [Test]
    public async Task Add_IntPlusDouble_ReturnsDouble()
    {
        var (context, _) = CreateContext();

        var action = new Add { Context = context, A = new global::app.data.@this("", 3), B = new global::app.data.@this("", 4.5)};
        var result = await action.Run();

        await Assert.That(result.Value).IsEqualTo(7.5);
        await Assert.That(result.Value!.Kind).IsEqualTo(global::app.type.number.NumberKind.Double);
    }

    // --- Subtract ---

    [Test]
    public async Task Subtract_ReturnsCorrectResult()
    {
        var (context, _) = CreateContext();

        var action = new Subtract { Context = context, A = new global::app.data.@this("", 10), B = new global::app.data.@this("", 3)};
        var result = await action.Run();

        await Assert.That(result.Value).IsEqualTo(7);
    }

    // --- Multiply ---

    [Test]
    public async Task Multiply_ReturnsProduct()
    {
        var (context, _) = CreateContext();

        var action = new Multiply { Context = context, A = new global::app.data.@this("", 6), B = new global::app.data.@this("", 7)};
        var result = await action.Run();

        await Assert.That(result.Value).IsEqualTo(42);
    }

    // --- Divide ---

    [Test]
    public async Task Divide_ReturnsQuotient()
    {
        var (context, _) = CreateContext();

        var action = new Divide { Context = context, A = new global::app.data.@this("", 10.0), B = new global::app.data.@this("", 3.0)};
        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        var value = Convert.ToDouble(result.Value);
        await Assert.That(Math.Abs(value - 3.333333) < 0.001).IsTrue();
    }

    [Test]
    public async Task Divide_ByZero_Fails()
    {
        var (context, _) = CreateContext();

        var action = new Divide { Context = context, A = new global::app.data.@this("", 10), B = new global::app.data.@this("", 0)};
        var result = await action.Run();

        await Assert.That(result.Success).IsFalse();
    }

    // --- Modulo ---

    [Test]
    public async Task Modulo_ReturnsRemainder()
    {
        var (context, _) = CreateContext();

        var action = new Modulo { Context = context, A = new global::app.data.@this("", 10), B = new global::app.data.@this("", 3)};
        var result = await action.Run();

        await Assert.That(result.Value).IsEqualTo(1);
    }

    // --- Power ---

    [Test]
    public async Task Power_ReturnsResult()
    {
        var (context, _) = CreateContext();

        var action = new Power { Context = context, Base = new global::app.data.@this("", 2), Exponent = new global::app.data.@this("", 10)};
        var result = await action.Run();

        await Assert.That(result.Value).IsEqualTo(1024);
    }

    // --- Sqrt ---

    [Test]
    public async Task Sqrt_ReturnsSquareRoot()
    {
        var (context, _) = CreateContext();

        var action = new Sqrt { Context = context, Value = new global::app.data.@this("", 16)};
        var result = await action.Run();

        await Assert.That(result.Value).IsEqualTo(4.0);
    }

    [Test]
    public async Task Sqrt_NegativeInput_Fails()
    {
        var (context, _) = CreateContext();

        var action = new Sqrt { Context = context, Value = new global::app.data.@this("", -1)};
        var result = await action.Run();

        await Assert.That(result.Success).IsFalse();
        // Pin the handler-boundary contract: negative input surfaces as
        // ArithmeticError (via number.Sqrt → Wrap), one canonical key.
        await Assert.That(result.Error?.Key).IsEqualTo("ArithmeticError");
    }

    // --- Abs ---

    [Test]
    public async Task Abs_ReturnsAbsoluteValue()
    {
        var (context, _) = CreateContext();

        var action = new Abs { Context = context, Value = new global::app.data.@this("", -42)};
        var result = await action.Run();

        await Assert.That(result.Value).IsEqualTo(42);
    }

    // --- Round ---

    [Test]
    public async Task Round_RoundsToDecimals()
    {
        var (context, _) = CreateContext();

        var action = new Round { Context = context, Value = new global::app.data.@this("", 3.14159), Decimals = 2 };
        var result = await action.Run();

        await Assert.That(result.Value).IsEqualTo(3.14);
    }

    // --- Floor / Ceiling ---

    [Test]
    public async Task Floor_RoundsDown()
    {
        var (context, _) = CreateContext();

        var action = new Floor { Context = context, Value = new global::app.data.@this("", 3.7)};
        var result = await action.Run();

        await Assert.That(Convert.ToDouble(result.Value)).IsEqualTo(3.0);
    }

    [Test]
    public async Task Ceiling_RoundsUp()
    {
        var (context, _) = CreateContext();

        var action = new Ceiling { Context = context, Value = new global::app.data.@this("", 3.2)};
        var result = await action.Run();

        await Assert.That(Convert.ToDouble(result.Value)).IsEqualTo(4.0);
    }

    // --- Min / Max ---

    [Test]
    public async Task Min_ReturnsSmaller()
    {
        var (context, _) = CreateContext();

        var action = new Min { Context = context, A = new global::app.data.@this("", 5), B = new global::app.data.@this("", 3)};
        var result = await action.Run();

        await Assert.That(result.Value).IsEqualTo(3);
    }

    [Test]
    public async Task Max_ReturnsLarger()
    {
        var (context, _) = CreateContext();

        var action = new Max { Context = context, A = new global::app.data.@this("", 5), B = new global::app.data.@this("", 3)};
        var result = await action.Run();

        await Assert.That(result.Value).IsEqualTo(5);
    }

    // --- Random ---

    [Test]
    public async Task Random_ReturnsValueInRange()
    {
        var (context, _) = CreateContext();

        var action = new global::app.modules.math.Random { Context = context, Min = 1, Max = 10 };
        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        var value = Convert.ToInt32(result.Value);
        await Assert.That(value >= 1 && value <= 10).IsTrue();
    }
}
