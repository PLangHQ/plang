using PLang.SafeFileSystem;

namespace PLang.Tests.Runtime2.Memory;

public class PathTests : IDisposable
{
    private readonly string _tempDir;
    private readonly PLangFileSystem _fs;

    public PathTests()
    {
        _tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plang_path_test_" + Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(_tempDir);
        _fs = new PLangFileSystem(_tempDir, "");
    }

    public void Dispose()
    {
        if (System.IO.Directory.Exists(_tempDir))
            System.IO.Directory.Delete(_tempDir, true);
    }

    private string TempFile(string name)
    {
        var path = System.IO.Path.Combine(_tempDir, name);
        System.IO.File.WriteAllText(path, "test content");
        return path;
    }

    // --- Constructor & Absolute ---

    [Test]
    public async Task Absolute_ResolvesRelativePath()
    {
        var p = new PLangPath("config.json", _fs);
        var expected = _fs.Path.GetFullPath("config.json");
        await Assert.That(p.Absolute).IsEqualTo(expected);
    }

    [Test]
    public async Task Absolute_PreservesAbsolutePath()
    {
        var abs = System.IO.Path.Combine(_tempDir, "test.txt");
        var p = new PLangPath(abs, _fs);
        await Assert.That(p.Absolute).IsEqualTo(abs);
    }

    // --- Raw ---

    [Test]
    public async Task Raw_PreservesOriginalInput()
    {
        var p = new PLangPath("relative/file.txt", _fs);
        await Assert.That(p.Raw).IsEqualTo("relative/file.txt");
    }

    // --- Relative ---

    [Test]
    public async Task Relative_StripsRootDirectory()
    {
        var abs = System.IO.Path.Combine(_tempDir, "sub", "file.txt");
        var p = new PLangPath(abs, _fs);
        var rel = p.Relative;
        await Assert.That(rel.Contains(_tempDir)).IsFalse();
        await Assert.That(rel).Contains("file.txt");
    }

    // --- Extension ---

    [Test]
    public async Task Extension_ReturnsWithDot()
    {
        var p = new PLangPath("config.json", _fs);
        await Assert.That(p.Extension).IsEqualTo(".json");
    }

    [Test]
    public async Task Extension_NoExtension_ReturnsEmpty()
    {
        var p = new PLangPath("Makefile", _fs);
        await Assert.That(p.Extension).IsEqualTo("");
    }

    // --- FileName ---

    [Test]
    public async Task FileName_ReturnsFileNameWithExtension()
    {
        var p = new PLangPath(System.IO.Path.Combine(_tempDir, "sub", "config.json"), _fs);
        await Assert.That(p.FileName).IsEqualTo("config.json");
    }

    // --- FileNameWithoutExtension ---

    [Test]
    public async Task FileNameWithoutExtension_ReturnsNameOnly()
    {
        var p = new PLangPath("archive.tar.gz", _fs);
        await Assert.That(p.FileNameWithoutExtension).IsEqualTo("archive.tar");
    }

    // --- Directory ---

    [Test]
    public async Task Directory_ReturnsParentPath()
    {
        var p = new PLangPath(System.IO.Path.Combine(_tempDir, "sub", "file.txt"), _fs);
        var expected = System.IO.Path.Combine(_tempDir, "sub");
        await Assert.That(p.Directory).IsEqualTo(expected);
    }

    // --- MimeType ---

    [Test]
    public async Task MimeType_Json()
    {
        var p = new PLangPath("data.json", _fs);
        await Assert.That(p.MimeType).IsEqualTo("application/json");
    }

    [Test]
    public async Task MimeType_Markdown()
    {
        var p = new PLangPath("README.md", _fs);
        await Assert.That(p.MimeType).IsEqualTo("text/markdown");
    }

    [Test]
    public async Task MimeType_Unknown_ReturnsOctetStream()
    {
        var p = new PLangPath("data.xyz", _fs);
        await Assert.That(p.MimeType).IsEqualTo("application/octet-stream");
    }

    // --- IsFile ---

