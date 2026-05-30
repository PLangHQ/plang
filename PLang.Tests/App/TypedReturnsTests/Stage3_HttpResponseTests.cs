using System.Reflection;
using Response = global::app.http.response.@this;

namespace PLang.Tests.App.TypedReturnsTests;

// Contract: http.request and http.upload return Data<Response> where Response
// is a sealed record carrying (Status:int, Headers, Body:object?, Duration).
// http.download is untouched (it saves to disk).

public class Stage3_HttpResponseTests
{
    private static System.Type RunReturnType<THandler>()
        => typeof(THandler).GetMethod("Run", BindingFlags.Public | BindingFlags.Instance, System.Type.EmptyTypes)!.ReturnType;

    [Test]
    public async Task HttpResponse_RecordExistsAt_AppHttpResponseThis()
    {
        var t = typeof(Response);
        await Assert.That(t.Namespace).IsEqualTo("app.http.response");
        await Assert.That(t.Name).IsEqualTo("this");
    }

    [Test]
    public async Task HttpResponse_CarriesAllFourFields()
    {
        var props = typeof(Response).GetProperties().Select(p => p.Name).ToHashSet();
        await Assert.That(props).Contains("Status");
        await Assert.That(props).Contains("Headers");
        await Assert.That(props).Contains("Body");
        await Assert.That(props).Contains("Duration");

        var statusProp = typeof(Response).GetProperty("Status")!;
        await Assert.That(statusProp.PropertyType).IsEqualTo(typeof(int));
        var durationProp = typeof(Response).GetProperty("Duration")!;
        await Assert.That(durationProp.PropertyType).IsEqualTo(typeof(System.TimeSpan));
    }

    [Test]
    public async Task HttpResponse_IsSealedRecord()
    {
        var t = typeof(Response);
        await Assert.That(t.IsSealed).IsTrue();
        // Records emit a synthesized <Clone>$ method; presence confirms record semantics.
        var clone = t.GetMethod("<Clone>$", BindingFlags.Public | BindingFlags.Instance);
        await Assert.That(clone).IsNotNull().Because("Response must be a record (compiler-emitted clone present).");

        var headers = new Dictionary<string, string> { ["X"] = "y" };
        var a = new Response(200, headers, "body", System.TimeSpan.FromSeconds(1));
        var b = new Response(200, headers, "body", System.TimeSpan.FromSeconds(1));
        await Assert.That(a).IsEqualTo(b).Because("Record equality on identical field values.");
    }

    [Test]
    public async Task HttpRequest_Run_ReturnsTaskDataOfResponse()
    {
        var ret = RunReturnType<global::app.module.http.request>();
        var expected = typeof(Task<global::app.data.@this<Response>>);
        await Assert.That(ret).IsEqualTo(expected);
    }

    [Test]
    public async Task HttpUpload_Run_ReturnsTaskDataOfResponse()
    {
        var ret = RunReturnType<global::app.module.http.upload>();
        var expected = typeof(Task<global::app.data.@this<Response>>);
        await Assert.That(ret).IsEqualTo(expected);
    }

    [Test]
    public async Task HttpDownload_Run_SignatureUnchanged()
    {
        var ret = RunReturnType<global::app.module.http.download>();
        // http.download writes to disk; no body wrapping → bare Task<Data>.
        await Assert.That(ret).IsEqualTo(typeof(Task<Data>));
    }
}
