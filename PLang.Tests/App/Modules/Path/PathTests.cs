using PLang.SafeFileSystem;
using App.Engine.FileSystem;
using App.Engine.Variables;
using App.modules.file;
using App.modules.file.providers;

namespace PLang.Tests.App.Modules.Path;

public class PathTests : IDisposable
{
    private readonly string _tempDir;
    private readonly PLangFileSystem _fs;
    private readonly App.Engine.@this _engine;
    private readonly DefaultFileProvider _provider;

    public PathTests()
    {
        _tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plang_path_test_" + Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(_tempDir);
        _fs = new PLangFileSystem(_tempDir, "");
        _engine = new App.Engine.@this(_tempDir, fileSystem: _fs);
        _provider = new DefaultFileProvider();
    }

    public void Dispose()
    {
        _engine.DisposeAsync().AsTask().GetAwaiter().GetResult();
        if (System.IO.Directory.Exists(_tempDir))
            System.IO.Directory.Delete(_tempDir, true);
    }

    private string TempFile(string name)
    {
        var path = _fs.Path.Combine(_tempDir, name);
        _fs.File.WriteAllText(path, "test content");
        return path;
    }

    // --- ToBoolean ---

    [Test]
    public async Task ToBoolean_FileExists_ReturnsTrue()
    {
        var path = TempFile("exists.txt");
        var pd = new Path(path, _fs);

        await Assert.That(pd.ToBoolean()).IsTrue();
    }

    [Test]
    public async Task ToBoolean_FileDoesNotExist_ReturnsFalse()
    {
        var pd = new Path(_fs.Path.Combine(_tempDir, "nope.txt"), _fs);

        await Assert.That(pd.ToBoolean()).IsFalse();
    }

    [Test]
    public async Task IsTruthy_PathDataExists_ReturnsTrue()
    {
        var path = TempFile("truthy.txt");
        var pd = new Path(path, _fs);

        await Assert.That(App.modules.condition.Operator.IsTruthy(pd)).IsTrue();
    }

    [Test]
    public async Task IsTruthy_PathDataNotExists_ReturnsFalse()
    {
        var pd = new Path(_fs.Path.Combine(_tempDir, "missing.txt"), _fs);

        await Assert.That(App.modules.condition.Operator.IsTruthy(pd)).IsFalse();
    }

    // --- Constructor & Absolute ---

    [Test]
    public async Task Absolute_ResolvesRelativePath()
    {
        var p = new PLangPath("config.json", _engine.Context);
        var expected = _fs.Path.Combine(_tempDir, "config.json");
        await Assert.That(p.Absolute).IsEqualTo(expected);
    }

    [Test]
    public async Task Absolute_PreservesAbsolutePath()
    {
        var abs = _fs.Path.Combine(_tempDir, "test.txt");
        var p = new PLangPath(abs, _engine.Context);
        await Assert.That(p.Absolute).IsEqualTo(abs);
    }

    // --- Raw ---

    [Test]
    public async Task Raw_PreservesOriginalInput()
    {
        var p = new PLangPath("relative/file.txt", _engine.Context);
        await Assert.That(p.Raw).IsEqualTo("relative/file.txt");
    }

    // --- Relative ---

    [Test]
    public async Task Relative_StripsRootDirectory()
    {
        var abs = _fs.Path.Combine(_tempDir, "sub", "file.txt");
        var p = new PLangPath(abs, _engine.Context);
        var expected = "sub" + _fs.Path.DirectorySeparatorChar + "file.txt";
        await Assert.That(p.Relative).IsEqualTo(expected);
    }

    // --- Extension ---

    [Test]
    public async Task Extension_ReturnsWithDot()
    {
        var p = new PLangPath("config.json", _engine.Context);
        await Assert.That(p.Extension).IsEqualTo(".json");
    }

    [Test]
    public async Task Extension_NoExtension_ReturnsEmpty()
    {
        var p = new PLangPath("Makefile", _engine.Context);
        await Assert.That(p.Extension).IsEqualTo("");
    }

    // --- FileName ---

    [Test]
    public async Task FileName_ReturnsFileNameWithExtension()
    {
        var p = new PLangPath(_fs.Path.Combine(_tempDir, "sub", "config.json"), _engine.Context);
        await Assert.That(p.FileName).IsEqualTo("config.json");
    }

    // --- FileNameWithoutExtension ---

    [Test]
    public async Task FileNameWithoutExtension_ReturnsNameOnly()
    {
        var p = new PLangPath("archive.tar.gz", _engine.Context);
        await Assert.That(p.FileNameWithoutExtension).IsEqualTo("archive.tar");
    }

    // --- Directory ---

