using global::App.Callback;
using global::App.Errors;

namespace PLang.Tests.App.Errors;

public class ErrorCallbackPropertyTests
{
    private static global::App.@this NewApp() =>
        new global::App.@this(System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang-errp-" + System.Guid.NewGuid().ToString("N")[..8]));

    [Test]
    public async Task ErrorCallback_Property_TriggersAppSnapshot_OnFirstRead()
    {
        var app = NewApp();
        var err = new global::App.Errors.Error("boom");
        using var scope = app.Errors.Push(err);

        var data = err.Callback;
        await Assert.That(data).IsNotNull();
        await Assert.That(data.Value).IsNotNull();
        await Assert.That(data.Value!.AppSnapshot.HasSection("Variables")).IsTrue();
    }

    [Test]
    public async Task ErrorCallback_Property_ReturnsDataOfErrorCallback()
    {
        var app = NewApp();
        var err = new global::App.Errors.Error("typed");
        using var scope = app.Errors.Push(err);

        global::App.Data.@this<ErrorCallback> data = err.Callback;
        await Assert.That(data).IsNotNull();
        await Assert.That(data.RawSignature).IsNull();
    }

    [Test]
    public async Task ErrorCallback_Property_ReadTwice_ReturnsSameDataInstance()
    {
        var app = NewApp();
        var err = new global::App.Errors.Error("idempotent");
        using var scope = app.Errors.Push(err);

        var first = err.Callback;
        var second = err.Callback;
        await Assert.That(first).IsSameReferenceAs(second);
    }

    [Test]
    public async Task ErrorCallback_Property_ReturnsTwoIndependentCalls_ForTwoErrors()
    {
        var app = NewApp();
        var e1 = new global::App.Errors.Error("first");
        var e2 = new global::App.Errors.Error("second");
        using (app.Errors.Push(e1))
        using (app.Errors.Push(e2))
        {
            var c1 = e1.Callback;
            var c2 = e2.Callback;
            await Assert.That(c1).IsNotSameReferenceAs(c2);
        }
    }
}
