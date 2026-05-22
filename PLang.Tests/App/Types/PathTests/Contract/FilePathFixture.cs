using Path = global::app.types.path.@this;
using FilePath = global::app.types.path.file.@this;
using System;
using System.Threading.Tasks;

namespace PLang.Tests.App.Types.PathTests.Contract;

/// <summary>
/// <see cref="IPathSchemeFixture"/> for the <c>file</c> scheme. Mints OUT-OF-ROOT
/// FilePaths (resource dir is a sibling of the App root, not under it) so the
/// Permission gate fires uniformly — the contract suite drives authorization
/// via a canned channel, the same way it does for HttpPath. A filesystem
/// supports every verb.
/// </summary>
public sealed class FilePathFixture : IPathSchemeFixture, IDisposable
{
    private readonly string _appRoot;
    private readonly string _resourceDir;
    private readonly global::app.@this _app;

    public FilePathFixture()
    {
        _appRoot = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plang-cfx-root-" + Guid.NewGuid().ToString("N"));
        _resourceDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plang-cfx-res-" + Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(_appRoot);
        System.IO.Directory.CreateDirectory(_resourceDir);
        _app = new global::app.@this(_appRoot);
    }

    public Task<Path> CreateFresh()
    {
        var file = System.IO.Path.Combine(_resourceDir, Guid.NewGuid().ToString("N") + ".txt");
        return Task.FromResult<Path>(new FilePath(file, _app.User.Context));
    }

    public Task Cleanup(Path p)
    {
        try { if (System.IO.File.Exists(p.Absolute)) System.IO.File.Delete(p.Absolute); }
        catch { /* idempotent */ }
        return Task.CompletedTask;
    }

    public bool CanPerform(VerbName verb) => true;

    public string Scheme => "file";

    public void Dispose()
    {
        _app.DisposeAsync().AsTask().GetAwaiter().GetResult();
        try { if (System.IO.Directory.Exists(_appRoot)) System.IO.Directory.Delete(_appRoot, true); } catch { }
        try { if (System.IO.Directory.Exists(_resourceDir)) System.IO.Directory.Delete(_resourceDir, true); } catch { }
    }
}
