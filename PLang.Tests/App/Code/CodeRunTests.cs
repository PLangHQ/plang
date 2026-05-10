using global::App.FileSystem.Default;
using global::App.FileSystem;
using global::App.Code;

namespace PLang.Tests.App.CodeArea;

/// <summary>
/// End-to-end smoke tests for App.Code.Load → Compiled → Runtime →
/// Start/Invoke. Compiles real .cs source through Roslyn, loads into a
/// collectible ALC, and invokes methods.
/// </summary>
public sealed class CodeRunTests : IDisposable
{
    private readonly string _tempDir;
    private readonly PLangFileSystem _fs;
    private readonly global::App.@this _app;

    public CodeRunTests()
    {
        _tempDir = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "plang_code_run_" + Guid.NewGuid().ToString("N"));
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

    private global::App.Data.@this<global::App.FileSystem.Path> WriteScript(
        string source, string name = "Code.cs")
    {
        var abs = System.IO.Path.Combine(_tempDir, name);
        System.IO.File.WriteAllText(abs, source);
        var path = new global::App.FileSystem.Path(abs, _app.User.Context);
        return new global::App.Data.@this<global::App.FileSystem.Path>(value: path);
    }

    [Test]
    public async Task Start_NoArgs_ReturnsValueFromStartMethod()
    {
        var pathData = WriteScript("""
            public class MyCode {
                public async System.Threading.Tasks.Task<int> Start() { await System.Threading.Tasks.Task.Yield(); return 42; }
            }
        """);

        var runner = new global::App.Code.Runner.@this(pathData);
        var result = await runner.Start(global::App.Data.@this.Ok(null));
        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Value).IsEqualTo(42);
    }

    [Test]
    public async Task Start_WithDataArg_ReceivesData()
    {
        var pathData = WriteScript("""
            public class MyCode {
                public async System.Threading.Tasks.Task<int> Start(global::App.Data.@this data) {
                    await System.Threading.Tasks.Task.Yield();
                    return (data.Value as int?) ?? 0;
                }
            }
        """);

        var runner = new global::App.Code.Runner.@this(pathData);
        var result = await runner.Start(global::App.Data.@this.Ok(7));
        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Value).IsEqualTo(7);
    }

    [Test]
    public async Task Invoke_NamedMethodWithPositionalArgs_BindsAndReturnsResult()
    {
        var pathData = WriteScript("""
            public class MyCode {
                public async System.Threading.Tasks.Task<int> Sum(int x, int y) { await System.Threading.Tasks.Task.Yield(); return x + y; }
            }
        """);

        var loaded = await _app.Code.Load(pathData);
        await Assert.That(loaded.Success).IsTrue();
        var runtime = loaded.Value!;

        var sumResult = await runtime.Invoke("Sum", new List<global::App.Data.@this>
        {
            global::App.Data.@this.Ok(5),
            global::App.Data.@this.Ok(7),
        }, _app.User.Context);
        await Assert.That(sumResult.Success).IsTrue();
        await Assert.That(sumResult.Value).IsEqualTo(12);
    }

    [Test]
    public async Task Invoke_TaskNonGeneric_ReturnsOkWithNullValue()
    {
        var pathData = WriteScript("""
            public class MyCode {
                public async System.Threading.Tasks.Task DoWork() { await System.Threading.Tasks.Task.Yield(); }
            }
        """);

        var loaded = await _app.Code.Load(pathData);
        var runtime = loaded.Value!;

        var result = await runtime.Invoke("DoWork", new List<global::App.Data.@this>(), _app.User.Context);
        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Value).IsNull();
    }

    [Test]
    public async Task Invoke_MissingMethod_ReturnsMethodNotFoundError()
    {
        var pathData = WriteScript("""
            public class MyCode {
                public async System.Threading.Tasks.Task<int> Start() => await System.Threading.Tasks.Task.FromResult(1);
            }
        """);

        var loaded = await _app.Code.Load(pathData);
        var runtime = loaded.Value!;
        var result = await runtime.Invoke("DoesNotExist", new List<global::App.Data.@this>(), _app.User.Context);

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.Key).IsEqualTo("MethodNotFound");
    }

    [Test]
    public async Task Invoke_ArityMismatch_ReturnsError()
    {
        var pathData = WriteScript("""
            public class MyCode {
                public async System.Threading.Tasks.Task<int> Sum(int x, int y) => await System.Threading.Tasks.Task.FromResult(x + y);
            }
        """);

        var loaded = await _app.Code.Load(pathData);
        var runtime = loaded.Value!;
        var result = await runtime.Invoke(
            "Sum",
            new List<global::App.Data.@this> { global::App.Data.@this.Ok(1) },
            _app.User.Context);

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.Key).IsEqualTo("ArityMismatch");
    }

    [Test]
    public async Task Load_FileNotFound_ReturnsError()
    {
        var ghostPath = new global::App.FileSystem.Path(
            System.IO.Path.Combine(_tempDir, "nope.cs"), _app.User.Context);
        var ghost = new global::App.Data.@this<global::App.FileSystem.Path>(value: ghostPath);

        var loaded = await _app.Code.Load(ghost);

        await Assert.That(loaded.Success).IsFalse();
        await Assert.That(loaded.Error!.Key).IsEqualTo("FileNotFound");
    }

    [Test]
    public async Task Load_BadSource_ReturnsCompileFailedError()
    {
        var pathData = WriteScript("public class Broken { this is not C# }");
        var loaded = await _app.Code.Load(pathData);

        await Assert.That(loaded.Success).IsFalse();
        await Assert.That(loaded.Error!.Key).IsEqualTo("CompileFailed");
    }

    [Test]
    public async Task Path_GetContent_ReadsFileThroughIFileProvider()
    {
        var abs = System.IO.Path.Combine(_tempDir, "hello.txt");
        System.IO.File.WriteAllText(abs, "hello from path");
        var path = new global::App.FileSystem.Path(abs, _app.User.Context);

        var result = await path.GetContent();
        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Value).IsEqualTo("hello from path");

        // Second call hits the memoised Content, no second read.
        var again = await path.GetContent();
        await Assert.That(again.Value).IsEqualTo("hello from path");
    }

    [Test]
    public async Task Load_SameSourceTwice_ReturnsCachedRuntime()
    {
        var pathData = WriteScript("""
            public class MyCode {
                public async System.Threading.Tasks.Task<int> Start() => await System.Threading.Tasks.Task.FromResult(99);
            }
        """);

        var a = await _app.Code.Load(pathData);
        var b = await _app.Code.Load(pathData);
        await Assert.That(a.Success).IsTrue();
        await Assert.That(b.Success).IsTrue();

        // Compile-once-per-app: same source-hash → same Runtime.
        await Assert.That(ReferenceEquals(a.Value, b.Value)).IsTrue();

        var resA = await a.Value!.Start(global::App.Data.@this.Ok(null), _app.User.Context);
        var resB = await b.Value!.Start(global::App.Data.@this.Ok(null), _app.User.Context);
        await Assert.That(resA.Value).IsEqualTo(99);
        await Assert.That(resB.Value).IsEqualTo(99);
        // Cache owns disposal — App.Dispose evicts.
    }

    [Test]
    public async Task Load_SourceChanges_CompilesFreshRuntime()
    {
        var pathData = WriteScript("""
            public class MyCode {
                public async System.Threading.Tasks.Task<int> Start() => await System.Threading.Tasks.Task.FromResult(1);
            }
        """, name: "Mutable.cs");

        var first = await _app.Code.Load(pathData);
        await Assert.That(first.Success).IsTrue();

        // Rewrite the file with different source — new hash, new Runtime.
        var abs = System.IO.Path.Combine(_tempDir, "Mutable.cs");
        System.IO.File.WriteAllText(abs, """
            public class MyCode {
                public async System.Threading.Tasks.Task<int> Start() => await System.Threading.Tasks.Task.FromResult(2);
            }
        """);

        var second = await _app.Code.Load(pathData);
        await Assert.That(second.Success).IsTrue();
        await Assert.That(ReferenceEquals(first.Value, second.Value)).IsFalse();

        var firstResult = await first.Value!.Start(global::App.Data.@this.Ok(null), _app.User.Context);
        var secondResult = await second.Value!.Start(global::App.Data.@this.Ok(null), _app.User.Context);
        await Assert.That(firstResult.Value).IsEqualTo(1);
        await Assert.That(secondResult.Value).IsEqualTo(2);
    }
}
