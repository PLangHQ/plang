using global::App.Actor.Context;
using App;
using global::App.Variables;
using global::App.modules.file;
using global::App.FileSystem.Default;
using PLangPath = global::App.FileSystem.Path;

namespace PLang.Tests.App.actions.file;

public class FileHandlerTests : IDisposable
{
    private readonly string _tempDir;
    private readonly PLangFileSystem _fs;
    private readonly global::App.@this _app;

    public FileHandlerTests()
    {
        _tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plang_test_" + Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(_tempDir);
        _fs = new PLangFileSystem(_tempDir, "");
        _app = new global::App.@this(_tempDir, fileSystem: _fs);
    }

    public void Dispose()
    {
        _app.DisposeAsync().AsTask().GetAwaiter().GetResult();
        if (System.IO.Directory.Exists(_tempDir))
            System.IO.Directory.Delete(_tempDir, true);
    }

    private string TempPath(string relativePath) =>
        _fs.Path.Combine(_tempDir, relativePath);

    private PLangPath MakePath(string relativePath) =>
        new PLangPath(TempPath(relativePath)) { Context = _app.Context };

    private PLangPath MakeAbsPath(string absolutePath) =>
        new PLangPath(absolutePath) { Context = _app.Context };

    // --- Save ---

    [Test]
    public async Task Save_ReturnsFileWithCorrectPaths()
    {
        var action = new Save { Context = _app.Context, Path = MakePath("test.txt"), Value = Data.Ok("hello") };
        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        var f = result.Value as PLangPath;
        await Assert.That(f).IsNotNull();
        await Assert.That(f!.Absolute).IsEqualTo(TempPath("test.txt"));
        await Assert.That(f.Relative).IsEqualTo("test.txt");
    }

    [Test]
    public async Task Save_FileExists_AfterSave()
    {
        var action = new Save { Context = _app.Context, Path = MakePath("exists.txt"), Value = Data.Ok("data") };
        await action.Run();

        await Assert.That(_fs.File.Exists(TempPath("exists.txt"))).IsTrue();
    }

    // --- Read ---