    [Test]
    public async Task Directory_ReturnsParentPath()
    {
        var p = new PLangPath(_fs.Path.Combine(_tempDir, "sub", "file.txt"), _engine.Context);
        var expected = _fs.Path.Combine(_tempDir, "sub");
        await Assert.That(p.Directory).IsEqualTo(expected);
    }

    // --- MimeType ---

    [Test]
    public async Task MimeType_Json()
    {
        var p = new PLangPath("data.json", _engine.Context);
        await Assert.That(p.MimeType).IsEqualTo("application/json");
    }

    [Test]
    public async Task MimeType_Markdown()
    {
        var p = new PLangPath("README.md", _engine.Context);
        await Assert.That(p.MimeType).IsEqualTo("text/markdown");
    }

    [Test]
    public async Task MimeType_Unknown_ReturnsOctetStream()
    {
        var p = new PLangPath("data.xyz", _engine.Context);
        await Assert.That(p.MimeType).IsEqualTo("application/octet-stream");
    }

    // --- IsFile (structural: has extension) ---

    [Test]
    public async Task IsFile_WithExtension_ReturnsTrue()
    {
        var p = new PLangPath("config.json", _engine.Context);
        await Assert.That(p.IsFile).IsTrue();
    }

    [Test]
    public async Task IsFile_NoExtension_ReturnsFalse()
    {
        var p = new PLangPath("Makefile", _engine.Context);
        await Assert.That(p.IsFile).IsFalse();
    }

    // --- IsDirectory (structural: no extension) ---

    [Test]
    public async Task IsDirectory_NoExtension_ReturnsTrue()
    {
        var p = new PLangPath("mydir", _engine.Context);
        await Assert.That(p.IsDirectory).IsTrue();
    }

    [Test]
    public async Task IsDirectory_WithExtension_ReturnsFalse()
    {
        var p = new PLangPath("file.txt", _engine.Context);
        await Assert.That(p.IsDirectory).IsFalse();
    }

    // --- Exists ---

    [Test]
    public async Task Exists_ExistingFile_ReturnsTrue()
    {
        var filePath = TempFile("there.txt");
        var p = new PLangPath(filePath, _engine.Context);
        await Assert.That(p.Exists).IsTrue();
    }

    [Test]
    public async Task Exists_ExistingDir_ReturnsTrue()
    {
        var p = new PLangPath(_tempDir, _engine.Context);
        await Assert.That(p.Exists).IsTrue();
    }

    [Test]
    public async Task Exists_Nothing_ReturnsFalse()
    {
        var p = new PLangPath(_fs.Path.Combine(_tempDir, "phantom"), _engine.Context);
        await Assert.That(p.Exists).IsFalse();
    }

    // --- Size ---

    [Test]
    public async Task Size_ExistingFile_ReturnsCorrectSize()
    {
        var filePath = _fs.Path.Combine(_tempDir, "sized.txt");
        _fs.File.WriteAllText(filePath, "12345");
        var p = new PLangPath(filePath, _engine.Context);
        await Assert.That(p.Size).IsEqualTo(5);
    }

    [Test]
    public async Task Size_NonexistentFile_ReturnsZero()
    {
        var p = new PLangPath(_fs.Path.Combine(_tempDir, "nosize.txt"), _engine.Context);
        await Assert.That(p.Size).IsEqualTo(0);
    }

    // --- ToString ---

    [Test]
    public async Task ToString_ReturnsRelativePath()
    {
        var abs = _fs.Path.Combine(_tempDir, "test.txt");
        var p = new PLangPath(abs, _engine.Context);
        await Assert.That(p.ToString()).IsEqualTo("test.txt");
    }

    // --- Equality ---

    [Test]
    public async Task Equals_SamePath_ReturnsTrue()
    {
        var abs = _fs.Path.Combine(_tempDir, "same.txt");
        var p1 = new PLangPath(abs, _engine.Context);
        var p2 = new PLangPath(abs, _engine.Context);
        await Assert.That(p1.Equals(p2)).IsTrue();
    }

    [Test]
    public async Task Equals_String_ReturnsTrue()
    {
        var abs = _fs.Path.Combine(_tempDir, "str.txt");
        var p = new PLangPath(abs, _engine.Context);
        await Assert.That(p.Equals(abs)).IsTrue();
    }

    [Test]
    public async Task Equals_DifferentPath_ReturnsFalse()
    {
        var p1 = new PLangPath(_fs.Path.Combine(_tempDir, "a.txt"), _engine.Context);
        var p2 = new PLangPath(_fs.Path.Combine(_tempDir, "b.txt"), _engine.Context);
        await Assert.That(p1.Equals(p2)).IsFalse();
    }

