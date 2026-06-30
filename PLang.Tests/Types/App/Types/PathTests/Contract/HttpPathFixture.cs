using Path = global::app.type.path.@this;
using HttpPath = global::app.type.path.http.@this;
using System;
using System.Threading.Tasks;
using PLang.Tests.App.Types.PathTests.Http;

namespace PLang.Tests.App.Types.PathTests.Contract;

/// <summary>
/// <see cref="IPathSchemeFixture"/> for the <c>http</c> scheme. Spins up an
/// in-process <see cref="HttpTestServer"/> and mints HttpPaths at fresh server
/// URLs. The HTTP test server has no directory-listing concept, so
/// <see cref="CanPerform"/> scopes <see cref="VerbName.List"/> out.
/// </summary>
public sealed class HttpPathFixture : IPathSchemeFixture, IDisposable
{
    private readonly HttpTestServer _server;
    private readonly string _appRoot;
    private readonly global::app.@this _app;

    public HttpPathFixture()
    {
        _server = new HttpTestServer();
        _appRoot = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plang-chx-" + Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(_appRoot);
        _app = new global::app.@this(_appRoot);
        global::PLang.Tests.TestApp.UseTestSigning(_app);
    }

    public Task<Path> CreateFresh()
    {
        var url = _server.NewResourceUrl();
        return Task.FromResult<Path>(new HttpPath(url, _app.User.Context));
    }

    public Task Cleanup(Path p) => Task.CompletedTask;   // server entries die with the server

    public bool CanPerform(VerbName verb) => verb != VerbName.List;

    public string Scheme => "http";

    public void Dispose()
    {
        _app.DisposeAsync().AsTask().GetAwaiter().GetResult();
        _server.Dispose();
        try { if (System.IO.Directory.Exists(_appRoot)) System.IO.Directory.Delete(_appRoot, true); } catch { }
    }
}