    [Test]
    public async Task Read_ReturnsFileObject()
    {
        _fs.File.WriteAllText(TempPath("read.txt"), "content here");

        var action = new Read { Context = _app.Context, Path = MakePath("read.txt") };
        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Value).IsEqualTo("content here");
    }

    [Test]
    public async Task Read_ContentIsLazy()
    {
        _fs.File.WriteAllText(TempPath("lazy.txt"), "lazy content");

        var action = new Read { Context = _app.Context, Path = MakePath("lazy.txt") };
        var result = await action.Run();

        await Assert.That(result.Value).IsEqualTo("lazy content");
    }

    [Test]
    public async Task Read_NonexistentFile_ReturnsError()
    {
        var action = new Read { Context = _app.Context, Path = MakePath("nonexistent.txt") };
        var result = await action.Run();

        await Assert.That(result.Success).IsFalse();
    }

    // --- Copy ---

    [Test]
    public async Task Copy_ReturnsFileWithSource()
    {
        _fs.File.WriteAllText(TempPath("src.txt"), "source data");

        var action = new Copy { Context = _app.Context, Source = MakePath("src.txt"), Destination = MakePath("dst.txt") };
        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        var f = result.Value as PLangPath;
        await Assert.That(f).IsNotNull();
        await Assert.That(f!.Source).IsNotNull();
        await Assert.That(f.Relative).IsEqualTo("dst.txt");
    }

    [Test]
    public async Task Copy_NonexistentSource_ReturnsFileNotFound()
    {
        var action = new Copy { Context = _app.Context, Source = MakePath("nope.txt"), Destination = MakePath("dst.txt") };
        var result = await action.Run();

        await Assert.That(result.Success).IsFalse();
    }

    [Test]
    public async Task Copy_Directory_CopiesAllFiles()
    {
        var srcDir = TempPath("copy_dir");
        _fs.Directory.CreateDirectory(srcDir);
        _fs.File.WriteAllText(_fs.Path.Combine(srcDir, "a.txt"), "a");

        var action = new Copy { Context = _app.Context, Source = MakeAbsPath(srcDir), Destination = MakePath("copy_dir_dst") };
        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        await Assert.That(_fs.File.Exists(_fs.Path.Combine(TempPath("copy_dir_dst"), "a.txt"))).IsTrue();
    }

    // --- Move ---

    [Test]
    public async Task Move_ReturnsFileWithSource()
    {
        _fs.File.WriteAllText(TempPath("move_src.txt"), "move data");

        var action = new Move { Context = _app.Context, Source = MakePath("move_src.txt"), Destination = MakePath("move_dst.txt") };
        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        var f = result.Value as PLangPath;
        await Assert.That(f).IsNotNull();
        await Assert.That(f!.Source).IsNotNull();
        await Assert.That(f.Relative).IsEqualTo("move_dst.txt");
    }

    [Test]
    public async Task Move_NonexistentSource_ReturnsFileNotFound()
    {
        var action = new Move { Context = _app.Context, Source = MakePath("nope.txt"), Destination = MakePath("dst.txt") };
        var result = await action.Run();

        await Assert.That(result.Success).IsFalse();
    }

    [Test]
    public async Task Move_Directory_MovesDirectory()
    {
        var srcDir = TempPath("move_dir");
        _fs.Directory.CreateDirectory(srcDir);
        _fs.File.WriteAllText(_fs.Path.Combine(srcDir, "a.txt"), "a");

        var action = new Move { Context = _app.Context, Source = MakeAbsPath(srcDir), Destination = MakePath("move_dir_dst") };
        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        await Assert.That(_fs.Directory.Exists(srcDir)).IsFalse();
        await Assert.That(_fs.File.Exists(_fs.Path.Combine(TempPath("move_dir_dst"), "a.txt"))).IsTrue();
    }

    // --- Delete ---

    [Test]
    public async Task Delete_ReturnsFile()
    {
        _fs.File.WriteAllText(TempPath("del.txt"), "delete me");

        var action = new Delete { Context = _app.Context, Path = MakePath("del.txt") };
        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        var f = result.Value as PLangPath;
        await Assert.That(f).IsNotNull();
        await Assert.That(f!.Exists).IsFalse();
    }

    [Test]
    public async Task Delete_NonexistentFile_ReturnsError()
    {
        var action = new Delete { Context = _app.Context, Path = MakePath("nope.txt") };
        var result = await action.Run();

        await Assert.That(result.Success).IsFalse();
    }

    [Test]
    public async Task Delete_NonexistentFile_IgnoreIfNotFound_ReturnsSuccess()
    {
        var action = new Delete { Context = _app.Context, Path = MakePath("nope.txt"), IgnoreIfNotFound = true };
        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
    }

    [Test]
    public async Task Delete_Directory_Recursive()
    {
        var dir = TempPath("del_dir");
        _fs.Directory.CreateDirectory(dir);
        _fs.File.WriteAllText(_fs.Path.Combine(dir, "child.txt"), "data");

        var action = new Delete { Context = _app.Context, Path = MakeAbsPath(dir), Recursive = true };
        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        await Assert.That(_fs.Directory.Exists(dir)).IsFalse();
    }

    // --- Exists ---

    [Test]
    public async Task Exists_ExistingFile_ReturnsTrue()
    {
        _fs.File.WriteAllText(TempPath("check.txt"), "present");

        var action = new Exists { Context = _app.Context, Path = MakePath("check.txt") };
        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        var f = result.Value as PLangPath;
        await Assert.That(f).IsNotNull();
        await Assert.That(f!.Exists).IsTrue();
    }

    [Test]
    public async Task Exists_NonexistentFile_ReturnsFalse()
    {
        var action = new Exists { Context = _app.Context, Path = MakePath("missing.txt") };
        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        var f = result.Value as PLangPath;
        await Assert.That(f).IsNotNull();
        await Assert.That(f!.Exists).IsFalse();
    }

    // --- List ---

    [Test]
    public async Task List_ReturnsFileArray()
    {
        var subDir = TempPath("listdir");
        _fs.Directory.CreateDirectory(subDir);
        _fs.File.WriteAllText(_fs.Path.Combine(subDir, "a.txt"), "a");
        _fs.File.WriteAllText(_fs.Path.Combine(subDir, "b.txt"), "b");

        var action = new List { Context = _app.Context, Path = MakeAbsPath(subDir) };
        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        var files = result.Value as PLangPath[];
        await Assert.That(files).IsNotNull();
        await Assert.That(files!.Length).IsEqualTo(2);
    }

    [Test]
    public async Task List_WithPattern_FiltersFiles()
    {
        var subDir = TempPath("listpattern");
        _fs.Directory.CreateDirectory(subDir);
        _fs.File.WriteAllText(_fs.Path.Combine(subDir, "a.txt"), "a");
        _fs.File.WriteAllText(_fs.Path.Combine(subDir, "b.md"), "b");

        var action = new List { Context = _app.Context, Path = MakeAbsPath(subDir), Pattern = "*.txt" };
        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        var files = result.Value as PLangPath[];
        await Assert.That(files).IsNotNull();
        await Assert.That(files!.Length).IsEqualTo(1);
    }

    [Test]
    public async Task List_Recursive_FindsNestedFiles()
    {
        var subDir = TempPath("listrecursive");
        var nested = _fs.Path.Combine(subDir, "sub");
        _fs.Directory.CreateDirectory(subDir);
        _fs.Directory.CreateDirectory(nested);
        _fs.File.WriteAllText(_fs.Path.Combine(subDir, "top.txt"), "top");
        _fs.File.WriteAllText(_fs.Path.Combine(nested, "deep.txt"), "deep");

        var action = new List { Context = _app.Context, Path = MakeAbsPath(subDir), Recursive = true };
        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        var files = result.Value as PLangPath[];
        await Assert.That(files).IsNotNull();
        await Assert.That(files!.Length).IsEqualTo(2);
    }

    [Test]
    public async Task List_NonexistentDirectory_ReturnsError()
    {
        var action = new List { Context = _app.Context, Path = MakePath("nodir") };
        var result = await action.Run();

        await Assert.That(result.Success).IsFalse();
    }

    // --- File type object properties ---

    [Test]
    public async Task FileType_MimeType_FromExtension()
    {
        _fs.File.WriteAllText(TempPath("doc.md"), "# Hello");

        var action = new Read { Context = _app.Context, Path = MakePath("doc.md") };
        var result = await action.Run();

        await Assert.That(result.Type!.Value).IsEqualTo("text/markdown");
    }

    [Test]
    public async Task FileType_Size_IsLazy()
    {
        _fs.File.WriteAllText(TempPath("sized.txt"), "12345");

        var action = new Read { Context = _app.Context, Path = MakePath("sized.txt") };
        var result = await action.Run();
        var content = result.Value as string;

        await Assert.That(content!.Length).IsEqualTo(5);
    }

    [Test]
    public async Task FileType_ToString_ReturnsContent()
    {
        _fs.File.WriteAllText(TempPath("tostring.txt"), "file-content");

        var action = new Read { Context = _app.Context, Path = MakePath("tostring.txt") };
        var result = await action.Run();

        await Assert.That(result.Value!.ToString()).IsEqualTo("file-content");
    }

    // --- Integration: file.exists -> Variables -> output.write ---

    [Test]
    public async Task Integration_FileExists_FlowsThroughVariables_ToOutput()
    {
        _fs.File.WriteAllText(TempPath("real.txt"), "I exist");

        var captureStream = new System.IO.MemoryStream();
        _app.User.Channels.Register(new Channel(
            EngineChannels.Default, captureStream,
            ChannelDirection.Output, ownsStream: true)
        { ContentType = "text/plain" });

        var goal = new global::App.Goals.Goal.@this
        {
            Name = "TestFileExistsFlow",
            Steps = new global::App.Goals.Goal.Steps.@this
            {
                new global::App.Goals.Goal.Steps.Step.@this
                {
                    Index = 0,
                    Text = "check if file exists",
                    Actions = new global::App.Goals.Goal.Steps.Step.Actions.@this
                    {
                        new global::App.Goals.Goal.Steps.Step.Actions.Action.@this
                        {
                            Module = "file",
                            ActionName = "exists",
                            Parameters = new System.Collections.Generic.List<global::App.Data.@this>
                                { new global::App.Data.@this("path", TempPath("real.txt")) },
                            Return = new System.Collections.Generic.List<global::App.Data.@this>
                                { new global::App.Data.@this("fileResult") }
                        }
                    }
                },
                new global::App.Goals.Goal.Steps.Step.@this
                {
                    Index = 1,
                    Text = "write exists result",
                    Actions = new global::App.Goals.Goal.Steps.Step.Actions.@this
                    {
                        new global::App.Goals.Goal.Steps.Step.Actions.Action.@this
                        {
                            Module = "output",
                            ActionName = "write",
                            Parameters = new System.Collections.Generic.List<global::App.Data.@this>
                                { new global::App.Data.@this("Data", "%fileResult.Exists%") },
                        }
                    }
                }
            }
        };

        var context = _app.Context;
        var goalResult = await _app.RunGoalAsync(goal, context);

        await Assert.That(goalResult.Success).IsTrue();

        var fileData = context.Variables.Get("fileResult");
        await Assert.That(fileData).IsNotNull();
        var fileObj = fileData!.Value as PLangPath;
        await Assert.That(fileObj).IsNotNull();
        await Assert.That(fileObj!.Exists).IsTrue();

        var existsData = context.Variables.Get("fileResult.Exists");
        await Assert.That(existsData).IsNotNull();
        await Assert.That(existsData!.Value).IsEqualTo(true);

        captureStream.Position = 0;
        var output = new System.IO.StreamReader(captureStream).ReadToEnd();
        await Assert.That(output).IsEqualTo("True" + System.Environment.NewLine);
    }

    [Test]
    public async Task Integration_FileNotExists_FlowsThroughVariables_ToOutput()
    {
        var captureStream = new System.IO.MemoryStream();
        _app.User.Channels.Register(new Channel(
            EngineChannels.Default, captureStream,
            ChannelDirection.Output, ownsStream: true)
        { ContentType = "text/plain" });

        var goal = new global::App.Goals.Goal.@this
        {
            Name = "TestFileNotExistsFlow",
            Steps = new global::App.Goals.Goal.Steps.@this
            {
                new global::App.Goals.Goal.Steps.Step.@this
                {
                    Index = 0,
                    Text = "check if file exists",
                    Actions = new global::App.Goals.Goal.Steps.Step.Actions.@this
                    {
                        new global::App.Goals.Goal.Steps.Step.Actions.Action.@this
                        {
                            Module = "file",
                            ActionName = "exists",
                            Parameters = new System.Collections.Generic.List<global::App.Data.@this>
                                { new global::App.Data.@this("path", TempPath("ghost.txt")) },
                            Return = new System.Collections.Generic.List<global::App.Data.@this>
                                { new global::App.Data.@this("fileResult") }
                        }
                    }
                },
                new global::App.Goals.Goal.Steps.Step.@this
                {
                    Index = 1,
                    Text = "write exists result",
                    Actions = new global::App.Goals.Goal.Steps.Step.Actions.@this
                    {
                        new global::App.Goals.Goal.Steps.Step.Actions.Action.@this
                        {
                            Module = "output",
                            ActionName = "write",
                            Parameters = new System.Collections.Generic.List<global::App.Data.@this>
                                { new global::App.Data.@this("Data", "%fileResult.Exists%") },
                        }
                    }
                }
            }
        };

        var context = _app.Context;
        var goalResult = await _app.RunGoalAsync(goal, context);

        await Assert.That(goalResult.Success).IsTrue();

        var existsData = context.Variables.Get("fileResult.Exists");
        await Assert.That(existsData).IsNotNull();
        await Assert.That(existsData!.Value).IsEqualTo(false);

        captureStream.Position = 0;
        var output = new System.IO.StreamReader(captureStream).ReadToEnd();
        await Assert.That(output).IsEqualTo("False" + System.Environment.NewLine);
    }
}
