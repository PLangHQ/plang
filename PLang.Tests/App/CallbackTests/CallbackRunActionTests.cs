namespace PLang.Tests.App.CallbackTests;

public class CallbackRunActionTests
{
    [Test]
    public async Task CallbackRun_VerifiesSignature_BeforeDispatch()
    {
        // The callback.run handler calls signing.verify before invoking ICallback.Run.
        await Task.CompletedTask;
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task CallbackRun_HardErrors_WhenSigningVerifyFails()
    {
        // signing.verify failure → CallbackSignatureMismatch (or equivalent typed error).
        await Task.CompletedTask;
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task CallbackRun_DispatchesIntoCallbackRun_AndPropagatesData()
    {
        // After verify, handler awaits callback.Value.Run(ctx) and returns its Data.
        await Task.CompletedTask;
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task CallbackRun_OnNonICallbackData_RaisesTypeError()
    {
        // - run %x% where %x% is Data<int> (not Data<ICallback>) → type error at the handler.
        await Task.CompletedTask;
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task CallbackRun_HandlerSignature_TakesDataOfICallback()
    {
        // Pins the Run(Data<ICallback> callback, Context.@this ctx) shape.
        await Task.CompletedTask;
        Assert.Fail("Not implemented");
    }
}
