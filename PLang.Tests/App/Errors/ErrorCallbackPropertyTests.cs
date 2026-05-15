using global::app.Callback;
using global::app.Errors;

namespace PLang.Tests.App.Errors;

public class ErrorCallbackPropertyTests
{
    private static global::app.@this NewApp() =>
        new global::app.@this(System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang-errp-" + System.Guid.NewGuid().ToString("N")[..8]));

    [Test]
    public async Task ErrorCallback_Property_TriggersAppSnapshot_OnFirstRead()
    {
        var app = NewApp();
        var err = new global::app.errors.Error("boom");
        using var scope = app.errors.Push(err);

        var data = err.Callback;
        await Assert.That(data).IsNotNull();
        await Assert.That(data.Value).IsNotNull();
        await Assert.That(data.Value!.AppSnapshot.HasSection("Variables")).IsTrue();
    }

    [Test]
    public async Task ErrorCallback_Property_ReturnsDataOfErrorCallback()
    {
        var app = NewApp();
        var err = new global::app.errors.Error("typed");
        using var scope = app.errors.Push(err);

        global::app.data.@this<ErrorCallback> data = err.Callback;
        await Assert.That(data).IsNotNull();
        await Assert.That(data.RawSignature).IsNull();
    }

    [Test]
    public async Task ErrorCallback_Property_ReadTwice_ReturnsSameDataInstance()
    {
        var app = NewApp();
        var err = new global::app.errors.Error("idempotent");
        using var scope = app.errors.Push(err);

        var first = err.Callback;
        var second = err.Callback;
        await Assert.That(first).IsSameReferenceAs(second);
    }

    [Test]
    public async Task ErrorCallback_Property_ReturnsTwoIndependentCalls_ForTwoErrors()
    {
        var app = NewApp();
        var e1 = new global::app.errors.Error("first");
        var e2 = new global::app.errors.Error("second");
        using (app.errors.Push(e1))
        using (app.errors.Push(e2))
        {
            var c1 = e1.Callback;
            var c2 = e2.Callback;
            await Assert.That(c1).IsNotSameReferenceAs(c2);
        }
    }
}