    [Test]
    public async Task IsFile_ExistingFile_ReturnsTrue()
    {
        var filePath = TempFile("exists.txt");
        var p = new PLangPath(filePath, _fs);
        await Assert.That(p.IsFile).IsTrue();
    }

    [Test]
    public async Task IsFile_NonexistentFile_ReturnsFalse()
    {
        var p = new PLangPath(System.IO.Path.Combine(_tempDir, "nope.txt"), _fs);
        await Assert.That(p.IsFile).IsFalse();
    }

    // --- IsDirectory ---

    [Test]
    public async Task IsDirectory_ExistingDir_ReturnsTrue()
    {
        var p = new PLangPath(_tempDir, _fs);
        await Assert.That(p.IsDirectory).IsTrue();
    }

    [Test]
    public async Task IsDirectory_File_ReturnsFalse()
    {
        var filePath = TempFile("notdir.txt");
        var p = new PLangPath(filePath, _fs);
        await Assert.That(p.IsDirectory).IsFalse();
    }

    // --- Exists ---

    [Test]
    public async Task Exists_ExistingFile_ReturnsTrue()
    {
        var filePath = TempFile("there.txt");
        var p = new PLangPath(filePath, _fs);
        await Assert.That(p.Exists).IsTrue();
    }

    [Test]
    public async Task Exists_ExistingDir_ReturnsTrue()
    {
        var p = new PLangPath(_tempDir, _fs);
        await Assert.That(p.Exists).IsTrue();
    }

    [Test]
    public async Task Exists_Nothing_ReturnsFalse()
    {
        var p = new PLangPath(System.IO.Path.Combine(_tempDir, "phantom"), _fs);
        await Assert.That(p.Exists).IsFalse();
    }

    // --- Size ---

    [Test]
    public async Task Size_ExistingFile_ReturnsCorrectSize()
    {
        var filePath = System.IO.Path.Combine(_tempDir, "sized.txt");
        System.IO.File.WriteAllText(filePath, "12345");
        var p = new PLangPath(filePath, _fs);
        await Assert.That(p.Size).IsEqualTo(5);
    }

    [Test]
    public async Task Size_NonexistentFile_ReturnsZero()
    {
        var p = new PLangPath(System.IO.Path.Combine(_tempDir, "nosize.txt"), _fs);
        await Assert.That(p.Size).IsEqualTo(0);
    }

    // --- ToString ---

    [Test]
    public async Task ToString_ReturnsAbsolutePath()
    {
        var abs = System.IO.Path.Combine(_tempDir, "test.txt");
        var p = new PLangPath(abs, _fs);
        await Assert.That(p.ToString()).IsEqualTo(abs);
    }

    // --- Equality ---

    [Test]
    public async Task Equals_SamePath_ReturnsTrue()
    {
        var abs = System.IO.Path.Combine(_tempDir, "same.txt");
        var p1 = new PLangPath(abs, _fs);
        var p2 = new PLangPath(abs, _fs);
        await Assert.That(p1.Equals(p2)).IsTrue();
    }

    [Test]
    public async Task Equals_String_ReturnsTrue()
    {
        var abs = System.IO.Path.Combine(_tempDir, "str.txt");
        var p = new PLangPath(abs, _fs);
        await Assert.That(p.Equals(abs)).IsTrue();
    }

    [Test]
    public async Task Equals_DifferentPath_ReturnsFalse()
    {
        var p1 = new PLangPath(System.IO.Path.Combine(_tempDir, "a.txt"), _fs);
        var p2 = new PLangPath(System.IO.Path.Combine(_tempDir, "b.txt"), _fs);
        await Assert.That(p1.Equals(p2)).IsFalse();
    }

    // --- Resolve (engine-resolvable) ---

    [Test]
    public async Task Resolve_CreatesPathViaEngine()
    {
        var engine = new Engine(_tempDir, fileSystem: _fs);
        var p = PLangPath.Resolve("test.txt", engine);
        await Assert.That(p).IsNotNull();
        await Assert.That(p.FileName).IsEqualTo("test.txt");
        await engine.DisposeAsync();
    }

    // --- IsFile is live (not cached) ---

