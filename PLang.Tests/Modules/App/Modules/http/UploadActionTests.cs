using System.Net;
using System.Net.Http.Headers;
using System.Text;
using app.actor.context;
using app.variable;
using app.module.http;
using app.module.http.code;
using PLangEngine = global::app.@this;
using HttpMethod = global::app.module.http.HttpMethod;

namespace PLang.Tests.App.Modules.http;

/// <summary>
/// Tests upload action with real Default + mock HTTP transport.
/// </summary>
public class UploadActionTests
{
    private string _tempDir = null!;
    private PLangEngine _app = null!;
    private MockHttpMessageHandler _handler = null!;

    [Before(Test)]
    public void Setup()
    {
        _tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang_test_http_ul_" + Guid.NewGuid().ToString("N")[..8]);
        System.IO.Directory.CreateDirectory(_tempDir);
        _app = new PLangEngine(_tempDir);

        _handler = new MockHttpMessageHandler();
        var provider = new Default(_handler) { Name = "test" };
        _app.Code.Register<IHttp>(provider);
        _app.Code.SetDefault<IHttp>("test");
    }

    [After(Test)]
    public async Task Cleanup()
    {
        try
        {
            await _app.DisposeAsync();
            if (System.IO.Directory.Exists(_tempDir))
                System.IO.Directory.Delete(_tempDir, true);
        }
        catch { /* best effort cleanup */ }
    }

    private global::app.actor.context.@this Ctx => _app.System.Context;

    private class MockHttpMessageHandler : System.Net.Http.HttpMessageHandler
    {
        public Func<System.Net.Http.HttpRequestMessage, Task<System.Net.Http.HttpResponseMessage>>? Handler { get; set; }
        public System.Net.Http.HttpRequestMessage? LastRequest { get; private set; }

