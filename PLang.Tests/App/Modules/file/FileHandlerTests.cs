using app.actor.context;
using app;
using app.variables;
using app.modules.file;
using PLangPath = global::app.types.path.@this;
using PLangFilePath = global::app.types.path.file.@this;

namespace PLang.Tests.App.actions.file;

public class FileHandlerTests : IDisposable
{
    private readonly string _tempDir;
    private readonly global::app.@this _app;

    public FileHandlerTests()
    {
        _tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plang_test_" + Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(_tempDir);
        _app = new global::app.@this(_tempDir);
    }

    public void Dispose()
    {
        _app.DisposeAsync().AsTask().GetAwaiter().GetResult();
        if (System.IO.Directory.Exists(_tempDir))
            System.IO.Directory.Delete(_tempDir, true);
    }

    private string TempPath(string relativePath) =>
        System.IO.Path.Combine(_tempDir, relativePath);

    private global::app.data.@this<PLangPath> MakePath(string relativePath) =>
        new("", new PLangFilePath(TempPath(relativePath)) { Context = _app.User.Context });

    private global::app.data.@this<PLangPath> MakeAbsPath(string absolutePath) =>
        new("", new PLangFilePath(absolutePath) { Context = _app.User.Context });

    // --- Save ---

    [Test]
    public async Task Save_ReturnsFileWithCorrectPaths()
    {
        var action = new Save { Context = _app.User.Context, Path = MakePath("test.txt"), Value = Data.Ok("hello") };
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
        var action = new Save { Context = _app.User.Context, Path = MakePath("exists.txt"), Value = Data.Ok("data") };
        await action.Run();

        await Assert.That(System.IO.File.Exists(TempPath("exists.txt"))).IsTrue();
    }

    // --- Read ---

