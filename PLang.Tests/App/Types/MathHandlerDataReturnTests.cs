namespace PLang.Tests.App.Types;

// plang-types — Stage 4
// math.* actions retype Run() from Task<Data<object>> to Task<Data<number>>.
// The handler RELAYS Data, never throws — overflows/divide-by-zero arrive as Data.Fail.
// MathHelper.ToDouble and MathHelper.PreserveType are deleted; no references remain.

public class MathHandlerDataReturnTests
{
    [Test] public async Task MathAdd_RunSignature_ReturnsDataNumber_NotDataObject()
        => throw new global::System.NotImplementedException();

    [Test] public async Task MathSubtract_RunSignature_ReturnsDataNumber()
        => throw new global::System.NotImplementedException();

    [Test] public async Task MathMultiply_RunSignature_ReturnsDataNumber()
        => throw new global::System.NotImplementedException();

    [Test] public async Task MathDivide_RunSignature_ReturnsDataNumber()
        => throw new global::System.NotImplementedException();

    [Test] public async Task MathIntDiv_RunSignature_ReturnsDataNumber()
        => throw new global::System.NotImplementedException();

    [Test] public async Task MathPower_RunSignature_ReturnsDataNumber()
        => throw new global::System.NotImplementedException();

    [Test] public async Task MathHandler_Overflow_ReturnsDataFail_NotException()
        => throw new global::System.NotImplementedException();

    [Test] public async Task MathHandler_DivByZero_ReturnsDataFail_NotException()
        => throw new global::System.NotImplementedException();

    [Test] public async Task MathHandler_ReadsPolicyViaAppConfigForNumberConfig()
        => throw new global::System.NotImplementedException();

    [Test] public async Task MathHandler_StepLevelOverflowParam_IsNullableNotOptionalAttribute()
        => throw new global::System.NotImplementedException();

    [Test] public async Task MathHelper_ToDouble_NotPresentInProductionAssembly()
        => throw new global::System.NotImplementedException();

    [Test] public async Task MathHelper_PreserveType_NotPresentInProductionAssembly()
        => throw new global::System.NotImplementedException();
}