        protected override Task<System.Net.Http.HttpResponseMessage> SendAsync(
            System.Net.Http.HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            if (Handler != null) return Handler(request);
            return Task.FromResult(new System.Net.Http.HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new System.Net.Http.StringContent("{\"ok\":true}", Encoding.UTF8, "application/json")
            });
        }
    }

    [Test]
    public async Task Upload_TextContent_SendsStringContent()
    {
        var action = new upload
        {
            Context = Ctx,
            Url = (global::app.type.text.@this)"https://api.example.com/upload",
            Content = new global::app.data.@this("", "Hello upload"),
            As = (global::app.type.choice.@this<global::app.module.http.ContentAs>)ContentAs.Text,
            Unsigned = (global::app.type.@bool.@this)true
        };

        var result = await action.Run();

        await result.IsSuccess();
        await Assert.That(_handler.LastRequest!.Method).IsEqualTo(System.Net.Http.HttpMethod.Post);
        var body = await _handler.LastRequest!.Content!.ReadAsStringAsync();
        await Assert.That(body).IsEqualTo("Hello upload");
    }

    [Test]
    public async Task Upload_FileContent_SendsBytes()
    {
        var filePath = System.IO.Path.Combine(_tempDir, "upload.txt");
        await System.IO.File.WriteAllTextAsync(filePath, "file data");

        var action = new upload
        {
            Context = Ctx,
            Url = (global::app.type.text.@this)"https://api.example.com/upload",
            Content = new global::app.data.@this("", "upload.txt"),
            As = (global::app.type.choice.@this<global::app.module.http.ContentAs>)ContentAs.File,
            Unsigned = (global::app.type.@bool.@this)true
        };

        var result = await action.Run();

        await result.IsSuccess();
        var body = await _handler.LastRequest!.Content!.ReadAsByteArrayAsync();
        await Assert.That(Encoding.UTF8.GetString(body)).IsEqualTo("file data");
    }

    [Test]
    public async Task Upload_Base64Content_DecodesAndSends()
    {
        var original = "hello base64";
        var b64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(original));

        var action = new upload
        {
            Context = Ctx,
            Url = (global::app.type.text.@this)"https://api.example.com/upload",
            Content = new global::app.data.@this("", b64),
            As = (global::app.type.choice.@this<global::app.module.http.ContentAs>)ContentAs.Base64,
            Unsigned = (global::app.type.@bool.@this)true
        };

        var result = await action.Run();

        await result.IsSuccess();
        var body = await _handler.LastRequest!.Content!.ReadAsByteArrayAsync();
        await Assert.That(Encoding.UTF8.GetString(body)).IsEqualTo(original);
    }

    [Test]
    public async Task Upload_AutoDetectFile_WhenFileExists()
    {
        var filePath = System.IO.Path.Combine(_tempDir, "auto.txt");
        await System.IO.File.WriteAllTextAsync(filePath, "auto content");

        var action = new upload
        {
            Context = Ctx,
            Url = (global::app.type.text.@this)"https://api.example.com/upload",
            Content = new global::app.data.@this("", "auto.txt"),
            Unsigned = (global::app.type.@bool.@this)true
        };

        var result = await action.Run();

        await result.IsSuccess();
        // Verify file content was uploaded, not the filename string
        var body = await _handler.LastRequest!.Content!.ReadAsByteArrayAsync();
        await Assert.That(Encoding.UTF8.GetString(body)).IsEqualTo("auto content");
    }

    [Test]
    public async Task Upload_AutoDetectString_WhenNoFile()
    {
        var action = new upload
        {
            Context = Ctx,
            Url = (global::app.type.text.@this)"https://api.example.com/upload",
            Content = new global::app.data.@this("", "just a string, not a file path"),
            Unsigned = (global::app.type.@bool.@this)true
        };

        var result = await action.Run();

        await result.IsSuccess();
        var body = await _handler.LastRequest!.Content!.ReadAsStringAsync();
        await Assert.That(body).IsEqualTo("just a string, not a file path");
    }

    [Test]
    public async Task Upload_CustomMethod_UsedCorrectly()
    {
        var action = new upload
        {
            Context = Ctx,
            Url = (global::app.type.text.@this)"https://api.example.com/upload",
            Content = new global::app.data.@this("", "data"),
            Method = (global::app.type.choice.@this<global::app.module.http.HttpMethod>)HttpMethod.PUT,
            As = (global::app.type.choice.@this<global::app.module.http.ContentAs>)ContentAs.Text,
            Unsigned = (global::app.type.@bool.@this)true
        };

        var result = await action.Run();

        await result.IsSuccess();
        await Assert.That(_handler.LastRequest!.Method).IsEqualTo(System.Net.Http.HttpMethod.Put);
    }

    [Test]
    public async Task Upload_ResponseParsed_AsJson()
    {
        _handler.Handler = _ => Task.FromResult(new System.Net.Http.HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new System.Net.Http.StringContent("{\"id\":42}", Encoding.UTF8, "application/json")
        });

        var action = new upload
        {
            Context = Ctx,
            Url = (global::app.type.text.@this)"https://api.example.com/upload",
            Content = new global::app.data.@this("", "data"),
            As = (global::app.type.choice.@this<global::app.module.http.ContentAs>)ContentAs.Text,
            Unsigned = (global::app.type.@bool.@this)true
        };

        var result = await action.Run();

        await result.IsSuccess();
        await Assert.That((await result.Value())).IsNotNull();
        // Serialize against the runtime type (object) so the value's own
        // [JsonConverter] fires, not the item base's infra props.
        var json = System.Text.Json.JsonSerializer.Serialize((object?)await result.Value());
        await Assert.That(json).Contains("42");
        await Assert.That((await result.Properties.Value("StatusCode"))).IsEqualTo(200);
    }

    #region Form Upload & @file

    [Test]
    public async Task Upload_DictAutoDetect_SendsMultipartForm()
    {
        var action = new upload
        {
            Context = Ctx,
            Url = (global::app.type.text.@this)"https://api.example.com/upload",
            Content = new global::app.data.@this("", new Dictionary<string, object> { ["name"] = "Alice", ["age"] = "30" }),
            Unsigned = (global::app.type.@bool.@this)true
        };

        var result = await action.Run();

        await result.IsSuccess();
        await Assert.That(_handler.LastRequest!.Content).IsTypeOf<MultipartFormDataContent>();
        // Verify field names are present in the multipart form
        var multipart = (MultipartFormDataContent)_handler.LastRequest!.Content!;
        var parts = multipart.ToList();
        await Assert.That(parts.Count).IsEqualTo(2);
        var names = parts.Select(p => p.Headers.ContentDisposition!.Name).OrderBy(n => n).ToList();
        await Assert.That(names).Contains("name");
        await Assert.That(names).Contains("age");
    }

    [Test]
    public async Task Upload_FormExplicit_SendsMultipartForm()
    {
        var action = new upload
        {
            Context = Ctx,
            Url = (global::app.type.text.@this)"https://api.example.com/upload",
            Content = new global::app.data.@this("", new Dictionary<string, object> { ["field1"] = "value1" }),
            As = (global::app.type.choice.@this<global::app.module.http.ContentAs>)ContentAs.Form,
            Unsigned = (global::app.type.@bool.@this)true
        };

        var result = await action.Run();

        await result.IsSuccess();
        await Assert.That(_handler.LastRequest!.Content).IsTypeOf<MultipartFormDataContent>();
        // Verify field name and value
        var multipart = (MultipartFormDataContent)_handler.LastRequest!.Content!;
        var part = multipart.First();
        await Assert.That(part.Headers.ContentDisposition!.Name).IsEqualTo("field1");
        var value = await part.ReadAsStringAsync();
        await Assert.That(value).IsEqualTo("value1");
    }

    [Test]
    public async Task Upload_FormWithAtFile_ReadsFileContent()
    {
        var filePath = System.IO.Path.Combine(_tempDir, "document.pdf");
        await System.IO.File.WriteAllTextAsync(filePath, "fake pdf content");

        var action = new upload
        {
            Context = Ctx,
            Url = (global::app.type.text.@this)"https://api.example.com/upload",
            Content = new global::app.data.@this("", new Dictionary<string, object> { ["title"] = "My Doc", ["file"] = "@document.pdf" }),
            As = (global::app.type.choice.@this<global::app.module.http.ContentAs>)ContentAs.Form,
            Unsigned = (global::app.type.@bool.@this)true
        };

        var result = await action.Run();

        await result.IsSuccess();
        // Verify multipart form was sent
        await Assert.That(_handler.LastRequest!.Content).IsTypeOf<MultipartFormDataContent>();

        // Read multipart content and verify file part exists
        var multipart = (MultipartFormDataContent)_handler.LastRequest!.Content!;
        var parts = multipart.ToList();
        await Assert.That(parts.Count).IsEqualTo(2); // title + file

        // Find the file part (has filename in Content-Disposition)
        var filePart = parts.FirstOrDefault(p =>
            p.Headers.ContentDisposition?.FileName != null);
        await Assert.That(filePart).IsNotNull();
        await Assert.That(filePart!.Headers.ContentDisposition!.FileName).IsEqualTo("document.pdf");

        var fileContent = await filePart.ReadAsStringAsync();
        await Assert.That(fileContent).IsEqualTo("fake pdf content");
    }

    [Test]
    public async Task Upload_AutoDetectObject_SendsJson()
    {
        // Non-dict, non-string object → serialized as JSON
        var action = new upload
        {
            Context = Ctx,
            Url = (global::app.type.text.@this)"https://api.example.com/upload",
            Content = new global::app.data.@this("", new List<string> { "a", "b", "c" }),
            Unsigned = (global::app.type.@bool.@this)true
        };

        var result = await action.Run();

        await result.IsSuccess();
        var body = await _handler.LastRequest!.Content!.ReadAsStringAsync();
        await Assert.That(body).Contains("[");
        await Assert.That(body).Contains("\"a\"");
    }

    #endregion
}
