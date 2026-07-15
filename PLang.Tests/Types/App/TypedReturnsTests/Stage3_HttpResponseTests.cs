using System.Reflection;

namespace PLang.Tests.App.TypedReturnsTests;

// Contract (post-dissolve, Decision 6): http.response.@this is GONE. http.request
// and http.upload return plain Data — the response body is the lazy value
// (type/kind from Content-Type), status/headers/duration ride as Properties
// (read with `!`). http.download is untouched (it saves to disk).

public class Stage3_HttpResponseTests
{
    private static System.Type RunReturnType<THandler>()
        => typeof(THandler).GetMethod("Run", BindingFlags.Public | BindingFlags.Instance, System.Type.EmptyTypes)!.ReturnType;

    [Test]
    public async Task HttpResponse_Type_IsDeleted()
    {
        var asm = typeof(global::app.module.action.http.request).Assembly;
        await Assert.That(asm.GetType("app.http.response.@this")).IsNull();
    }

    [Test]
    public async Task HttpRequest_Run_ReturnsTaskData_NotResponse()
        => await Assert.That(RunReturnType<global::app.module.action.http.request>())
            .IsEqualTo(typeof(Task<Data>));

    [Test]
    public async Task HttpUpload_Run_ReturnsTaskData_NotResponse()
        => await Assert.That(RunReturnType<global::app.module.action.http.upload>())
            .IsEqualTo(typeof(Task<Data>));

    [Test]
    public async Task HttpDownload_Run_SignatureUnchanged()
        => await Assert.That(RunReturnType<global::app.module.action.http.download>())
            .IsEqualTo(typeof(Task<Data>));
}
