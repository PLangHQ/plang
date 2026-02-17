using PLang.Runtime2.Engine.Context;
using PLang.Runtime2.Engine;
using PLang.Runtime2.Engine.Memory;
using PLang.Runtime2.modules;
using PLang.Runtime2.modules.library;

namespace PLang.Tests.Runtime2.Modules.library;

public class LibraryLoadTests
{
    /// <summary>
    /// Creates an engine rooted at the directory containing the PLang assembly,
    /// so the sandboxed filesystem can find the assembly file via fs.File.Exists.
    /// </summary>
    private static (PLangContext context, Engine engine, string assemblyPath) CreateContextWithAssembly()
    {
        var assemblyPath = typeof(Engine).Assembly.Location;
        var assemblyDir = global::System.IO.Path.GetDirectoryName(assemblyPath)!;
        var engine = new Engine(assemblyDir);
        var context = engine.CreateContext();
        return (context, engine, assemblyPath);
    }

    [Test]
    public async Task Load_NonexistentPath_ReturnsError()
    {
        await using var engine = new Engine("/app");
        using var context = engine.CreateContext();

        var load = new Load
        {
            Context = context,
            Path = "nonexistent_mylib.dll",
            Namespace = null
        };

        var result = await load.Run();

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.Message).Contains("Library not found");
    }

    [Test]
    public async Task Load_ValidAssembly_AddsToLibraries()
    {
        var (context, engine, assemblyPath) = CreateContextWithAssembly();
        await using (engine)
        {
            var load = new Load
            {
                Context = context,
                Path = assemblyPath,
                Namespace = "PLang.Runtime2.modules"
            };

            var libraryCountBefore = engine.EngineLibraries.Value.Count;
            var result = await load.Run();

            await Assert.That(result.Success).IsTrue();
            await Assert.That(engine.EngineLibraries.Value.Count).IsEqualTo(libraryCountBefore + 1);
        }
    }

    [Test]
    public async Task Load_ValidAssembly_DiscoveredActionsAccessible()
    {
        var (context, engine, assemblyPath) = CreateContextWithAssembly();
        await using (engine)
        {
            var load = new Load
            {
                Context = context,
                Path = assemblyPath,
                Namespace = "PLang.Runtime2.modules"
            };

            var result = await load.Run();
            await Assert.That(result.Success).IsTrue();

            var addedLib = engine.EngineLibraries.Value[^1];
            await Assert.That(addedLib.Contains("variable", "set")).IsTrue();
        }
    }

    [Test]
    public async Task Load_WithCustomNamespace_OnlyDiscoversMatchingTypes()
    {
        var (context, engine, assemblyPath) = CreateContextWithAssembly();
        await using (engine)
        {
            var load = new Load
            {
                Context = context,
                Path = assemblyPath,
                Namespace = "Some.Completely.Wrong.Namespace"
            };

            var result = await load.Run();
            await Assert.That(result.Success).IsTrue();

            var addedLib = engine.EngineLibraries.Value[^1];
            await Assert.That(addedLib.Count).IsEqualTo(0);
        }
    }

    [Test]
    public async Task Load_ReturnsLibraryInfo()
    {
        var (context, engine, assemblyPath) = CreateContextWithAssembly();
        await using (engine)
        {
            var load = new Load
            {
                Context = context,
                Path = assemblyPath,
                Namespace = "PLang.Runtime2.modules"
            };

            var result = await load.Run();

            await Assert.That(result.Success).IsTrue();
            await Assert.That(result.Value).IsNotNull();
        }
    }

    [Test]
    public async Task Load_NullNamespace_DefaultsToBuiltInNamespace()
    {
        var (context, engine, assemblyPath) = CreateContextWithAssembly();
        await using (engine)
        {
            var load = new Load
            {
                Context = context,
                Path = assemblyPath,
                Namespace = null
            };

            var result = await load.Run();
            await Assert.That(result.Success).IsTrue();

            var addedLib = engine.EngineLibraries.Value[^1];
            await Assert.That(addedLib.Contains("variable", "set")).IsTrue();
        }
    }

    [Test]
    public async Task Load_AddedLibrary_ResolvableViaGetCodeGenerated()
    {
        var (context, engine, assemblyPath) = CreateContextWithAssembly();
        await using (engine)
        {
            var load = new Load
            {
                Context = context,
                Path = assemblyPath,
                Namespace = "PLang.Runtime2.modules"
            };

            var result = await load.Run();
            await Assert.That(result.Success).IsTrue();

            // First-match-wins means built-in [0] resolves first,
            // but no errors should occur with multiple libraries
            var (handler, error) = engine.EngineLibraries.GetCodeGenerated("variable", "set", context);
            await Assert.That(handler).IsNotNull();
            await Assert.That(error).IsNull();
        }
    }
}
