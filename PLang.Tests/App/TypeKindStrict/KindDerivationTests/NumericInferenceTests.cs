using TUnit.Core;
using TUnit.Assertions;

namespace PLang.Tests.App.TypeKindStrict.KindDerivationTests;

// Numeric inference produces {number, kind} everywhere. The Build path
// and the runtime Mint path must agree (the "no build/runtime
// disagreement" convergence). String values produce {text, ...} everywhere too.

public class NumericInferenceTests
{
    [Test] public async Task MintTyped_FromInt_ProducesNumberIntName_NotInt()
    {
        // Runtime mint of an `int` value produces Type.Name == "number" (not "int")
        // and Type.Kind == "int". 
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }

    [Test] public async Task MintTyped_FromDouble_ProducesNumberDoubleKind()
    {
        // Runtime mint of a `double` → {number, double}.
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }

    [Test] public async Task MintTyped_FromDecimal_ProducesNumberDecimalKind()
    {
        // Runtime mint of a `decimal` → {number, decimal}.
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }

    [Test] public async Task DataTypeGetter_StringValue_ReturnsText_NotString()
    {
        // var d = new data.@this("x", "hi"); d.Type.Name == "text" (not "string").
        // The Canonical[typeof(string)] = "text" change is global; the Type getter
        // (which infers from Value's CLR type when no explicit Type is set) reads
        // the canonical name.
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }

    [Test] public async Task BuildStamp_AgreesWithRuntimeMint()
    {
        // The build-time stamp for `set %x% = 5` produces {number, int}; the runtime
        // mint of the same literal-as-CLR-int produces {number, int}. No drift.
        // This is the "no build/runtime disagreement" guard in this design.
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }
}