    [Test]
    public async Task IsFile_BecomesTrue_AfterFileCreated()
    {
        var filePath = System.IO.Path.Combine(_tempDir, "later.txt");
        var p = new PLangPath(filePath, _fs);

        await Assert.That(p.IsFile).IsFalse();

        System.IO.File.WriteAllText(filePath, "created");
        await Assert.That(p.IsFile).IsTrue();
    }

    // --- Cached properties are stable ---

    [Test]
    public async Task CachedProperties_SameOnMultipleAccess()
    {
        var p = new PLangPath(System.IO.Path.Combine(_tempDir, "stable.json"), _fs);
        var ext1 = p.Extension;
        var ext2 = p.Extension;
        var name1 = p.FileName;
        var name2 = p.FileName;

        await Assert.That(ext1).IsEqualTo(ext2);
        await Assert.That(name1).IsEqualTo(name2);
    }

    // --- Helper for directories ---

    private string TempDir(string name)
    {
        var path = System.IO.Path.Combine(_tempDir, name);
        System.IO.Directory.CreateDirectory(path);
        return path;
    }

    // --- Copy ---

    [Test]
    public async Task Copy_File_CopiesToDestination()
    {
        var srcPath = TempFile("copy_src.txt");
        var destPath = System.IO.Path.Combine(_tempDir, "copy_dst.txt");

        var src = new PLangPath(srcPath, _fs);
        var dest = new PLangPath(destPath, _fs);
        var result = src.Copy(dest);

        await Assert.That(result.Success).IsTrue();
        await Assert.That(System.IO.File.Exists(destPath)).IsTrue();
        await Assert.That(System.IO.File.ReadAllText(destPath)).IsEqualTo("test content");
    }

    [Test]
    public async Task Copy_Directory_CopiesAllFiles()
    {
        var srcDir = TempDir("copy_dir_src");
        System.IO.File.WriteAllText(System.IO.Path.Combine(srcDir, "a.txt"), "aaa");
        System.IO.File.WriteAllText(System.IO.Path.Combine(srcDir, "b.txt"), "bbb");

        var destDir = System.IO.Path.Combine(_tempDir, "copy_dir_dst");
        var src = new PLangPath(srcDir, _fs);
        var dest = new PLangPath(destDir, _fs);
        var result = src.Copy(dest);

        await Assert.That(result.Success).IsTrue();
        await Assert.That(System.IO.File.Exists(System.IO.Path.Combine(destDir, "a.txt"))).IsTrue();
        await Assert.That(System.IO.File.Exists(System.IO.Path.Combine(destDir, "b.txt"))).IsTrue();
    }

    [Test]
    public async Task Copy_Directory_WithSubfolders()
    {
        var srcDir = TempDir("copy_sub_src");
        var nested = System.IO.Path.Combine(srcDir, "sub");
        System.IO.Directory.CreateDirectory(nested);
        System.IO.File.WriteAllText(System.IO.Path.Combine(srcDir, "top.txt"), "top");
        System.IO.File.WriteAllText(System.IO.Path.Combine(nested, "deep.txt"), "deep");

        var destDir = System.IO.Path.Combine(_tempDir, "copy_sub_dst");
        var src = new PLangPath(srcDir, _fs);
        var dest = new PLangPath(destDir, _fs);
        var result = src.Copy(dest, includeSubfolders: true);

        await Assert.That(result.Success).IsTrue();
        await Assert.That(System.IO.File.Exists(System.IO.Path.Combine(destDir, "top.txt"))).IsTrue();
        await Assert.That(System.IO.File.Exists(System.IO.Path.Combine(destDir, "sub", "deep.txt"))).IsTrue();
    }

    [Test]
    public async Task Copy_Directory_WithoutSubfolders()
    {
        var srcDir = TempDir("copy_nosub_src");
        var nested = System.IO.Path.Combine(srcDir, "sub");
        System.IO.Directory.CreateDirectory(nested);
        System.IO.File.WriteAllText(System.IO.Path.Combine(srcDir, "top.txt"), "top");
        System.IO.File.WriteAllText(System.IO.Path.Combine(nested, "deep.txt"), "deep");

        var destDir = System.IO.Path.Combine(_tempDir, "copy_nosub_dst");
        var src = new PLangPath(srcDir, _fs);
        var dest = new PLangPath(destDir, _fs);
        var result = src.Copy(dest, includeSubfolders: false);

        await Assert.That(result.Success).IsTrue();
        await Assert.That(System.IO.File.Exists(System.IO.Path.Combine(destDir, "top.txt"))).IsTrue();
        await Assert.That(System.IO.Directory.Exists(System.IO.Path.Combine(destDir, "sub"))).IsFalse();
    }

