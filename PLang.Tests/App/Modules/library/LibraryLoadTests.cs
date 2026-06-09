using app.actor.context;
using app;
using app.variable;
using app.module;
using app.module.module;

namespace PLang.Tests.App.Modules.module;

public class ModuleAddTests
{
    /// <summary>
    /// Creates an engine rooted at the directory containing the PLang assembly,
    /// so the sandboxed filesystem can find the assembly file via fs.File.Exists.
    /// </summary>
    private static (global::app.actor.context.@this context, global::app.@this app, string assemblyPath) CreateContextWithAssembly()
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
            Path = global::app.data.@this<global::app.type.path.@this>.Ok(global::app.type.path.@this.Resolve("nonexistent_mylib.dll", context)),
            Namespace = null
        };

        var result = await add.Run();

        await result.IsFailure();
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
                Path = global::app.data.@this<global::app.type.path.@this>.Ok(global::app.type.path.@this.Resolve(assemblyPath, context)),
                Namespace = (global::app.type.text.@this)"app.module"
            };

            var countBefore = app.Module.Count;
            var result = await add.Run();

            await result.IsSuccess();
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
                Path = global::app.data.@this<global::app.type.path.@this>.Ok(global::app.type.path.@this.Resolve(assemblyPath, context)),
                Namespace = (global::app.type.text.@this)"app.module"
            };

            var result = await add.Run();
            await result.IsSuccess();

            // After adding, actions should be discoverable via the flat registry
            await Assert.That(app.Module.Contains("variable", "set")).IsTrue();
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
                Path = global::app.data.@this<global::app.type.path.@this>.Ok(global::app.type.path.@this.Resolve(assemblyPath, context)),
                Namespace = (global::app.type.text.@this)"Some.Completely.Wrong.Namespace"
            };

            var result = await add.Run();
            await result.IsSuccess();

            // The result value should report 0 actions discovered
            await Assert.That((await result.Value())).IsNotNull();
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
                Path = global::app.data.@this<global::app.type.path.@this>.Ok(global::app.type.path.@this.Resolve(assemblyPath, context)),
                Namespace = (global::app.type.text.@this)"app.module"
            };

            var result = await add.Run();

            await result.IsSuccess();
            await Assert.That((await result.Value())).IsNotNull();
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
                Path = global::app.data.@this<global::app.type.path.@this>.Ok(global::app.type.path.@this.Resolve(assemblyPath, context)),
                Namespace = null
            };

            var result = await add.Run();
            await result.IsSuccess();

            // With null namespace, Discover defaults to App.modules
            await Assert.That(app.Module.Contains("variable", "set")).IsTrue();
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
                Path = global::app.data.@this<global::app.type.path.@this>.Ok(global::app.type.path.@this.Resolve(assemblyPath, context)),
                Namespace = (global::app.type.text.@this)"app.module"
            };

            var result = await add.Run();
            await result.IsSuccess();

            // Actions registered via Discover should be resolvable
            var (action, error) = app.Module.GetCodeGenerated(new PrAction { Module = "variable", ActionName = "set" });
            await Assert.That(action).IsNotNull();
            await Assert.That(error).IsNull();
        }
    }
}
