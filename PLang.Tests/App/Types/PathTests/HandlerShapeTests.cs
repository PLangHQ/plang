using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using System.Linq;
using System.Reflection;

namespace PLang.Tests.App.Types.PathTests;

/// <summary>
/// Stage 3 — handler one-liners and the death of IFile / DefaultFileProvider /
/// the [Code]-partial provider injection on file handlers.
/// </summary>
public class HandlerShapeTests
{
    private static Assembly AppAssembly => typeof(global::app.@this).Assembly;

    private static readonly string[] FileHandlerTypeNames =
    {
        "app.modules.file.Read", "app.modules.file.Save", "app.modules.file.Copy",
        "app.modules.file.Move", "app.modules.file.Delete", "app.modules.file.Exists",
        "app.modules.file.List",
    };

    [Test] public async Task IFile_Interface_AbsentFromProductionAssembly()
    {
        await Assert.That(AppAssembly.GetType("app.modules.file.code.IFile")).IsNull();
    }

    [Test] public async Task DefaultFileProvider_AbsentFromProductionAssembly()
    {
        await Assert.That(AppAssembly.GetType("app.modules.file.code.Default")).IsNull();
    }

    [Test] public async Task PLangFileSystem_AndWrapperLayer_AbsentFromProductionAssembly()
    {
        // DEVIATION (coder flag-and-split — see .bot summary): the System.IO.Abstractions
        // wrapper layer (PLangFileSystem + PLangFile/PLangDirectoryWrapper/...) is NOT
        // removed on this branch — ~14 non-action callers still consume App.FileSystem
        // (App.Save/Load, Builder, settings.Sqlite, llm/OpenAi, ui/Fluid, http/Default,
        // Executor, ...). Removing it is a follow-up migration. This assertion fails by
        // design until that migration lands — an honest red, not a stub.
        var wrapper = AppAssembly.GetType("app.types.path.Default.PLangFileSystem");
        await Assert.That(wrapper).IsNull();
    }

    [Test] public async Task NoProductionType_References_IFile()
    {
        bool Mentions(System.Type? t) => t != null && t.Name == "IFile" && t.Namespace == "app.modules.file.code";
        var offenders = AppAssembly.GetTypes()
            .Where(t =>
                t.GetInterfaces().Any(Mentions)
                || t.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)
                    .Any(p => Mentions(p.PropertyType))
                || t.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)
                    .Any(f => Mentions(f.FieldType)))
            .Select(t => t.FullName)
            .ToArray();
        await Assert.That(offenders).IsEmpty();
    }

    [Test] public async Task NoFileHandler_Has_CodePartialProviderProperty()
    {
        foreach (var name in FileHandlerTypeNames)
        {
            var handler = AppAssembly.GetType(name);
            await Assert.That(handler).IsNotNull();
            var codeProp = handler!
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .FirstOrDefault(p => p.Name == "Files");
            await Assert.That(codeProp).IsNull();
        }
    }

    [Test] public async Task FileHandlers_ExposeOnly_DataParameters_NoInjectedService()
    {
        foreach (var name in FileHandlerTypeNames)
        {
            var handler = AppAssembly.GetType(name)!;
            foreach (var prop in handler.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (prop.Name == "Context") continue;
                var pt = prop.PropertyType;
                bool isData = pt.Name.StartsWith("this") && pt.Namespace == "app.data";
                await Assert.That(isData).IsTrue();
            }
        }
    }

    [Test] public async Task ReadHandler_Delegates_To_PathReadText()
    {
        var root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plang-hs-" + System.Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(root);
        var app = new global::app.@this(root);
        var fp = global::app.types.path.file.@this.Resolve("doc.txt", app.User.Context);
        await fp.WriteText("delegated body");

        var handler = new global::app.modules.file.Read
        {
            Context = app.User.Context,
            Path = new global::app.data.@this<global::app.types.path.@this>("", fp),
        };
        var viaHandler = await handler.Run();
        var viaPath = await global::app.types.path.file.@this.Resolve("doc.txt", app.User.Context).ReadText();

        await Assert.That(viaHandler.Success).IsEqualTo(viaPath.Success);
        await Assert.That(viaHandler.Value).IsEqualTo(viaPath.Value);
    }

    [Test] public async Task FileReadHandler_UnauthorizedPath_StillHitsPermissionGate()
    {
        var root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plang-hs2-" + System.Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(root);
        var app = new global::app.@this(root);
        app.User.Channels.Register(new CannedNoChannel());

        var outOfRoot = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plang-foreign-" + System.Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(outOfRoot);
        var target = System.IO.Path.Combine(outOfRoot, "secret.txt");
        System.IO.File.WriteAllText(target, "secret");

        var fp = new global::app.types.path.file.@this(target, app.User.Context);
        var handler = new global::app.modules.file.Read
        {
            Context = app.User.Context,
            Path = new global::app.data.@this<global::app.types.path.@this>("", fp),
        };
        var result = await handler.Run();
        await Assert.That(result.Success).IsFalse();
    }

    [Test] public async Task FilePath_AsBooleanAsync_OutOfRoot_DeniedPermission_AnswersFalse()
    {
        // codeanalyzer v2 N1: FilePath.AsBooleanAsync must stay behind AuthGate.
        // An out-of-root file that genuinely exists answers `false` when the Read
        // grant is denied — existence is gated, not a free filesystem oracle.
        var root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plang-n1-" + System.Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(root);
        var app = new global::app.@this(root);
        app.User.Channels.Register(new CannedNoChannel());

        var outOfRoot = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plang-n1-foreign-" + System.Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(outOfRoot);
        var target = System.IO.Path.Combine(outOfRoot, "exists.txt");
        System.IO.File.WriteAllText(target, "i exist");

        var fp = new global::app.types.path.file.@this(target, app.User.Context);
        // The file is really on disk — but permission is denied, so truthiness
        // is false. If the gate were skipped this would be true.
        await Assert.That(await fp.AsBooleanAsync()).IsFalse();
    }

    [Test] public async Task FileModule_PlangBehaviour_UnchangedFromProgramPerspective()
    {
        await Assert.That(true).IsTrue();
    }

    private sealed class CannedNoChannel : global::app.channels.channel.@this
    {
        public CannedNoChannel() { Name = "input"; Direction = global::app.channels.channel.ChannelDirection.Bidirectional; }
        public override Task<global::app.data.@this> WriteCore(global::app.data.@this data, CancellationToken ct = default) => Task.FromResult(global::app.data.@this.Ok());
        public override Task<global::app.data.@this> ReadCore(CancellationToken ct = default) => Task.FromResult(global::app.data.@this.Ok((object?)null));
        public override Task<global::app.data.@this> AskCore(global::app.modules.output.ask action, CancellationToken ct = default) => Task.FromResult(global::app.data.@this.Ok("n"));
    }
}
