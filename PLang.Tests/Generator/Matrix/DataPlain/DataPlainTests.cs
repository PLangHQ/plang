namespace PLang.Tests.Generator.Matrix.DataPlain;

// Matrix entry for plain Data (== Data<object>) — universal type, no T constraint.
// v4 contract: As<object> walks %var% references and substitutes them, but does no further typed conversion.

public class DataPlainTests
{
    // Data property accepts a string Value and surfaces it as object (no conversion).
    [Test] public async Task DataPlain_StringValue_PassesThrough() => Assert.Fail("Not implemented");

    // Data property accepts an int Value and surfaces it as object (boxed).
    [Test] public async Task DataPlain_IntValue_PassesThrough() => Assert.Fail("Not implemented");

    // Data property accepts a list Value and surfaces it as object (still List<object?> shape).
    [Test] public async Task DataPlain_ListValue_PassesThrough() => Assert.Fail("Not implemented");

    // Data property accepts a dict Value and surfaces it as object (still Dictionary shape).
    [Test] public async Task DataPlain_DictValue_PassesThrough() => Assert.Fail("Not implemented");

    // %var% in Data property resolves through As<object> — substitution happens, returns the variable's Value.
    [Test] public async Task DataPlain_VarReference_ResolvesAsObject() => Assert.Fail("Not implemented");
}
