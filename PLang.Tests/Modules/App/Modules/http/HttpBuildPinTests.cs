namespace PLang.Tests.App.actions.http;

// The builder's pick for words → http.request vs http.upload, pinned against the committed build-only
// .pr at test/module/http/postbodypin/ (a .goal, never run — it would hit the network). A json/dict
// `body {…}` post is http.request with a Body; a file is http.upload. The pin goes red against a .pr
// built before the request/upload descriptions were disambiguated (that .pr maps the post to http.upload).
public class HttpBuildPinTests
{
    private static System.Text.Json.JsonElement Step(int index)
    {
        var path = System.IO.Path.Combine(global::PLang.Tests.Shared.Fixture.Root(), "test", "module", "http",
            "postbodypin", ".build", "postbodypin.pr");
        var pr = System.Text.Json.JsonDocument.Parse(System.IO.File.ReadAllText(path)).RootElement;
        return pr.GetProperty("step").EnumerateArray().First(s => s.GetProperty("index").GetInt32() == index);
    }

    private static (string Name, System.Text.Json.JsonValueKind? BodyKind) HttpAction(System.Text.Json.JsonElement step)
    {
        foreach (var a in step.GetProperty("code").EnumerateArray())
        {
            if (a.GetProperty("module").GetString() != "http") continue;
            System.Text.Json.JsonValueKind? body = null;
            if (a.TryGetProperty("property", out var props))
                foreach (var p in props.EnumerateArray())
                    if (p.GetProperty("name").GetString() == "Body")
                        body = p.GetProperty("value").ValueKind;
            return (a.GetProperty("name").GetString()!, body);
        }
        return ("", null);
    }

    [Test]
    public async Task PostWithJsonBody_PicksHttpRequest_WithABodyObject()
    {
        var (name, body) = HttpAction(Step(0));
        await Assert.That(name).IsEqualTo("request");
        await Assert.That(body).IsEqualTo(System.Text.Json.JsonValueKind.Object);
    }

    [Test]
    public async Task UploadFile_PicksHttpUpload()
    {
        var (name, _) = HttpAction(Step(1));
        await Assert.That(name).IsEqualTo("upload");
    }
}
