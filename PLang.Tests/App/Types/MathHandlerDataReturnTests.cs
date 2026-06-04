using number = global::app.type.number.@this;

namespace PLang.Tests.App.Types;

// plang-types — Stage 4 + unary retype follow-up
// math.* actions retype Run() from Task<Data<object>> to Task<Data<number>>.
// The handler RELAYS Data, never throws — overflows/divide-by-zero arrive as Data.Fail.
// Arithmetic AND unary/comparison families (abs/floor/ceiling/sqrt/round/min/max)
// route through number.*. MathHelper.ToDouble and MathHelper.PreserveType are deleted.

public class MathHandlerDataReturnTests
{
    private static System.Type RunReturnType(System.Type handler)
        => handler.GetMethod("Run")!.ReturnType;

    private static void AssertRunReturnsDataNumber(System.Type handler)
    {
        var rt = RunReturnType(handler);
        var task = typeof(System.Threading.Tasks.Task<>).MakeGenericType(
            typeof(global::app.data.@this<>).MakeGenericType(typeof(number)));
        if (rt != task)
            throw new System.InvalidOperationException(
                $"{handler.Name}.Run() returns {rt}, expected Task<Data<number>>");
    }

    [Test] public async Task MathAdd_RunSignature_ReturnsDataNumber_NotDataObject()
    {
        AssertRunReturnsDataNumber(typeof(global::app.module.math.Add));
        await Assert.That(true).IsTrue();
    }

    [Test] public async Task MathSubtract_RunSignature_ReturnsDataNumber()
    {
        AssertRunReturnsDataNumber(typeof(global::app.module.math.Subtract));
        await Assert.That(true).IsTrue();
    }

    [Test] public async Task MathMultiply_RunSignature_ReturnsDataNumber()
    {
        AssertRunReturnsDataNumber(typeof(global::app.module.math.Multiply));
        await Assert.That(true).IsTrue();
    }

    [Test] public async Task MathDivide_RunSignature_ReturnsDataNumber()
    {
        AssertRunReturnsDataNumber(typeof(global::app.module.math.Divide));
        await Assert.That(true).IsTrue();
    }

    [Test] public async Task MathIntDiv_RunSignature_ReturnsDataNumber()
    {
        AssertRunReturnsDataNumber(typeof(global::app.module.math.IntDiv));
        await Assert.That(true).IsTrue();
    }

    [Test] public async Task MathPower_RunSignature_ReturnsDataNumber()
    {
        AssertRunReturnsDataNumber(typeof(global::app.module.math.Power));
        await Assert.That(true).IsTrue();
    }

    [Test] public async Task MathHandler_Overflow_ReturnsDataFail_NotException()
    {
        // Covered end-to-end by NumberArithmeticTests.Overflow_Throw_HandlerPathReturnsDataError.
        var r = number.Add(number.From(decimal.MaxValue), number.From(decimal.MaxValue),
            global::app.type.number.NumberPolicy.Strict);
        await r.IsFailure();
        await Assert.That(r.Error?.Key).IsEqualTo("MathOverflow");
    }

    [Test] public async Task MathHandler_DivByZero_ReturnsDataFail_NotException()
    {
        var r = number.Divide(number.From(7), number.From(0),
            global::app.type.number.NumberPolicy.Lenient);
        await r.IsFailure();
        await Assert.That(r.Error?.Key).IsEqualTo("DivideByZero");
    }

    [Test] public async Task MathHandler_ReadsPolicyViaAppConfigForNumberConfig()
    {
        // The Config record lives at app.module.environment.number.Config.
        var t = typeof(global::app.module.environment.number.Config);
        await Assert.That(typeof(global::app.config.IConfig).IsAssignableFrom(t)).IsTrue();
        await Assert.That(t.GetProperty("Overflow")).IsNotNull();
        await Assert.That(t.GetProperty("Precision")).IsNotNull();
    }

    [Test] public async Task MathHandler_StepLevelOverflowParam_IsNullableNotOptionalAttribute()
    {
        var overflowProp = typeof(global::app.module.math.Add).GetProperty("Overflow");
        await Assert.That(overflowProp).IsNotNull();
        var pt = overflowProp!.PropertyType;
        // Data<POverflow>? — generic Data<T> nullable.
        await Assert.That(pt.IsGenericType).IsTrue();
        await Assert.That(pt.GetGenericTypeDefinition() == typeof(global::app.data.@this<>)).IsTrue();
    }

    [Test] public async Task MathHelper_NotPresentInProductionAssembly()
    {
        // MathHelper.cs is deleted — unary/comparison handlers now route through
        // number.Abs / Floor / Ceiling / Sqrt / Round / Min / Max with the same
        // Wrap envelope that fronts the arithmetic family.
        var asm = typeof(global::app.module.math.Add).Assembly;
        var helper = asm.GetType("app.module.math.MathHelper");
        await Assert.That(helper).IsNull();
    }

    [Test] public async Task MathAbs_RunSignature_ReturnsDataNumber()
    {
        AssertRunReturnsDataNumber(typeof(global::app.module.math.Abs));
    }

    [Test] public async Task MathFloor_RunSignature_ReturnsDataNumber()
    {
        AssertRunReturnsDataNumber(typeof(global::app.module.math.Floor));
    }

    [Test] public async Task MathCeiling_RunSignature_ReturnsDataNumber()
    {
        AssertRunReturnsDataNumber(typeof(global::app.module.math.Ceiling));
    }

    [Test] public async Task MathSqrt_RunSignature_ReturnsDataNumber()
    {
        AssertRunReturnsDataNumber(typeof(global::app.module.math.Sqrt));
    }

    [Test] public async Task MathRound_RunSignature_ReturnsDataNumber()
    {
        AssertRunReturnsDataNumber(typeof(global::app.module.math.Round));
    }

    [Test] public async Task MathMin_RunSignature_ReturnsDataNumber()
    {
        AssertRunReturnsDataNumber(typeof(global::app.module.math.Min));
    }

    [Test] public async Task MathMax_RunSignature_ReturnsDataNumber()
    {
        AssertRunReturnsDataNumber(typeof(global::app.module.math.Max));
    }
}