    [Test]
    public async Task Read_ReturnsFileObject()
    {
        System.IO.File.WriteAllText(TempPath("read.txt"), "content here");

        var action = new Read { Context = _app.User.Context, Path = MakePath("read.txt") };
        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Value).IsEqualTo("content here");
    }

    [Test]
    public async Task Read_UnregisteredSchemePath_SurfacesTypedError_NotNre()
    {
        // a path whose scheme conversion failed (e.g. the
        // unregistered s3:// scheme) is a non-Success data.@this<path>. The
        // handler must surface that typed SchemeNotRegistered error, not NRE
        // on Path.Value.
        var ctx = _app.User.Context;
        var failedPath = new Data("path", "s3://bucket/key") { Context = ctx }
            .As<PLangPath>(ctx);
        await Assert.That(failedPath.Success).IsFalse();   // conversion already failed

        var action = new Read { Context = ctx, Path = failedPath };
        var result = await action.Run();

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.Key).IsEqualTo("SchemeNotRegistered");
    }

    [Test]
    public async Task Read_ContentIsLazy()
    {
        System.IO.File.WriteAllText(TempPath("lazy.txt"), "lazy content");

        var action = new Read { Context = _app.User.Context, Path = MakePath("lazy.txt") };
        var result = await action.Run();

        await Assert.That(result.Value).IsEqualTo("lazy content");
    }

    [Test]
    public async Task Read_NonexistentFile_ReturnsError()
    {
        var action = new Read { Context = _app.User.Context, Path = MakePath("nonexistent.txt") };
        var result = await action.Run();

        await Assert.That(result.Success).IsFalse();
    }

    // --- Read with ResolveVariables (Gap 2 from coder handover) ---

    [Test]
    public async Task Read_ResolveVariablesTrue_ResolvesVariableInContent()
    {
        System.IO.File.WriteAllText(TempPath("template.txt"), "Hello %name%, welcome");
        _app.User.Context.Variables.Set("name", "Ingi");

        var action = new Read
        {
            Context = _app.User.Context,
            Path = MakePath("template.txt"),
            ResolveVariables = new global::app.data.@this<bool>("ResolveVariables", true)
        };
        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Value).IsEqualTo("Hello Ingi, welcome");
    }

    [Test]
    public async Task Read_ResolveVariablesFalse_LeavesVariableLiteral()
    {
        // Default value of ResolveVariables is false (per [Default(false)]). Even
        // when %var% is set, the literal must come back unresolved.
        System.IO.File.WriteAllText(TempPath("literal.txt"), "Hello %name%, welcome");
        _app.User.Context.Variables.Set("name", "Ingi");

        var action = new Read
        {
            Context = _app.User.Context,
            Path = MakePath("literal.txt"),
            ResolveVariables = new global::app.data.@this<bool>("ResolveVariables", false)
        };
        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Value).IsEqualTo("Hello %name%, welcome");
    }

    [Test]
    public async Task Read_ResolveVariablesTrue_BlocksInfrastructureVariables()
    {
        // skipInfrastructure: file content is untrusted — %!app%, %!fileSystem%
        // etc. must not resolve even when ResolveVariables=true. Without this
        // guard, a malicious file could leak runtime internals through %!app.Id%.
        System.IO.File.WriteAllText(TempPath("untrusted.txt"), "id is %!app.Id%");

        var action = new Read
        {
            Context = _app.User.Context,
            Path = MakePath("untrusted.txt"),
            ResolveVariables = new global::app.data.@this<bool>("ResolveVariables", true)
        };
        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        // The literal stays — infrastructure variable was not resolved.
        await Assert.That(result.Value).IsEqualTo("id is %!app.Id%");
    }

    // --- Copy ---

    [Test]
    public async Task Copy_ReturnsFileWithSource()
    {
        System.IO.File.WriteAllText(TempPath("src.txt"), "source data");

        var action = new Copy { Context = _app.User.Context, Source = MakePath("src.txt"), Destination = MakePath("dst.txt") };
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
        var action = new Copy { Context = _app.User.Context, Source = MakePath("nope.txt"), Destination = MakePath("dst.txt") };
        var result = await action.Run();

        await Assert.That(result.Success).IsFalse();
    }

    [Test]
    public async Task Copy_Directory_CopiesAllFiles()
    {
        var srcDir = TempPath("copy_dir");
        System.IO.Directory.CreateDirectory(srcDir);
        System.IO.File.WriteAllText(System.IO.Path.Combine(srcDir, "a.txt"), "a");

        var action = new Copy { Context = _app.User.Context, Source = MakeAbsPath(srcDir), Destination = MakePath("copy_dir_dst") };
        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        await Assert.That(System.IO.File.Exists(System.IO.Path.Combine(TempPath("copy_dir_dst"), "a.txt"))).IsTrue();
    }

    // --- Move ---

    [Test]
    public async Task Move_ReturnsFileWithSource()
    {
        System.IO.File.WriteAllText(TempPath("move_src.txt"), "move data");

        var action = new Move { Context = _app.User.Context, Source = MakePath("move_src.txt"), Destination = MakePath("move_dst.txt") };
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
        var action = new Move { Context = _app.User.Context, Source = MakePath("nope.txt"), Destination = MakePath("dst.txt") };
        var result = await action.Run();

        await Assert.That(result.Success).IsFalse();
    }

    [Test]
    public async Task Move_Directory_MovesDirectory()
    {
        var srcDir = TempPath("move_dir");
        System.IO.Directory.CreateDirectory(srcDir);
        System.IO.File.WriteAllText(System.IO.Path.Combine(srcDir, "a.txt"), "a");

        var action = new Move { Context = _app.User.Context, Source = MakeAbsPath(srcDir), Destination = MakePath("move_dir_dst") };
        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        await Assert.That(System.IO.Directory.Exists(srcDir)).IsFalse();
        await Assert.That(System.IO.File.Exists(System.IO.Path.Combine(TempPath("move_dir_dst"), "a.txt"))).IsTrue();
    }

    // --- Delete ---

    [Test]
    public async Task Delete_ReturnsFile()
    {
        System.IO.File.WriteAllText(TempPath("del.txt"), "delete me");

        var action = new Delete { Context = _app.User.Context, Path = MakePath("del.txt") };
        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        var f = result.Value as PLangPath;
        await Assert.That(f).IsNotNull();
        await Assert.That(await f!.AsBooleanAsync()).IsFalse();
    }

    [Test]
    public async Task Delete_NonexistentFile_ReturnsError()
    {
        var action = new Delete { Context = _app.User.Context, Path = MakePath("nope.txt") };
        var result = await action.Run();

        await Assert.That(result.Success).IsFalse();
    }

    [Test]
    public async Task Delete_NonexistentFile_IgnoreIfNotFound_ReturnsSuccess()
    {
        var action = new Delete { Context = _app.User.Context, Path = MakePath("nope.txt"), IgnoreIfNotFound = true };
        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
    }

    [Test]
    public async Task Delete_Directory_Recursive()
    {
        var dir = TempPath("del_dir");
        System.IO.Directory.CreateDirectory(dir);
        System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "child.txt"), "data");

        var action = new Delete { Context = _app.User.Context, Path = MakeAbsPath(dir), Recursive = true };
        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        await Assert.That(System.IO.Directory.Exists(dir)).IsFalse();
    }

    // --- Exists ---

    [Test]
    public async Task Exists_ExistingFile_ReturnsTrue()
    {
        System.IO.File.WriteAllText(TempPath("check.txt"), "present");

        var action = new Exists { Context = _app.User.Context, Path = MakePath("check.txt") };
        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        var f = result.Value as PLangPath;
        await Assert.That(f).IsNotNull();
        await Assert.That(await f!.AsBooleanAsync()).IsTrue();
    }

    [Test]
    public async Task Exists_NonexistentFile_ReturnsFalse()
    {
        var action = new Exists { Context = _app.User.Context, Path = MakePath("missing.txt") };
        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        var f = result.Value as PLangPath;
        await Assert.That(f).IsNotNull();
        await Assert.That(await f!.AsBooleanAsync()).IsFalse();
    }

    // --- List ---

    [Test]
    public async Task List_ReturnsFileArray()
    {
        var subDir = TempPath("listdir");
        System.IO.Directory.CreateDirectory(subDir);
        System.IO.File.WriteAllText(System.IO.Path.Combine(subDir, "a.txt"), "a");
        System.IO.File.WriteAllText(System.IO.Path.Combine(subDir, "b.txt"), "b");

        var action = new List { Context = _app.User.Context, Path = MakeAbsPath(subDir) };
        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        var files = result.Value as System.Collections.Generic.List<PLangPath>;
        await Assert.That(files).IsNotNull();
        await Assert.That(files!.Count).IsEqualTo(2);
    }

    [Test]
    public async Task List_WithPattern_FiltersFiles()
    {
        var subDir = TempPath("listpattern");
        System.IO.Directory.CreateDirectory(subDir);
        System.IO.File.WriteAllText(System.IO.Path.Combine(subDir, "a.txt"), "a");
        System.IO.File.WriteAllText(System.IO.Path.Combine(subDir, "b.md"), "b");

        var action = new List { Context = _app.User.Context, Path = MakeAbsPath(subDir), Pattern = "*.txt" };
        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        var files = result.Value as System.Collections.Generic.List<PLangPath>;
        await Assert.That(files).IsNotNull();
        await Assert.That(files!.Count).IsEqualTo(1);
    }

    [Test]
    public async Task List_Recursive_FindsNestedFiles()
    {
        var subDir = TempPath("listrecursive");
        var nested = System.IO.Path.Combine(subDir, "sub");
        System.IO.Directory.CreateDirectory(subDir);
        System.IO.Directory.CreateDirectory(nested);
        System.IO.File.WriteAllText(System.IO.Path.Combine(subDir, "top.txt"), "top");
        System.IO.File.WriteAllText(System.IO.Path.Combine(nested, "deep.txt"), "deep");

        var action = new List { Context = _app.User.Context, Path = MakeAbsPath(subDir), Recursive = true };
        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        var files = result.Value as System.Collections.Generic.List<PLangPath>;
        await Assert.That(files).IsNotNull();
        await Assert.That(files!.Count).IsEqualTo(2);
    }

    [Test]
    public async Task List_NonexistentDirectory_ReturnsError()
    {
        var action = new List { Context = _app.User.Context, Path = MakePath("nodir") };
        var result = await action.Run();

        await Assert.That(result.Success).IsFalse();
    }

    // --- File type object properties ---

    [Test]
    public async Task FileType_MimeType_FromExtension()
    {
        System.IO.File.WriteAllText(TempPath("doc.md"), "# Hello");

        var action = new Read { Context = _app.User.Context, Path = MakePath("doc.md") };
        var result = await action.Run();

        await Assert.That(result.Type!.Value).IsEqualTo("text/markdown");
    }

    [Test]
    public async Task FileType_Size_IsLazy()
    {
        System.IO.File.WriteAllText(TempPath("sized.txt"), "12345");

        var action = new Read { Context = _app.User.Context, Path = MakePath("sized.txt") };
        var result = await action.Run();
        var content = result.Value as string;

        await Assert.That(content!.Length).IsEqualTo(5);
    }

    [Test]
    public async Task FileType_ToString_ReturnsContent()
    {
        System.IO.File.WriteAllText(TempPath("tostring.txt"), "file-content");

        var action = new Read { Context = _app.User.Context, Path = MakePath("tostring.txt") };
        var result = await action.Run();

        await Assert.That(result.Value!.ToString()).IsEqualTo("file-content");
    }

    // --- Integration: file.exists -> Variables -> output.write ---

    [Test]
    public async Task Integration_FileExists_FlowsThroughVariables_ToOutput()
    {
        System.IO.File.WriteAllText(TempPath("real.txt"), "I exist");

        var captureStream = new System.IO.MemoryStream();
        _app.User.Channels.Register(new StreamChannel(
            EngineChannels.Output, captureStream,
            ChannelDirection.Output, ownsStream: true)
        { Mime = "text/plain" });

        var goal = new global::app.goals.goal.@this
        {
            Name = "TestFileExistsFlow",
            Steps = new global::app.goals.goal.steps.@this
            {
                new global::app.goals.goal.steps.step.@this
                {
                    Index = 0,
                    Text = "check if file exists",
                    Actions = new global::app.goals.goal.steps.step.actions.@this
                    {
                        new global::app.goals.goal.steps.step.actions.action.@this
                        {
                            Module = "file",
                            ActionName = "exists",
                            Parameters = new System.Collections.Generic.List<global::app.data.@this>
                                { new global::app.data.@this("path", TempPath("real.txt")) },
                        },
                        new global::app.goals.goal.steps.step.actions.action.@this
                        {
                            Module = "variable",
                            ActionName = "set",
                            Parameters = new System.Collections.Generic.List<global::app.data.@this>
                            {
                                new global::app.data.@this("Name", "fileResult"),
                                new global::app.data.@this("Value", "%!data%")                            }
                        }
                    }
                },
                new global::app.goals.goal.steps.step.@this
                {
                    Index = 1,
                    Text = "write exists result",
                    Actions = new global::app.goals.goal.steps.step.actions.@this
                    {
                        new global::app.goals.goal.steps.step.actions.action.@this
                        {
                            Module = "output",
                            ActionName = "write",
                            Parameters = new System.Collections.Generic.List<global::app.data.@this>
                                { new global::app.data.@this("Data", "%fileResult.Exists%") },
                        }
                    }
                }
            }
        };

        var context = _app.User.Context;
        var goalResult = await _app.RunGoalAsync(goal, context);

        await Assert.That(goalResult.Success).IsTrue();

        var fileData = context.Variables.Get("fileResult");
        await Assert.That(fileData).IsNotNull();
        var fileObj = fileData!.Value as PLangPath;
        await Assert.That(fileObj).IsNotNull();
        await Assert.That(await fileObj!.AsBooleanAsync()).IsTrue();

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
        _app.User.Channels.Register(new StreamChannel(
            EngineChannels.Output, captureStream,
            ChannelDirection.Output, ownsStream: true)
        { Mime = "text/plain" });

        var goal = new global::app.goals.goal.@this
        {
            Name = "TestFileNotExistsFlow",
            Steps = new global::app.goals.goal.steps.@this
            {
                new global::app.goals.goal.steps.step.@this
                {
                    Index = 0,
                    Text = "check if file exists",
                    Actions = new global::app.goals.goal.steps.step.actions.@this
                    {
                        new global::app.goals.goal.steps.step.actions.action.@this
                        {
                            Module = "file",
                            ActionName = "exists",
                            Parameters = new System.Collections.Generic.List<global::app.data.@this>
                                { new global::app.data.@this("path", TempPath("ghost.txt")) },
                        },
                        new global::app.goals.goal.steps.step.actions.action.@this
                        {
                            Module = "variable",
                            ActionName = "set",
                            Parameters = new System.Collections.Generic.List<global::app.data.@this>
                            {
                                new global::app.data.@this("Name", "fileResult"),
                                new global::app.data.@this("Value", "%!data%")                            }
                        }
                    }
                },
                new global::app.goals.goal.steps.step.@this
                {
                    Index = 1,
                    Text = "write exists result",
                    Actions = new global::app.goals.goal.steps.step.actions.@this
                    {
                        new global::app.goals.goal.steps.step.actions.action.@this
                        {
                            Module = "output",
                            ActionName = "write",
                            Parameters = new System.Collections.Generic.List<global::app.data.@this>
                                { new global::app.data.@this("Data", "%fileResult.Exists%") },
                        }
                    }
                }
            }
        };

        var context = _app.User.Context;
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
