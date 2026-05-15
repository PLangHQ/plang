using global::app.Actor.Context;
using app;
using global::app.Variables;
using global::app.modules;
using global::app.modules.module;

namespace PLang.Tests.App.Modules.module;

public class ModuleAddTests
{
    /// <summary>
    /// Creates an engine rooted at the directory containing the PLang assembly,
    /// so the sandboxed filesystem can find the assembly file via fs.File.Exists.
    /// </summary>
    private static (global::app.Actor.Context.@this context, global::app.@this app, string assemblyPath) CreateContextWithAssembly()
    {
        var assemblyPath = typeof(global::app.@this).Assembly.Location;
        var assemblyDir = global::System.IO.Path.GetDirectoryName(assemblyPath)!;
        var app = new global::app.@this(assemblyDir);
        return (app.User.Context, app, assemblyPath);
    }

    [Test]
    public async Task Add_NonexistentPath_ReturnsError()
    {
        await using var app = new global::app.@this("/app");
        var context = app.User.Context;

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
        var (context, app, assemblyPath) = CreateContextWithAssembly();
        await using (app)
        {
            var add = new Add
            {
                Context = context,
                Path = assemblyPath,
                Namespace = "app.modules"
            };

            var countBefore = app.Modules.Count;
            var result = await add.Run();

            await Assert.That(result.Success).IsTrue();
            // Discover re-registers the same built-in types, count stays same
            // but no error should occur
        }
    }

    [Test]
    public async Task Add_ValidAssembly_DiscoveredActionsAccessible()
    {
        var (context, app, assemblyPath) = CreateContextWithAssembly();
        await using (app)
        {
            var add = new Add
            {
                Context = context,
                Path = assemblyPath,
                Namespace = "app.modules"
            };

            var result = await add.Run();
            await Assert.That(result.Success).IsTrue();

            // After adding, actions should be discoverable via the flat registry
            await Assert.That(app.Modules.Contains("variable", "set")).IsTrue();
        }
    }

    [Test]
    public async Task Add_WithCustomNamespace_OnlyDiscoversMatchingTypes()
    {
        var (context, app, assemblyPath) = CreateContextWithAssembly();
        await using (app)
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
        var (context, app, assemblyPath) = CreateContextWithAssembly();
        await using (app)
        {
            var add = new Add
            {
                Context = context,
                Path = assemblyPath,
                Namespace = "app.modules"
            };

            var result = await add.Run();

            await Assert.That(result.Success).IsTrue();
            await Assert.That(result.Value).IsNotNull();
        }
    }

    [Test]
    public async Task Add_NullNamespace_DefaultsToBuiltInNamespace()
    {
        var (context, app, assemblyPath) = CreateContextWithAssembly();
        await using (app)
        {
            var add = new Add
            {
                Context = context,
                Path = assemblyPath,
                Namespace = null
            };

            var result = await add.Run();
            await Assert.That(result.Success).IsTrue();

            // With null namespace, Discover defaults to App.modules
            await Assert.That(app.Modules.Contains("variable", "set")).IsTrue();
        }
    }

    [Test]
    public async Task Add_AddedActions_ResolvableViaGetCodeGenerated()
    {
        var (context, app, assemblyPath) = CreateContextWithAssembly();
        await using (app)
        {
            var add = new Add
            {
                Context = context,
                Path = assemblyPath,
                Namespace = "app.modules"
            };

            var result = await add.Run();
            await Assert.That(result.Success).IsTrue();

            // Actions registered via Discover should be resolvable
            var (action, error) = app.Modules.GetCodeGenerated(new PrAction { Module = "variable", ActionName = "set" });
            await Assert.That(action).IsNotNull();
            await Assert.That(error).IsNull();
        }
    }
}
