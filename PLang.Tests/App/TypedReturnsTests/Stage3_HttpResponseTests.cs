namespace PLang.Tests.App.TypedReturnsTests;

// Stage 3 — HTTP Response record + typed http.request/upload returns.
// Architect: .bot/typed-action-returns/architect/stages.md (Stage 3)
// Plan: .bot/typed-action-returns/architect/plan.md (A.3)

public class Stage3_HttpResponseTests
{
    [Test]
    public async Task HttpResponse_RecordExistsAt_AppHttpResponseThis()
        // typeof(app.http.Response.@this) exists at PLang/app/http/Response/this.cs.
        => Assert.Fail("Not implemented");

    [Test]
    public async Task HttpResponse_CarriesAllFourFields()
        // Properties: Status:int, Headers:Dictionary<string,string>, Body:object?, Duration:TimeSpan.
        => Assert.Fail("Not implemented");

    [Test]
    public async Task HttpResponse_IsSealedRecord()
        // typeof(Response).IsSealed && record-equality semantics hold.
        => Assert.Fail("Not implemented");

    [Test]
    public async Task HttpRequest_Run_ReturnsTaskDataOfResponse()
        // app.modules.http.RequestHandler.Run returns Task<Data<Response>>.
        => Assert.Fail("Not implemented");

    [Test]
    public async Task HttpUpload_Run_ReturnsTaskDataOfResponse()
        // app.modules.http.UploadHandler.Run returns Task<Data<Response>>.
        => Assert.Fail("Not implemented");

    [Test]
    public async Task HttpDownload_Run_SignatureUnchanged()
        // http.download still saves to file; its return signature is whatever it was pre-stage (no shape change here).
        => Assert.Fail("Not implemented");
}