    // --- Resolve (engine-resolvable) ---

    [Test]
    public async Task Resolve_CreatesPathViaEngine()
    {
        var p = PLangPath.Resolve("test.txt", _engine.Context);
        await Assert.That(p).IsNotNull();
        await Assert.That(p.FileName).IsEqualTo("test.txt");
    }

    // --- Exists is live (not cached) ---

    [Test]
    public async Task Exists_BecomesTrue_AfterFileCreated()
    {
        var filePath = _fs.Path.Combine(_tempDir, "later.txt");
        var p = new PLangPath(filePath, _engine.Context);

        await Assert.That(p.Exists).IsFalse();

        _fs.File.WriteAllText(filePath, "created");
        await Assert.That(p.Exists).IsTrue();
    }

    // --- Cached properties are stable ---

    [Test]
    public async Task CachedProperties_SameOnMultipleAccess()
    {
        var p = new PLangPath(_fs.Path.Combine(_tempDir, "stable.json"), _engine.Context);
        var ext1 = p.Extension;
        var ext2 = p.Extension;
        var name1 = p.FileName;
        var name2 = p.FileName;

        await Assert.That(ext1).IsEqualTo(ext2);
        await Assert.That(name1).IsEqualTo(name2);
    }

    // --- Read ---

    [Test]
    public async Task Read_ExistingFile_ReturnsFileObject()
    {
        var filePath = TempFile("read_me.txt");
        var p = new PLangPath(filePath, _engine.Context);
        var result = _provider.Read(new Read { Context = _engine.Context, Path = p });

        await Assert.That(result.Success).IsTrue();
        var f = result as Path;
        await Assert.That(f).IsNotNull();
        await Assert.That(f!.Value).IsEqualTo("test content");
    }

