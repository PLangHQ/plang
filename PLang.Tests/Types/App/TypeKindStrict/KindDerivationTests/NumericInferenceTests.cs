using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace PLang.Tests.App.TypeKindStrict.KindDerivationTests;

public class NumericInferenceTests : System.IAsyncDisposable
{
    private readonly global::app.@this _app = global::PLang.Tests.TestApp.Create("/tmp/numinfer-" + System.Guid.NewGuid().ToString("N")[..6]);
    public async System.Threading.Tasks.ValueTask DisposeAsync() => await _app.DisposeAsync();

    [Test] public async Task MintTyped_FromInt_ProducesNumberIntName_NotInt()
    {
        var d = new global::app.data.@this("x", 42, context: _app.User.Context);
        await Assert.That(d.Type.Name).IsEqualTo("number");
        await Assert.That(d.Type.Kind?.Name).IsEqualTo("int");
    }

    [Test] public async Task MintTyped_FromDouble_ProducesNumberDoubleKind()
    {
        var d = new global::app.data.@this("x", 3.14, context: _app.User.Context);
        await Assert.That(d.Type.Name).IsEqualTo("number");
        await Assert.That(d.Type.Kind?.Name).IsEqualTo("double");
    }

    [Test] public async Task MintTyped_FromDecimal_ProducesNumberDecimalKind()
    {
        var d = new global::app.data.@this("x", 3.14m, context: _app.User.Context);
        await Assert.That(d.Type.Name).IsEqualTo("number");
        await Assert.That(d.Type.Kind?.Name).IsEqualTo("decimal");
    }

    [Test] public async Task DataTypeGetter_StringValue_ReturnsText_NotString()
    {
        var d = new global::app.data.@this("x", "hi", context: _app.User.Context);
        await Assert.That(d.Type.Name).IsEqualTo("text");
    }

    [Test] public async Task BuildStamp_AgreesWithRuntimeMint()
    {
        // Both paths converge on {number, int} for an int literal.
        var runtime = new global::app.data.@this("x", 5, context: _app.User.Context);
        var buildKind = global::app.type.number.@this.Build("5");
        await Assert.That(runtime.Type.Name).IsEqualTo("number");
        await Assert.That(runtime.Type.Kind?.Name).IsEqualTo("int");
        await Assert.That(buildKind).IsEqualTo("int");
    }
}