    [Test]
    public async Task Copy_NotFound_ReturnsError()
    {
        var src = new PLangPath(System.IO.Path.Combine(_tempDir, "ghost.txt"), _fs);
        var dest = new PLangPath(System.IO.Path.Combine(_tempDir, "dst.txt"), _fs);
        var result = src.Copy(dest);

        await Assert.That(result.Success).IsFalse();
    }

    // --- Move ---

    [Test]
    public async Task Move_File_MovesToDestination()
    {
        var srcPath = TempFile("move_src.txt");
        var destPath = System.IO.Path.Combine(_tempDir, "move_dst.txt");

        var src = new PLangPath(srcPath, _fs);
        var dest = new PLangPath(destPath, _fs);
        var result = src.Move(dest);

        await Assert.That(result.Success).IsTrue();
        await Assert.That(System.IO.File.Exists(destPath)).IsTrue();
        await Assert.That(System.IO.File.Exists(srcPath)).IsFalse();
    }

    [Test]
    public async Task Move_Directory_MovesToDestination()
    {
        var srcDir = TempDir("move_dir_src");
        System.IO.File.WriteAllText(System.IO.Path.Combine(srcDir, "a.txt"), "aaa");

        var destDir = System.IO.Path.Combine(_tempDir, "move_dir_dst");
        var src = new PLangPath(srcDir, _fs);
        var dest = new PLangPath(destDir, _fs);
        var result = src.Move(dest);

        await Assert.That(result.Success).IsTrue();
        await Assert.That(System.IO.Directory.Exists(destDir)).IsTrue();
        await Assert.That(System.IO.Directory.Exists(srcDir)).IsFalse();
        await Assert.That(System.IO.File.Exists(System.IO.Path.Combine(destDir, "a.txt"))).IsTrue();
    }

    [Test]
    public async Task Move_NotFound_ReturnsError()
    {
        var src = new PLangPath(System.IO.Path.Combine(_tempDir, "ghost.txt"), _fs);
        var dest = new PLangPath(System.IO.Path.Combine(_tempDir, "dst.txt"), _fs);
        var result = src.Move(dest);

        await Assert.That(result.Success).IsFalse();
    }

    // --- Delete ---

    [Test]
    public async Task Delete_File_RemovesFile()
    {
        var filePath = TempFile("del_file.txt");
        var p = new PLangPath(filePath, _fs);
        var result = p.Delete();

        await Assert.That(result.Success).IsTrue();
        await Assert.That(System.IO.File.Exists(filePath)).IsFalse();
    }

    [Test]
    public async Task Delete_EmptyDirectory_RemovesIt()
    {
        var dirPath = TempDir("del_empty_dir");
        var p = new PLangPath(dirPath, _fs);
        var result = p.Delete();

        await Assert.That(result.Success).IsTrue();
        await Assert.That(System.IO.Directory.Exists(dirPath)).IsFalse();
    }

    [Test]
    public async Task Delete_DirectoryRecursive_RemovesAll()
    {
        var dirPath = TempDir("del_recursive_dir");
        System.IO.File.WriteAllText(System.IO.Path.Combine(dirPath, "child.txt"), "data");

        var p = new PLangPath(dirPath, _fs);
        var result = p.Delete(recursive: true);

        await Assert.That(result.Success).IsTrue();
        await Assert.That(System.IO.Directory.Exists(dirPath)).IsFalse();
    }

    [Test]
    public async Task Delete_NotFound_ReturnsError()
    {
        var p = new PLangPath(System.IO.Path.Combine(_tempDir, "ghost.txt"), _fs);
        var result = p.Delete();

        await Assert.That(result.Success).IsFalse();
    }
}
