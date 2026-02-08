using PLang.Runtime2.Context;
using PLang.Runtime2.Core;
using PLang.Runtime2.Memory;
using PLang.Runtime2.actions.file;
using PLang.SafeFileSystem;
using FileResult = PLang.Runtime2.actions.file.types.file;

namespace PLang.Tests.Runtime2.actions.file;

public class FileHandlerTests : IDisposable
{
    private readonly string _tempDir;
    private readonly PLangFileSystem _fs;
    private readonly Engine _engine;

    public FileHandlerTests()
    {
        _tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plang_test_" + Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(_tempDir);
        _fs = new PLangFileSystem(_tempDir, "");
        var appContext = new PLangAppContext(_tempDir);
        _engine = new Engine(appContext, fileSystem: _fs);
    }

    public void Dispose()
    {
        _engine.DisposeAsync().AsTask().GetAwaiter().GetResult();
        if (System.IO.Directory.Exists(_tempDir))
            System.IO.Directory.Delete(_tempDir, true);
    }

    private string TempPath(string relativePath) =>
        System.IO.Path.Combine(_tempDir, relativePath);

    private T CreateHandler<T>() where T : new()
    {
        var handler = new T();
        if (handler is PLang.Runtime2.actions.BaseClass bc)
        {
            var context = _engine.CreateContext();
            bc.Initialize(_engine, context);
        }
        return handler;
    }

    // --- Save ---

    [Test]
    public async Task Save_ReturnsFileWithCorrectPaths()
    {
        var handler = CreateHandler<SaveHandler>();
        var result = await handler.ExecuteAsync(new save { path = TempPath("test.txt"), value = "hello" });

        await Assert.That(result.Success).IsTrue();
        var f = result.Value as FileResult;
        await Assert.That(f).IsNotNull();
        await Assert.That(f!.AbsolutePath).IsEqualTo(TempPath("test.txt"));
        await Assert.That(f.Path).IsEqualTo("test.txt");
    }

    [Test]
    public async Task Save_FileExists_AfterSave()
    {
        var handler = CreateHandler<SaveHandler>();
        await handler.ExecuteAsync(new save { path = TempPath("exists.txt"), value = "data" });

        await Assert.That(System.IO.File.Exists(TempPath("exists.txt"))).IsTrue();
    }

    // --- Read ---

    [Test]
    public async Task Read_ReturnsFileObject()
    {
        System.IO.File.WriteAllText(TempPath("read.txt"), "content here");
        var handler = CreateHandler<ReadHandler>();

        var result = await handler.ExecuteAsync(new read { path = TempPath("read.txt") });

        await Assert.That(result.Success).IsTrue();
        var f = result.Value as FileResult;
        await Assert.That(f).IsNotNull();
        await Assert.That(f!.Path).IsEqualTo("read.txt");
    }

    [Test]
    public async Task Read_ContentIsLazy()
    {
        System.IO.File.WriteAllText(TempPath("lazy.txt"), "lazy content");
        var handler = CreateHandler<ReadHandler>();

        var result = await handler.ExecuteAsync(new read { path = TempPath("lazy.txt") });
        var f = result.Value as FileResult;

        await Assert.That(f!.Content.Value).IsEqualTo("lazy content");
    }

    [Test]
    public async Task Read_NonexistentFile_ReturnsError()
    {
        var handler = CreateHandler<ReadHandler>();

        var result = await handler.ExecuteAsync(new read { path = TempPath("nonexistent.txt") });

        await Assert.That(result.Success).IsFalse();
    }

    // --- Copy ---

    [Test]
    public async Task Copy_ReturnsFileWithSource()
    {
        System.IO.File.WriteAllText(TempPath("src.txt"), "source data");
        var handler = CreateHandler<CopyHandler>();

        var result = await handler.ExecuteAsync(new copy { source = TempPath("src.txt"), destination = TempPath("dst.txt") });

        await Assert.That(result.Success).IsTrue();
        var f = result.Value as FileResult;
        await Assert.That(f).IsNotNull();
        await Assert.That(f!.Source).IsNotNull();
        await Assert.That(f.Path).IsEqualTo("dst.txt");
    }

    // --- Move ---

