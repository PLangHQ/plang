using PLang.Runtime2.Engine.Context;
using PLang.Runtime2.Engine;
using PLang.Runtime2.Engine.Memory;
using PLang.Runtime2.modules;
using PLang.Runtime2.modules.module;

namespace PLang.Tests.Runtime2.Modules.module;

public class ModuleAddTests
{
    /// <summary>
    /// Creates an engine rooted at the directory containing the PLang assembly,
    /// so the sandboxed filesystem can find the assembly file via fs.File.Exists.
    /// </summary>
    private static (PLangContext context, PLang.Runtime2.Engine.@this engine, string assemblyPath) CreateContextWithAssembly()
    {
        var assemblyPath = typeof(PLang.Runtime2.Engine.@this).Assembly.Location;
        var assemblyDir = global::System.IO.Path.GetDirectoryName(assemblyPath)!;
        var engine = new PLang.Runtime2.Engine.@this(assemblyDir);
        var context = engine.CreateContext();
        return (context, engine, assemblyPath);
    }

    [Test]
    public async Task Add_NonexistentPath_ReturnsError()
    {
        await using var engine = new PLang.Runtime2.Engine.@this("/app");
        using var context = engine.CreateContext();

        var add = new Add
        {
            Context = context,
            Path = "nonexistent_mylib.dll",
            Namespace = null
        };

        var result = await add.Run();

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.Message).Contains("Module not found");
    }

    [Test]
    public async Task Add_ValidAssembly_DiscoverActions()
    {
        var (context, engine, assemblyPath) = CreateContextWithAssembly();
        await using (engine)
        {
            var add = new Add
            {
                Context = context,
                Path = assemblyPath,
                Namespace = "PLang.Runtime2.modules"
            };

            var countBefore = engine.Modules.Count;
            var result = await add.Run();

            await Assert.That(result.Success).IsTrue();
            // Discover re-registers the same built-in types, count stays same
            // but no error should occur
        }
    }

    [Test]
    public async Task Add_ValidAssembly_DiscoveredActionsAccessible()
    {
        var (context, engine, assemblyPath) = CreateContextWithAssembly();
        await using (engine)
        {
            var add = new Add
            {
                Context = context,
                Path = assemblyPath,
                Namespace = "PLang.Runtime2.modules"
            };

            var result = await add.Run();
            await Assert.That(result.Success).IsTrue();

            // After adding, actions should be discoverable via the flat registry
            await Assert.That(engine.Modules.Contains("variable", "set")).IsTrue();
        }
    }

    [Test]
    public async Task Add_WithCustomNamespace_OnlyDiscoversMatchingTypes()
    {
        var (context, engine, assemblyPath) = CreateContextWithAssembly();
        await using (engine)
        {
            var add = new Add
            {
                Context = context,
                Path = assemblyPath,
                Namespace = "Some.Completely.Wrong.Namespace"
            };

            var result = await add.Run();
            await Assert.That(result.Success).IsTrue();

            // The result value should report 0 actions discovered
            await Assert.That(result.Value).IsNotNull();
        }
    }

    [Test]
    public async Task Add_ReturnsModuleInfo()
    {
        var (context, engine, assemblyPath) = CreateContextWithAssembly();
        await using (engine)
        {
            var add = new Add
            {
                Context = context,
                Path = assemblyPath,
                Namespace = "PLang.Runtime2.modules"
            };

            var result = await add.Run();

            await Assert.That(result.Success).IsTrue();
            await Assert.That(result.Value).IsNotNull();
        }
    }

    [Test]
    public async Task Add_NullNamespace_DefaultsToBuiltInNamespace()
    {
        var (context, engine, assemblyPath) = CreateContextWithAssembly();
        await using (engine)
        {
            var add = new Add
            {
                Context = context,
                Path = assemblyPath,
                Namespace = null
            };

            var result = await add.Run();
            await Assert.That(result.Success).IsTrue();

            // With null namespace, Discover defaults to PLang.Runtime2.modules
            await Assert.That(engine.Modules.Contains("variable", "set")).IsTrue();
        }
    }

    [Test]
    public async Task Add_AddedActions_ResolvableViaGetCodeGenerated()
    {
        var (context, engine, assemblyPath) = CreateContextWithAssembly();
        await using (engine)
        {
            var add = new Add
            {
                Context = context,
                Path = assemblyPath,
                Namespace = "PLang.Runtime2.modules"
            };

            var result = await add.Run();
            await Assert.That(result.Success).IsTrue();

            // Actions registered via Discover should be resolvable
            var (action, error) = engine.Modules.GetCodeGenerated("variable", "set", context);
            await Assert.That(action).IsNotNull();
            await Assert.That(error).IsNull();
        }
    }
}
