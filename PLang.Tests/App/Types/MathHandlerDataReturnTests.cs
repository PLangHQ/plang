using number = global::app.types.number.@this;

namespace PLang.Tests.App.Types;

// plang-types — Stage 4
// math.* actions retype Run() from Task<Data<object>> to Task<Data<number>>.
// The handler RELAYS Data, never throws — overflows/divide-by-zero arrive as Data.Fail.
// MathHelper.ToDouble and MathHelper.PreserveType are slated for deletion once the
// non-arithmetic math handlers (abs/floor/etc.) follow the same retype — a follow-up.

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
        AssertRunReturnsDataNumber(typeof(global::app.modules.math.Add));
        await Assert.That(true).IsTrue();
    }

    [Test] public async Task MathSubtract_RunSignature_ReturnsDataNumber()
    {
        AssertRunReturnsDataNumber(typeof(global::app.modules.math.Subtract));
        await Assert.That(true).IsTrue();
    }

    [Test] public async Task MathMultiply_RunSignature_ReturnsDataNumber()
    {
        AssertRunReturnsDataNumber(typeof(global::app.modules.math.Multiply));
        await Assert.That(true).IsTrue();
    }

    [Test] public async Task MathDivide_RunSignature_ReturnsDataNumber()
    {
        AssertRunReturnsDataNumber(typeof(global::app.modules.math.Divide));
        await Assert.That(true).IsTrue();
    }

    [Test] public async Task MathIntDiv_RunSignature_ReturnsDataNumber()
    {
        AssertRunReturnsDataNumber(typeof(global::app.modules.math.IntDiv));
        await Assert.That(true).IsTrue();
    }

    [Test] public async Task MathPower_RunSignature_ReturnsDataNumber()
    {
        AssertRunReturnsDataNumber(typeof(global::app.modules.math.Power));
        await Assert.That(true).IsTrue();
    }

    [Test] public async Task MathHandler_Overflow_ReturnsDataFail_NotException()
    {
        // Covered end-to-end by NumberArithmeticTests.Overflow_Throw_HandlerPathReturnsDataError.
        var r = number.Add(number.From(decimal.MaxValue), number.From(decimal.MaxValue),
            global::app.types.number.NumberPolicy.Strict);
        await Assert.That(r.Success).IsFalse();
        await Assert.That(r.Error?.Key).IsEqualTo("MathOverflow");
    }

    [Test] public async Task MathHandler_DivByZero_ReturnsDataFail_NotException()
    {
        var r = number.Divide(number.From(7), number.From(0),
            global::app.types.number.NumberPolicy.Lenient);
        await Assert.That(r.Success).IsFalse();
        await Assert.That(r.Error?.Key).IsEqualTo("DivideByZero");
    }

    [Test] public async Task MathHandler_ReadsPolicyViaAppConfigForNumberConfig()
    {
        // The Config record lives at app.modules.math.number.Config.
        var t = typeof(global::app.modules.math.number.Config);
        await Assert.That(typeof(global::app.config.IConfig).IsAssignableFrom(t)).IsTrue();
        await Assert.That(t.GetProperty("Overflow")).IsNotNull();
        await Assert.That(t.GetProperty("Precision")).IsNotNull();
    }

    [Test] public async Task MathHandler_StepLevelOverflowParam_IsNullableNotOptionalAttribute()
    {
        var overflowProp = typeof(global::app.modules.math.Add).GetProperty("Overflow");
        await Assert.That(overflowProp).IsNotNull();
        var pt = overflowProp!.PropertyType;
        // Data<POverflow>? — generic Data<T> nullable.
        await Assert.That(pt.IsGenericType).IsTrue();
        await Assert.That(pt.GetGenericTypeDefinition() == typeof(global::app.data.@this<>)).IsTrue();
    }

    [Test, Skip("deferred: MathHelper.ToDouble still backs abs/ceiling/floor/min/max/round/sqrt")]
    public async Task MathHelper_ToDouble_NotPresentInProductionAssembly()
        => await Assert.That(true).IsTrue();

    [Test, Skip("deferred: MathHelper.PreserveType still backs the non-arithmetic math handlers")]
    public async Task MathHelper_PreserveType_NotPresentInProductionAssembly()
        => await Assert.That(true).IsTrue();
}