    [Test]
    public async Task Move_ReturnsFileWithSource()
    {
        System.IO.File.WriteAllText(TempPath("move_src.txt"), "move data");
        var handler = CreateHandler<MoveHandler>();

        var result = await handler.ExecuteAsync(new move { source = TempPath("move_src.txt"), destination = TempPath("move_dst.txt") });

        await Assert.That(result.Success).IsTrue();
        var f = result.Value as FileResult;
        await Assert.That(f).IsNotNull();
        await Assert.That(f!.Source).IsNotNull();
        await Assert.That(f.Path).IsEqualTo("move_dst.txt");
    }

    // --- Delete ---

    [Test]
    public async Task Delete_ReturnsFile()
    {
        System.IO.File.WriteAllText(TempPath("del.txt"), "delete me");
        var handler = CreateHandler<DeleteHandler>();

        var result = await handler.ExecuteAsync(new delete { path = TempPath("del.txt") });

        await Assert.That(result.Success).IsTrue();
        var f = result.Value as FileResult;
        await Assert.That(f).IsNotNull();
        await Assert.That(f!.Exists).IsFalse();
    }

    // --- Exists ---

    [Test]
    public async Task Exists_ExistingFile_ReturnsTrue()
    {
        System.IO.File.WriteAllText(TempPath("check.txt"), "present");
        var handler = CreateHandler<ExistsHandler>();

        var result = await handler.ExecuteAsync(new exists { path = TempPath("check.txt") });

        await Assert.That(result.Success).IsTrue();
        var f = result.Value as FileResult;
        await Assert.That(f).IsNotNull();
        await Assert.That(f!.Exists).IsTrue();
    }

    [Test]
    public async Task Exists_NonexistentFile_ReturnsFalse()
    {
        var handler = CreateHandler<ExistsHandler>();

        var result = await handler.ExecuteAsync(new exists { path = TempPath("missing.txt") });

        await Assert.That(result.Success).IsTrue();
        var f = result.Value as FileResult;
        await Assert.That(f).IsNotNull();
        await Assert.That(f!.Exists).IsFalse();
    }

    // --- List ---

    [Test]
    public async Task List_ReturnsFileWithFilesProperty()
    {
        var subDir = TempPath("listdir");
        System.IO.Directory.CreateDirectory(subDir);
        System.IO.File.WriteAllText(System.IO.Path.Combine(subDir, "a.txt"), "a");
        System.IO.File.WriteAllText(System.IO.Path.Combine(subDir, "b.txt"), "b");
        var handler = CreateHandler<ListHandler>();

        var result = await handler.ExecuteAsync(new list { path = subDir });

        await Assert.That(result.Success).IsTrue();
        var f = result.Value as FileResult;
        await Assert.That(f).IsNotNull();
        await Assert.That(f!.Files.Length).IsEqualTo(2);
    }

    [Test]
    public async Task List_NonexistentDirectory_ReturnsError()
    {
        var handler = CreateHandler<ListHandler>();

        var result = await handler.ExecuteAsync(new list { path = TempPath("nodir") });

        await Assert.That(result.Success).IsFalse();
    }

    // --- File type object properties ---

    [Test]
    public async Task FileType_MimeType_FromExtension()
    {
        System.IO.File.WriteAllText(TempPath("doc.md"), "# Hello");
        var handler = CreateHandler<ReadHandler>();

        var result = await handler.ExecuteAsync(new read { path = TempPath("doc.md") });
        var f = result.Value as FileResult;

        await Assert.That(f!.Type).IsEqualTo("text/markdown");
    }

    [Test]
    public async Task FileType_Size_IsLazy()
    {
        System.IO.File.WriteAllText(TempPath("sized.txt"), "12345");
        var handler = CreateHandler<ReadHandler>();

        var result = await handler.ExecuteAsync(new read { path = TempPath("sized.txt") });
        var f = result.Value as FileResult;

        await Assert.That(f!.Size).IsEqualTo(5);
    }

    [Test]
    public async Task FileType_ToString_ReturnsRelativePath()
    {
        System.IO.File.WriteAllText(TempPath("tostring.txt"), "x");
        var handler = CreateHandler<ReadHandler>();

        var result = await handler.ExecuteAsync(new read { path = TempPath("tostring.txt") });
        var f = result.Value as FileResult;

        await Assert.That(f!.ToString()).IsEqualTo("tostring.txt");
    }
}
