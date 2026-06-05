namespace PLang.Tests.App.ScalarsAsNative;

// The load-bearing seam: every scalar value is born as its wrapper, not as a
// raw CLR value. UnwrapJsonElement is the canonical entry — every JsonValueKind
// produces a wrapper (or Data.Null() for the null case). Wire / variable.set /
// CLI / action results follow the same shape. UnwrapNewtonsoftToken is deleted
// (dead v1 shim; Newtonsoft is not a dependency).
public class ConstructionBornNativeTests
{
    [Test]
    public async Task UnwrapJsonElement_String_ProducesTextWrapper()
    {
        // JsonValueKind.String → text.@this("..."), not raw GetString().
        await Task.CompletedTask;
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task UnwrapJsonElement_Number_ProducesNumberWrapper()
    {
        // JsonValueKind.Number → number.@this, not raw long/decimal/double.
        await Task.CompletedTask;
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task UnwrapJsonElement_TrueFalse_ProducesBoolWrapper()
    {
        // JsonValueKind.True/False → bool.@this(true|false), not raw bool.
        await Task.CompletedTask;
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task UnwrapJsonElement_Null_ProducesDataNullSingleton()
    {
        // JsonValueKind.Null/Undefined → Data.Null() carrying the null.@this
        // singleton — NOT a bare C# null value, NOT a fresh allocation.
        await Task.CompletedTask;
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task NoRawScalarEscapes_ParseSeamSweep()
    {
        // A grep-style absence test: round-trip a json document with every scalar
        // kind through UnwrapJsonElement and confirm no leaf value is a raw CLR
        // scalar (string, int/long/decimal/double, bool) — only wrappers and
        // collections holding Data.
        await Task.CompletedTask;
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task UnwrapNewtonsoftToken_IsDeleted()
    {
        // The dead v1 shim is gone. Reflection probe: typeof(Data).GetMethod
        // ("UnwrapNewtonsoftToken", BindingFlags.NonPublic|...) is null. Newtonsoft
        // is not a dependency; nothing live feeds JTokens.
        await Task.CompletedTask;
        Assert.Fail("Not implemented");
    }
}
