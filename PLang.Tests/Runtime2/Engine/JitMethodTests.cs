using PLang.Runtime2.Engine;
using PLang.Runtime2.Engine.Memory;
using PLang.Runtime2.Engine.Modules;
using PLang.SafeFileSystem;

namespace PLang.Tests.Runtime2.Engine;

public class JitMethodTests : IDisposable
{
    private readonly string _tempDir;
    private readonly PLangFileSystem _fs;
    private readonly PLang.Runtime2.Engine.@this _engine;

    public JitMethodTests()
    {
        _tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plang_jit_test_" + Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(_tempDir);
        _fs = new PLangFileSystem(_tempDir, "");
        _engine = new PLang.Runtime2.Engine.@this(_tempDir, fileSystem: _fs);
    }

    public void Dispose()
    {
        _engine.DisposeAsync().AsTask().GetAwaiter().GetResult();
        if (System.IO.Directory.Exists(_tempDir))
            System.IO.Directory.Delete(_tempDir, true);
    }

    [Test]
    public async Task JitCompile_SimpleMethod_ProducesWorkingExecutor()
    {
        // Find the [Method] on Engine
        var method = typeof(PLang.Runtime2.Engine.@this).GetMethod("TestEcho",
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        await Assert.That(method).IsNotNull();

        // JIT compile it
        var executor = MethodJitCompiler.Compile(_engine, method!);
        await Assert.That(executor).IsNotNull();

        // Execute with parameters
        var parameters = new List<Data> { new Data("message", "hello jit") };
        var context = _engine.CreateContext();
        var result = await executor!.ExecuteAsync(parameters, _engine, context);

        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Value).IsEqualTo("echo: hello jit");
    }

    [Test]
    public async Task JitCompile_MethodWithDefaults_UsesDefaultValues()
    {
        var method = typeof(PLang.Runtime2.Engine.@this).GetMethod("TestEcho",
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

        var executor = MethodJitCompiler.Compile(_engine, method!);

        // Call without the optional 'prefix' parameter — should use default "echo"
        var parameters = new List<Data> { new Data("message", "world") };
        var context = _engine.CreateContext();
        var result = await executor!.ExecuteAsync(parameters, _engine, context);

        await Assert.That(result.Value).IsEqualTo("echo: world");
    }

    [Test]
    public async Task JitCompile_MethodWithVariableRef_ResolvesFromMemoryStack()
    {
        var method = typeof(PLang.Runtime2.Engine.@this).GetMethod("TestEcho",
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

        var executor = MethodJitCompiler.Compile(_engine, method!);

        var context = _engine.CreateContext();
        context.MemoryStack.Set("myMsg", "from memory");

        var parameters = new List<Data> { new Data("message", "%myMsg%") };
        var result = await executor!.ExecuteAsync(parameters, _engine, context);

        await Assert.That(result.Value).IsEqualTo("echo: from memory");
    }
}
