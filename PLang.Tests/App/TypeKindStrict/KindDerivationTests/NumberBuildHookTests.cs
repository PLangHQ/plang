using TUnit.Core;
using TUnit.Assertions;

namespace PLang.Tests.App.TypeKindStrict.KindDerivationTests;

// number.Build is an existing hook (PLang/app/type/number/this.Build.cs).
// These tests lock its behaviour against regression: text is wired into the
// same dispatcher path; int/long/decimal/double surface only as
// number kinds. The design depends on this hook continuing to do exactly
// what it does today.

public class NumberBuildHookTests
{
    [Test] public async Task Build_IntegerLiteral_ReturnsInt()
    {
        // number.Build("5") → "int". Bare integer in int.Range.
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }

    [Test] public async Task Build_DecimalLiteral_ReturnsDecimal()
    {
        // number.Build("3.14") → "decimal". Contains '.', no scientific notation.
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }

    [Test] public async Task Build_ScientificLiteral_ReturnsDouble()
    {
        // number.Build("1e5") → "double". Scientific notation (or NaN/Infinity).
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }

    [Test] public async Task Build_VarReference_ReturnsNull()
    {
        // number.Build("%var%") → null. Same %var% rule as image.Build and text.Build.
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }
}
