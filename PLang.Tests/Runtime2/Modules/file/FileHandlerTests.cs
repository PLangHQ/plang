using PLang.Runtime2.Context;
using PLang.Runtime2.Core;
using PLang.Runtime2.Memory;
using PLang.Runtime2.modules.file;
using PLang.SafeFileSystem;
using FileResult = PLang.Runtime2.modules.file.types.file;

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

    private PLangContext CreateContext()
    {
        var context = _engine.CreateContext();
        context.RegisterContextVariables(_engine);
        return context;
    }

    // --- Save ---

    [Test]
    public async Task Save_ReturnsFileWithCorrectPaths()
    {
        var action = new Save { Context = CreateContext(), Path = TempPath("test.txt"), Value = "hello" };
        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        var f = result.Value as FileResult;
        await Assert.That(f).IsNotNull();
        await Assert.That(f!.AbsolutePath).IsEqualTo(TempPath("test.txt"));
        await Assert.That(f.Path).IsEqualTo("test.txt");
    }

    [Test]
    public async Task Save_FileExists_AfterSave()
    {
        var action = new Save { Context = CreateContext(), Path = TempPath("exists.txt"), Value = "data" };
        await action.Run();

        await Assert.That(System.IO.File.Exists(TempPath("exists.txt"))).IsTrue();
    }

    // --- Read ---

    [Test]
    public async Task Read_ReturnsFileObject()
    {
        System.IO.File.WriteAllText(TempPath("read.txt"), "content here");

        var action = new Read { Context = CreateContext(), Path = TempPath("read.txt") };
        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        var f = result.Value as FileResult;
        await Assert.That(f).IsNotNull();
        await Assert.That(f!.Path).IsEqualTo("read.txt");
    }

    [Test]
    public async Task Read_ContentIsLazy()
    {
        System.IO.File.WriteAllText(TempPath("lazy.txt"), "lazy content");

        var action = new Read { Context = CreateContext(), Path = TempPath("lazy.txt") };
        var result = await action.Run();
        var f = result.Value as FileResult;

        await Assert.That(f!.Value.Value).IsEqualTo("lazy content");
    }

    [Test]
    public async Task Read_NonexistentFile_ReturnsError()
    {
        var action = new Read { Context = CreateContext(), Path = TempPath("nonexistent.txt") };
        var result = await action.Run();

        await Assert.That(result.Success).IsFalse();
    }

    // --- Copy ---

    [Test]
    public async Task Copy_ReturnsFileWithSource()
    {
        System.IO.File.WriteAllText(TempPath("src.txt"), "source data");

        var action = new Copy { Context = CreateContext(), Source = TempPath("src.txt"), Destination = TempPath("dst.txt") };
        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        var f = result.Value as FileResult;
        await Assert.That(f).IsNotNull();
        await Assert.That(f!.Source).IsNotNull();
        await Assert.That(f.Path).IsEqualTo("dst.txt");
    }

    [Test]
    public async Task Copy_NonexistentSource_ReturnsFileNotFound()
    {
        var action = new Copy { Context = CreateContext(), Source = TempPath("nope.txt"), Destination = TempPath("dst.txt") };
        var result = await action.Run();

        await Assert.That(result.Success).IsFalse();
    }

    // --- Move ---

    [Test]
    public async Task Move_ReturnsFileWithSource()
    {
        System.IO.File.WriteAllText(TempPath("move_src.txt"), "move data");

        var action = new Move { Context = CreateContext(), Source = TempPath("move_src.txt"), Destination = TempPath("move_dst.txt") };
        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        var f = result.Value as FileResult;
        await Assert.That(f).IsNotNull();
        await Assert.That(f!.Source).IsNotNull();
        await Assert.That(f.Path).IsEqualTo("move_dst.txt");
    }

    [Test]
    public async Task Move_NonexistentSource_ReturnsFileNotFound()
    {
        var action = new Move { Context = CreateContext(), Source = TempPath("nope.txt"), Destination = TempPath("dst.txt") };
        var result = await action.Run();

        await Assert.That(result.Success).IsFalse();
    }

    // --- Delete ---

    [Test]
    public async Task Delete_ReturnsFile()
    {
        System.IO.File.WriteAllText(TempPath("del.txt"), "delete me");

        var action = new Delete { Context = CreateContext(), Path = TempPath("del.txt") };
        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        var f = result.Value as FileResult;
        await Assert.That(f).IsNotNull();
        await Assert.That(f!.Exists).IsFalse();
    }

    [Test]
    public async Task Delete_NonexistentFile_ReturnsError()
    {
        var action = new Delete { Context = CreateContext(), Path = TempPath("nope.txt") };
        var result = await action.Run();

        await Assert.That(result.Success).IsFalse();
    }

    [Test]
    public async Task Delete_NonexistentFile_IgnoreIfNotFound_ReturnsSuccess()
    {
        var action = new Delete { Context = CreateContext(), Path = TempPath("nope.txt"), IgnoreIfNotFound = true };
        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
    }

    // --- Exists ---

    [Test]
    public async Task Exists_ExistingFile_ReturnsTrue()
    {
        System.IO.File.WriteAllText(TempPath("check.txt"), "present");

        var action = new Exists { Context = CreateContext(), Path = TempPath("check.txt") };
        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        var f = result.Value as FileResult;
        await Assert.That(f).IsNotNull();
        await Assert.That(f!.Exists).IsTrue();
    }

    [Test]
    public async Task Exists_NonexistentFile_ReturnsFalse()
    {
        var action = new Exists { Context = CreateContext(), Path = TempPath("missing.txt") };
        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        var f = result.Value as FileResult;
        await Assert.That(f).IsNotNull();
        await Assert.That(f!.Exists).IsFalse();
    }

    // --- List ---

    [Test]
    public async Task List_ReturnsFileArray()
    {
        var subDir = TempPath("listdir");
        System.IO.Directory.CreateDirectory(subDir);
        System.IO.File.WriteAllText(System.IO.Path.Combine(subDir, "a.txt"), "a");
        System.IO.File.WriteAllText(System.IO.Path.Combine(subDir, "b.txt"), "b");

        var action = new List { Context = CreateContext(), Path = subDir };
        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        var files = result.Value as FileResult[];
        await Assert.That(files).IsNotNull();
        await Assert.That(files!.Length).IsEqualTo(2);
    }

    [Test]
    public async Task List_WithPattern_FiltersFiles()
    {
        var subDir = TempPath("listpattern");
        System.IO.Directory.CreateDirectory(subDir);
        System.IO.File.WriteAllText(System.IO.Path.Combine(subDir, "a.txt"), "a");
        System.IO.File.WriteAllText(System.IO.Path.Combine(subDir, "b.md"), "b");

        var action = new List { Context = CreateContext(), Path = subDir, Pattern = "*.txt" };
        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        var files = result.Value as FileResult[];
        await Assert.That(files).IsNotNull();
        await Assert.That(files!.Length).IsEqualTo(1);
    }

    [Test]
    public async Task List_Recursive_FindsNestedFiles()
    {
        var subDir = TempPath("listrecursive");
        var nested = System.IO.Path.Combine(subDir, "sub");
        System.IO.Directory.CreateDirectory(nested);
        System.IO.File.WriteAllText(System.IO.Path.Combine(subDir, "top.txt"), "top");
        System.IO.File.WriteAllText(System.IO.Path.Combine(nested, "deep.txt"), "deep");

        var action = new List { Context = CreateContext(), Path = subDir, Recursive = true };
        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        var files = result.Value as FileResult[];
        await Assert.That(files).IsNotNull();
        await Assert.That(files!.Length).IsEqualTo(2);
    }

    [Test]
    public async Task List_NonexistentDirectory_ReturnsError()
    {
        var action = new List { Context = CreateContext(), Path = TempPath("nodir") };
        var result = await action.Run();

        await Assert.That(result.Success).IsFalse();
    }

    // --- File type object properties ---

    [Test]
    public async Task FileType_MimeType_FromExtension()
    {
        System.IO.File.WriteAllText(TempPath("doc.md"), "# Hello");

        var action = new Read { Context = CreateContext(), Path = TempPath("doc.md") };
        var result = await action.Run();
        var f = result.Value as FileResult;

        await Assert.That(f!.Type).IsEqualTo("text/markdown");
    }

    [Test]
    public async Task FileType_Size_IsLazy()
    {
        System.IO.File.WriteAllText(TempPath("sized.txt"), "12345");

        var action = new Read { Context = CreateContext(), Path = TempPath("sized.txt") };
        var result = await action.Run();
        var f = result.Value as FileResult;

        await Assert.That(f!.Size).IsEqualTo(5);
    }

    [Test]
    public async Task FileType_ToString_ReturnsRelativePath()
    {
        System.IO.File.WriteAllText(TempPath("tostring.txt"), "x");

        var action = new Read { Context = CreateContext(), Path = TempPath("tostring.txt") };
        var result = await action.Run();
        var f = result.Value as FileResult;

        await Assert.That(f!.ToString()).IsEqualTo("tostring.txt");
    }

    // --- Integration: file.exists → MemoryStack → output.write ---

    [Test]
    public async Task Integration_FileExists_FlowsThroughMemoryStack_ToOutput()
    {
        // Arrange: create a real file
        System.IO.File.WriteAllText(TempPath("real.txt"), "I exist");

        // Build engine with built-in modules
        _engine.RegisterBuiltInModules();

        // Replace default channel on User actor's IO so we can capture output
        var captureStream = new System.IO.MemoryStream();
        _engine.User.IO.Register(new PLang.Runtime2.IO.Channel(
            PLang.Runtime2.IO.IO.Default, captureStream,
            PLang.Runtime2.IO.ChannelDirection.Output, ownsStream: true)
        { ContentType = "text/plain" });

        // Build a goal: step 1 = file.exists, step 2 = output.write %fileResult.Exists%
        var goal = new PLang.Runtime2.Core.Goal
        {
            Name = "TestFileExistsFlow",
            Steps = new PLang.Runtime2.Core.Steps
            {
                new PLang.Runtime2.Core.Step
                {
                    Index = 0,
                    Text = "check if file exists",
                    Actions = new PLang.Runtime2.Core.Actions
                    {
                        new PLang.Runtime2.Core.Action
                        {
                            Module = "file",
                            ActionName = "exists",
                            Parameters = new System.Collections.Generic.List<PLang.Runtime2.Memory.Data>
                                { new PLang.Runtime2.Memory.Data("path", TempPath("real.txt")) },
                            Return = new System.Collections.Generic.List<PLang.Runtime2.Memory.Data>
                                { new PLang.Runtime2.Memory.Data("fileResult") }
                        }
                    }
                },
                new PLang.Runtime2.Core.Step
                {
                    Index = 1,
                    Text = "write exists result",
                    Actions = new PLang.Runtime2.Core.Actions
                    {
                        new PLang.Runtime2.Core.Action
                        {
                            Module = "output",
                            ActionName = "write",
                            Parameters = new System.Collections.Generic.List<PLang.Runtime2.Memory.Data>
                                { new PLang.Runtime2.Memory.Data("content", "%fileResult.Exists%") },
                        }
                    }
                }
            }
        };

        // Act: run the goal through the engine
        var context = _engine.CreateContext();
        var goalResult = await _engine.RunGoalAsync(goal, context);

        // Assert: goal succeeded
        await Assert.That(goalResult.Success).IsTrue();

        // Assert: fileResult variable is in memory and is a @file object
        var fileData = context.MemoryStack.Get("fileResult");
        await Assert.That(fileData).IsNotNull();
        var fileObj = fileData!.Value as FileResult;
        await Assert.That(fileObj).IsNotNull();
        await Assert.That(fileObj!.Exists).IsTrue();

        // Assert: the lazy %fileResult.Exists% resolved through MemoryStack dot-notation
        var existsData = context.MemoryStack.Get("fileResult.Exists");
        await Assert.That(existsData).IsNotNull();
        await Assert.That(existsData!.Value).IsEqualTo(true);

        // Assert: output.write wrote "True" to our capture stream
        captureStream.Position = 0;
        var output = new System.IO.StreamReader(captureStream).ReadToEnd();
        await Assert.That(output).IsEqualTo("True");
    }

    [Test]
    public async Task Integration_FileNotExists_FlowsThroughMemoryStack_ToOutput()
    {
        // Arrange: NO file created — path doesn't exist

        _engine.RegisterBuiltInModules();

        var captureStream = new System.IO.MemoryStream();
        _engine.User.IO.Register(new PLang.Runtime2.IO.Channel(
            PLang.Runtime2.IO.IO.Default, captureStream,
            PLang.Runtime2.IO.ChannelDirection.Output, ownsStream: true)
        { ContentType = "text/plain" });

        var goal = new PLang.Runtime2.Core.Goal
        {
            Name = "TestFileNotExistsFlow",
            Steps = new PLang.Runtime2.Core.Steps
            {
                new PLang.Runtime2.Core.Step
                {
                    Index = 0,
                    Text = "check if file exists",
                    Actions = new PLang.Runtime2.Core.Actions
                    {
                        new PLang.Runtime2.Core.Action
                        {
                            Module = "file",
                            ActionName = "exists",
                            Parameters = new System.Collections.Generic.List<PLang.Runtime2.Memory.Data>
                                { new PLang.Runtime2.Memory.Data("path", TempPath("ghost.txt")) },
                            Return = new System.Collections.Generic.List<PLang.Runtime2.Memory.Data>
                                { new PLang.Runtime2.Memory.Data("fileResult") }
                        }
                    }
                },
                new PLang.Runtime2.Core.Step
                {
                    Index = 1,
                    Text = "write exists result",
                    Actions = new PLang.Runtime2.Core.Actions
                    {
                        new PLang.Runtime2.Core.Action
                        {
                            Module = "output",
                            ActionName = "write",
                            Parameters = new System.Collections.Generic.List<PLang.Runtime2.Memory.Data>
                                { new PLang.Runtime2.Memory.Data("content", "%fileResult.Exists%") },
                        }
                    }
                }
            }
        };

        var context = _engine.CreateContext();
        var goalResult = await _engine.RunGoalAsync(goal, context);

        await Assert.That(goalResult.Success).IsTrue();

        // fileResult.Exists should be false for non-existent file
        var existsData = context.MemoryStack.Get("fileResult.Exists");
        await Assert.That(existsData).IsNotNull();
        await Assert.That(existsData!.Value).IsEqualTo(false);

        // Output should be "False"
        captureStream.Position = 0;
        var output = new System.IO.StreamReader(captureStream).ReadToEnd();
        await Assert.That(output).IsEqualTo("False");
    }
}