    [Test]
    public async Task Read_NonexistentFile_ReturnsError()
    {
        var p = new PLangPath(_fs.Path.Combine(_tempDir, "no_such.txt"), _engine.Context);
        var result = _provider.Read(new Read { Context = _engine.Context, Path = p });

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.Key).IsEqualTo("NotFound");
        await Assert.That(result.Error!.StatusCode).IsEqualTo(404);
    }

    // --- List ---

    [Test]
    public async Task List_ExistingDirectory_ReturnsFileArray()
    {
        var dir = TempDir("list_dir");
        _fs.File.WriteAllText(_fs.Path.Combine(dir, "a.txt"), "a");
        _fs.File.WriteAllText(_fs.Path.Combine(dir, "b.txt"), "b");

        var p = new PLangPath(dir, _engine.Context);
        var result = _provider.List(new List { Context = _engine.Context, Path = p, Pattern = "*" });

        await Assert.That(result.Success).IsTrue();
        var files = result.Value as Path[];
        await Assert.That(files).IsNotNull();
        await Assert.That(files!.Length).IsEqualTo(2);
        var names = files.Select(f => _fs.Path.GetFileName(f.Absolute)).OrderBy(n => n).ToArray();
        await Assert.That(names[0]).IsEqualTo("a.txt");
        await Assert.That(names[1]).IsEqualTo("b.txt");
    }

    [Test]
    public async Task List_WithPattern_FiltersFiles()
    {
        var dir = TempDir("list_pattern");
        _fs.File.WriteAllText(_fs.Path.Combine(dir, "a.txt"), "a");
        _fs.File.WriteAllText(_fs.Path.Combine(dir, "b.md"), "b");

        var p = new PLangPath(dir, _engine.Context);
        var result = _provider.List(new List { Context = _engine.Context, Path = p, Pattern = "*.txt" });

        await Assert.That(result.Success).IsTrue();
        var files = result.Value as Path[];
        await Assert.That(files!.Length).IsEqualTo(1);
    }

    [Test]
    public async Task List_Recursive_FindsNestedFiles()
    {
        var dir = TempDir("list_recursive");
        var nested = _fs.Path.Combine(dir, "sub");
        _fs.Directory.CreateDirectory(nested);
        _fs.File.WriteAllText(_fs.Path.Combine(dir, "top.txt"), "top");
        _fs.File.WriteAllText(_fs.Path.Combine(nested, "deep.txt"), "deep");

        var p = new PLangPath(dir, _engine.Context);
        var result = _provider.List(new List { Context = _engine.Context, Path = p, Pattern = "*", Recursive = true });

        await Assert.That(result.Success).IsTrue();
        var files = result.Value as Path[];
        await Assert.That(files!.Length).IsEqualTo(2);
    }

    [Test]
    public async Task List_NonexistentDirectory_ReturnsError()
    {
        var p = new PLangPath(_fs.Path.Combine(_tempDir, "no_dir"), _engine.Context);
        var result = _provider.List(new List { Context = _engine.Context, Path = p, Pattern = "*" });

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.Key).IsEqualTo("NotFound");
        await Assert.That(result.Error!.StatusCode).IsEqualTo(404);
    }

    // --- Save ---

    [Test]
    public async Task Save_String_WritesFile()
    {
        var filePath = _fs.Path.Combine(_tempDir, "saved.txt");
        var p = new PLangPath(filePath, _engine.Context);

        var result = await _provider.Save(new Save { Context = _engine.Context, Path = p, Value = Data.Ok("hello world") });

        await Assert.That(result.Success).IsTrue();
        await Assert.That(_fs.File.Exists(filePath)).IsTrue();
        await Assert.That(_fs.File.ReadAllText(filePath)).IsEqualTo("hello world");
    }

    [Test]
    public async Task Save_Bytes_WritesFile()
    {
        var filePath = _fs.Path.Combine(_tempDir, "saved.bin");
        var p = new PLangPath(filePath, _engine.Context);

        var bytes = new byte[] { 1, 2, 3 };
        var result = await _provider.Save(new Save { Context = _engine.Context, Path = p, Value = Data.Ok(bytes) });

        await Assert.That(result.Success).IsTrue();
        await Assert.That(_fs.File.Exists(filePath)).IsTrue();
        var actual = _fs.File.ReadAllBytes(filePath);
        await Assert.That(actual.SequenceEqual(bytes)).IsTrue();
    }

    [Test]
    public async Task Save_CreatesDirectory_IfNotExists()
    {
        var filePath = _fs.Path.Combine(_tempDir, "newdir", "saved.txt");
        var p = new PLangPath(filePath, _engine.Context);

        var result = await _provider.Save(new Save { Context = _engine.Context, Path = p, Value = Data.Ok("nested") });

        await Assert.That(result.Success).IsTrue();
        await Assert.That(_fs.File.Exists(filePath)).IsTrue();
    }

    // --- AsFile ---

    [Test]
    public async Task AsFile_ReturnsFileObject()
    {
        var filePath = TempFile("asfile.txt");
        var p = new PLangPath(filePath, _engine.Context);
        var result = _provider.Exists(new Exists { Context = _engine.Context, Path = p });

        await Assert.That(result.Success).IsTrue();
        var f = result as Path;
        await Assert.That(f).IsNotNull();
        await Assert.That(f!.Exists).IsTrue();
    }

    // --- Helper for directories ---

    private string TempDir(string name)
    {
        var path = _fs.Path.Combine(_tempDir, name);
        _fs.Directory.CreateDirectory(path);
        return path;
    }

    // --- Copy ---

    [Test]
    public async Task Copy_File_CopiesToDestination()
    {
        var srcPath = TempFile("copy_src.txt");
        var destPath = _fs.Path.Combine(_tempDir, "copy_dst.txt");

        var src = new PLangPath(srcPath, _engine.Context);
        var result = _provider.Copy(new Copy { Context = _engine.Context, Source = src, Destination = new PLangPath(destPath, _engine.Context) });

        await Assert.That(result.Success).IsTrue();
        await Assert.That(_fs.File.Exists(destPath)).IsTrue();
        await Assert.That(_fs.File.ReadAllText(destPath)).IsEqualTo("test content");
        await Assert.That(_fs.File.Exists(srcPath)).IsTrue(); // copy, not move
    }

    [Test]
    public async Task Copy_Directory_CopiesAllFiles()
    {
        var srcDir = TempDir("copy_dir_src");
        _fs.File.WriteAllText(_fs.Path.Combine(srcDir, "a.txt"), "aaa");
        _fs.File.WriteAllText(_fs.Path.Combine(srcDir, "b.txt"), "bbb");

        var destDir = _fs.Path.Combine(_tempDir, "copy_dir_dst");
        var src = new PLangPath(srcDir, _engine.Context);
        var result = _provider.Copy(new Copy { Context = _engine.Context, Source = src, Destination = new PLangPath(destDir, _engine.Context) });

        await Assert.That(result.Success).IsTrue();
        await Assert.That(_fs.File.Exists(_fs.Path.Combine(destDir, "a.txt"))).IsTrue();
        await Assert.That(_fs.File.Exists(_fs.Path.Combine(destDir, "b.txt"))).IsTrue();
    }

    [Test]
    public async Task Copy_Directory_WithSubfolders()
    {
        var srcDir = TempDir("copy_sub_src");
        var nested = _fs.Path.Combine(srcDir, "sub");
        _fs.Directory.CreateDirectory(nested);
        _fs.File.WriteAllText(_fs.Path.Combine(srcDir, "top.txt"), "top");
        _fs.File.WriteAllText(_fs.Path.Combine(nested, "deep.txt"), "deep");

        var destDir = _fs.Path.Combine(_tempDir, "copy_sub_dst");
        var src = new PLangPath(srcDir, _engine.Context);
        var result = _provider.Copy(new Copy { Context = _engine.Context, Source = src, Destination = new PLangPath(destDir, _engine.Context), IncludeSubfolders = true });

        await Assert.That(result.Success).IsTrue();
        await Assert.That(_fs.File.Exists(_fs.Path.Combine(destDir, "top.txt"))).IsTrue();
        await Assert.That(_fs.File.Exists(_fs.Path.Combine(destDir, "sub", "deep.txt"))).IsTrue();
    }

    [Test]
    public async Task Copy_Directory_WithoutSubfolders()
    {
        var srcDir = TempDir("copy_nosub_src");
        var nested = _fs.Path.Combine(srcDir, "sub");
        _fs.Directory.CreateDirectory(nested);
        _fs.File.WriteAllText(_fs.Path.Combine(srcDir, "top.txt"), "top");
        _fs.File.WriteAllText(_fs.Path.Combine(nested, "deep.txt"), "deep");

        var destDir = _fs.Path.Combine(_tempDir, "copy_nosub_dst");
        var src = new PLangPath(srcDir, _engine.Context);
        var result = _provider.Copy(new Copy { Context = _engine.Context, Source = src, Destination = new PLangPath(destDir, _engine.Context), IncludeSubfolders = false });

        await Assert.That(result.Success).IsTrue();
        await Assert.That(_fs.File.Exists(_fs.Path.Combine(destDir, "top.txt"))).IsTrue();
        await Assert.That(_fs.Directory.Exists(_fs.Path.Combine(destDir, "sub"))).IsFalse();
    }

    [Test]
    public async Task Copy_NotFound_ReturnsError()
    {
        var src = new PLangPath(_fs.Path.Combine(_tempDir, "ghost.txt"), _engine.Context);
        var dest = new PLangPath(_fs.Path.Combine(_tempDir, "dst.txt"), _engine.Context);
        var result = _provider.Copy(new Copy { Context = _engine.Context, Source = src, Destination = dest });

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.Key).IsEqualTo("NotFound");
        await Assert.That(result.Error!.StatusCode).IsEqualTo(404);
    }

    // --- Move ---

    [Test]
    public async Task Move_File_MovesToDestination()
    {
        var srcPath = TempFile("move_src.txt");
        var destPath = _fs.Path.Combine(_tempDir, "move_dst.txt");

        var src = new PLangPath(srcPath, _engine.Context);
        var result = _provider.Move(new Move { Context = _engine.Context, Source = src, Destination = new PLangPath(destPath, _engine.Context) });

        await Assert.That(result.Success).IsTrue();
        await Assert.That(_fs.File.Exists(destPath)).IsTrue();
        await Assert.That(_fs.File.Exists(srcPath)).IsFalse();
    }

    [Test]
    public async Task Move_Directory_MovesToDestination()
    {
        var srcDir = TempDir("move_dir_src");
        _fs.File.WriteAllText(_fs.Path.Combine(srcDir, "a.txt"), "aaa");

        var destDir = _fs.Path.Combine(_tempDir, "move_dir_dst");
        var src = new PLangPath(srcDir, _engine.Context);
        var result = _provider.Move(new Move { Context = _engine.Context, Source = src, Destination = new PLangPath(destDir, _engine.Context) });

        await Assert.That(result.Success).IsTrue();
        await Assert.That(_fs.Directory.Exists(destDir)).IsTrue();
        await Assert.That(_fs.Directory.Exists(srcDir)).IsFalse();
        await Assert.That(_fs.File.Exists(_fs.Path.Combine(destDir, "a.txt"))).IsTrue();
    }

    [Test]
    public async Task Move_NotFound_ReturnsError()
    {
        var src = new PLangPath(_fs.Path.Combine(_tempDir, "ghost.txt"), _engine.Context);
        var dest = new PLangPath(_fs.Path.Combine(_tempDir, "dst.txt"), _engine.Context);
        var result = _provider.Move(new Move { Context = _engine.Context, Source = src, Destination = dest });

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.Key).IsEqualTo("NotFound");
        await Assert.That(result.Error!.StatusCode).IsEqualTo(404);
    }

    // --- Delete ---

    [Test]
    public async Task Delete_File_RemovesFile()
    {
        var filePath = TempFile("del_file.txt");
        var p = new PLangPath(filePath, _engine.Context);
        var result = _provider.Delete(new Delete { Context = _engine.Context, Path = p });

        await Assert.That(result.Success).IsTrue();
        await Assert.That(_fs.File.Exists(filePath)).IsFalse();
    }

    [Test]
    public async Task Delete_EmptyDirectory_RemovesIt()
    {
        var dirPath = TempDir("del_empty_dir");
        var p = new PLangPath(dirPath, _engine.Context);
        var result = _provider.Delete(new Delete { Context = _engine.Context, Path = p });

        await Assert.That(result.Success).IsTrue();
        await Assert.That(_fs.Directory.Exists(dirPath)).IsFalse();
    }

    [Test]
    public async Task Delete_DirectoryRecursive_RemovesAll()
    {
        var dirPath = TempDir("del_recursive_dir");
        _fs.File.WriteAllText(_fs.Path.Combine(dirPath, "child.txt"), "data");

        var p = new PLangPath(dirPath, _engine.Context);
        var result = _provider.Delete(new Delete { Context = _engine.Context, Path = p, Recursive = true });

        await Assert.That(result.Success).IsTrue();
        await Assert.That(_fs.Directory.Exists(dirPath)).IsFalse();
    }

    [Test]
    public async Task Delete_NotFound_ReturnsError()
    {
        var p = new PLangPath(_fs.Path.Combine(_tempDir, "ghost.txt"), _engine.Context);
        var result = _provider.Delete(new Delete { Context = _engine.Context, Path = p });

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.Key).IsEqualTo("NotFound");
        await Assert.That(result.Error!.StatusCode).IsEqualTo(404);
    }

    [Test]
    public async Task Delete_NotFound_IgnoreIfNotFound_ReturnsSuccess()
    {
        var p = new PLangPath(_fs.Path.Combine(_tempDir, "ghost.txt"), _engine.Context);
        var result = _provider.Delete(new Delete { Context = _engine.Context, Path = p, IgnoreIfNotFound = true });

        await Assert.That(result.Success).IsTrue();
    }

    // --- #2: Relative prefix-matching bug ---

    [Test]
    public async Task Relative_DoesNotMatchSimilarPrefix()
    {
        // Root is _tempDir (e.g., /tmp/plang_path_test_abc).
        // A sibling path like _tempDir + "ation" should NOT be treated as relative.
        var siblingPath = _tempDir + "ation" + _fs.Path.DirectorySeparatorChar + "file.txt";
        var p = new PLangPath(siblingPath, _engine.Context);

        // Should return absolute path, not "ation/file.txt"
        await Assert.That(p.Relative).IsEqualTo(p.Absolute);
    }

    // --- #3: Move directory with overwrite ---

    [Test]
    public async Task Move_Directory_Overwrite_ReplacesExisting()
    {
        var srcDir = TempDir("move_over_src");
        _fs.File.WriteAllText(_fs.Path.Combine(srcDir, "new.txt"), "new");

        var destDir = TempDir("move_over_dst");
        _fs.File.WriteAllText(_fs.Path.Combine(destDir, "old.txt"), "old");

        var src = new PLangPath(srcDir, _engine.Context);
        var result = _provider.Move(new Move { Context = _engine.Context, Source = src, Destination = new PLangPath(destDir, _engine.Context), Overwrite = true });

        await Assert.That(result.Success).IsTrue();
        await Assert.That(_fs.File.Exists(_fs.Path.Combine(destDir, "new.txt"))).IsTrue();
        await Assert.That(_fs.File.Exists(_fs.Path.Combine(destDir, "old.txt"))).IsFalse();
        await Assert.That(_fs.Directory.Exists(srcDir)).IsFalse();
    }

    // --- #7: Null guard on constructor ---

    [Test]
    public async Task Constructor_NullPath_ThrowsArgumentNull()
    {
        await Assert.That(() => new PLangPath(null!, _engine.Context)).Throws<ArgumentNullException>();
    }

    [Test]
    public async Task Constructor_NullEngine_ThrowsArgumentNull()
    {
        await Assert.That(() => new PLangPath("test.txt", null!)).Throws<ArgumentNullException>();
    }

    // --- #8: Copy file to existing directory ---

    [Test]
    public async Task Copy_FileToExistingDirectory_PutsFileInsideDir()
    {
        var srcPath = TempFile("copy_to_dir.txt");
        var destDir = TempDir("copy_target_dir");

        var src = new PLangPath(srcPath, _engine.Context);
        var result = _provider.Copy(new Copy { Context = _engine.Context, Source = src, Destination = new PLangPath(destDir, _engine.Context) });

        await Assert.That(result.Success).IsTrue();
        await Assert.That(_fs.File.Exists(_fs.Path.Combine(destDir, "copy_to_dir.txt"))).IsTrue();
    }

    // --- Move file to existing directory (auditor v2 #1) ---

    [Test]
    public async Task Move_FileToExistingDirectory_PutsFileInsideDir()
    {
        var srcPath = TempFile("move_to_dir.txt");
        var destDir = TempDir("move_target_dir");

        var src = new PLangPath(srcPath, _engine.Context);
        var result = _provider.Move(new Move { Context = _engine.Context, Source = src, Destination = new PLangPath(destDir, _engine.Context) });

        await Assert.That(result.Success).IsTrue();
        await Assert.That(_fs.File.Exists(_fs.Path.Combine(destDir, "move_to_dir.txt"))).IsTrue();
        await Assert.That(_fs.File.Exists(srcPath)).IsFalse();
    }

    // --- Relative returns "." for root path (auditor v2 #2) ---

    [Test]
    public async Task Relative_RootPath_ReturnsDot()
    {
        var p = new PLangPath(_tempDir, _engine.Context);
        await Assert.That(p.Relative).IsEqualTo(".");
    }

    // --- Exception handling tests (tester #1) ---

    [Test]
    public async Task Copy_DestExists_OverwriteFalse_ReturnsIOError()
    {
        var srcPath = TempFile("copy_ow_src.txt");
        var destPath = TempFile("copy_ow_dst.txt"); // dest already exists

        var src = new PLangPath(srcPath, _engine.Context);
        var result = _provider.Copy(new Copy { Context = _engine.Context, Source = src, Destination = new PLangPath(destPath, _engine.Context), Overwrite = false });

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.Key).IsEqualTo("IOError");
        await Assert.That(result.Error!.StatusCode).IsEqualTo(500);
    }

    [Test]
    public async Task Copy_DestExists_OverwriteTrue_Succeeds()
    {
        var srcPath = TempFile("copy_owt_src.txt");
        var destPath = _fs.Path.Combine(_tempDir, "copy_owt_dst.txt");
        _fs.File.WriteAllText(destPath, "old content");

        var src = new PLangPath(srcPath, _engine.Context);
        var result = _provider.Copy(new Copy { Context = _engine.Context, Source = src, Destination = new PLangPath(destPath, _engine.Context), Overwrite = true });

        await Assert.That(result.Success).IsTrue();
        await Assert.That(_fs.File.ReadAllText(destPath)).IsEqualTo("test content");
    }

    [Test]
    public async Task Move_DestExists_OverwriteFalse_ReturnsIOError()
    {
        var srcPath = TempFile("move_ow_src.txt");
        var destPath = TempFile("move_ow_dst.txt"); // dest already exists

        var src = new PLangPath(srcPath, _engine.Context);
        var result = _provider.Move(new Move { Context = _engine.Context, Source = src, Destination = new PLangPath(destPath, _engine.Context), Overwrite = false });

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.Key).IsEqualTo("IOError");
        await Assert.That(result.Error!.StatusCode).IsEqualTo(500);
    }

    [Test]
    public async Task Move_DestExists_OverwriteTrue_Succeeds()
    {
        var srcPath = TempFile("move_owt_src.txt");
        var destPath = _fs.Path.Combine(_tempDir, "move_owt_dst.txt");
        _fs.File.WriteAllText(destPath, "old content");

        var src = new PLangPath(srcPath, _engine.Context);
        var result = _provider.Move(new Move { Context = _engine.Context, Source = src, Destination = new PLangPath(destPath, _engine.Context), Overwrite = true });

        await Assert.That(result.Success).IsTrue();
        await Assert.That(_fs.File.ReadAllText(destPath)).IsEqualTo("test content");
        await Assert.That(_fs.File.Exists(srcPath)).IsFalse();
    }

    [Test]
    public async Task Delete_ReadOnlyParent_ReturnsIOError()
    {
        var dir = TempDir("del_ro_parent");
        var filePath = _fs.Path.Combine(dir, "locked.txt");
        _fs.File.WriteAllText(filePath, "data");

        // Make parent directory read-only (prevents deletion on Linux)
        System.IO.File.SetUnixFileMode(dir,
            System.IO.UnixFileMode.UserRead | System.IO.UnixFileMode.UserExecute);

        try
        {
            var p = new PLangPath(filePath, _engine.Context);
            var result = _provider.Delete(new Delete { Context = _engine.Context, Path = p });

            await Assert.That(result.Success).IsFalse();
            await Assert.That(result.Error!.Key).IsEqualTo("IOError");
        }
        finally
        {
            // Restore permissions for cleanup
            System.IO.File.SetUnixFileMode(dir,
                System.IO.UnixFileMode.UserRead | System.IO.UnixFileMode.UserWrite | System.IO.UnixFileMode.UserExecute);
        }
    }

    [Test]
    public async Task Save_ReadOnlyDir_ReturnsIOError()
    {
        var dir = TempDir("save_ro");
        System.IO.File.SetUnixFileMode(dir,
            System.IO.UnixFileMode.UserRead | System.IO.UnixFileMode.UserExecute);

        try
        {
            var p = new PLangPath(_fs.Path.Combine(dir, "blocked.txt"), _engine.Context);
            var result = await _provider.Save(new Save { Context = _engine.Context, Path = p, Value = Data.Ok("data") });

            await Assert.That(result.Success).IsFalse();
            await Assert.That(result.Error!.Key).IsEqualTo("IOError");
        }
        finally
        {
            System.IO.File.SetUnixFileMode(dir,
                System.IO.UnixFileMode.UserRead | System.IO.UnixFileMode.UserWrite | System.IO.UnixFileMode.UserExecute);
        }
    }

    [Test]
    public async Task Read_PermissionDenied_ReturnsIOError()
    {
        var filePath = TempFile("read_denied.txt");
        System.IO.File.SetUnixFileMode(filePath, System.IO.UnixFileMode.None);

        try
        {
            var p = new PLangPath(filePath, _engine.Context);
            var result = _provider.Read(new Read { Context = _engine.Context, Path = p });

            await Assert.That(result.Success).IsFalse();
            await Assert.That(result.Error!.Key).IsEqualTo("IOError");
        }
        finally
        {
            System.IO.File.SetUnixFileMode(filePath,
                System.IO.UnixFileMode.UserRead | System.IO.UnixFileMode.UserWrite);
        }
    }

    [Test]
    public async Task List_PermissionDenied_ReturnsIOError()
    {
        var dir = TempDir("list_denied");
        _fs.File.WriteAllText(_fs.Path.Combine(dir, "a.txt"), "a");
        System.IO.File.SetUnixFileMode(dir, System.IO.UnixFileMode.None);

        try
        {
            var p = new PLangPath(dir, _engine.Context);
            var result = _provider.List(new List { Context = _engine.Context, Path = p, Pattern = "*" });

            await Assert.That(result.Success).IsFalse();
            await Assert.That(result.Error!.Key).IsEqualTo("IOError");
        }
        finally
        {
            System.IO.File.SetUnixFileMode(dir,
                System.IO.UnixFileMode.UserRead | System.IO.UnixFileMode.UserWrite | System.IO.UnixFileMode.UserExecute);
        }
    }

    // --- Save object serialization (tester #4) ---

    [Test]
    public async Task Save_Object_SerializesToJson()
    {
        var filePath = _fs.Path.Combine(_tempDir, "saved_obj.json");
        var p = new PLangPath(filePath, _engine.Context);
        var data = new Dictionary<string, object> { ["name"] = "test", ["count"] = 42 };

        var result = await _provider.Save(new Save { Context = _engine.Context, Path = p, Value = Data.Ok(data) });

        await Assert.That(result.Success).IsTrue();
        await Assert.That(_fs.File.Exists(filePath)).IsTrue();
        var content = _fs.File.ReadAllText(filePath);
        await Assert.That(content).Contains("test");
        await Assert.That(content).Contains("42");
    }

    // --- Save serialization error (auditor v4 #1) ---

    [Test]
    public async Task Save_NonSerializableObject_ReturnsSerializationError()
    {
        var filePath = _fs.Path.Combine(_tempDir, "bad_obj.json");
        var p = new PLangPath(filePath, _engine.Context);

        // Create a circular reference — JsonSerializer will throw JsonException
        var dict = new Dictionary<string, object>();
        dict["self"] = dict;

        var result = await _provider.Save(new Save { Context = _engine.Context, Path = p, Value = Data.Ok(dict) });

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.Key).IsEqualTo("SerializationError");
        await Assert.That(result.Error!.StatusCode).IsEqualTo(500);
    }

    // --- Delete non-empty directory error has correct code ---

    [Test]
    public async Task Delete_NonEmptyDirectory_WithoutRecursive_ReturnsDirectoryNotEmpty()
    {
        var dirPath = TempDir("del_nonempty_code");
        _fs.File.WriteAllText(_fs.Path.Combine(dirPath, "child.txt"), "data");

        var p = new PLangPath(dirPath, _engine.Context);
        var result = _provider.Delete(new Delete { Context = _engine.Context, Path = p, Recursive = false });

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.Key).IsEqualTo("DirectoryNotEmpty");
        await Assert.That(result.Error!.StatusCode).IsEqualTo(400);
    }
}
