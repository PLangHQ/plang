using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using System.Linq;
using System.Reflection;

namespace PLang.Tests.App.Types.PathTests;

/// <summary>
/// Handler one-liners and the death of IFile / DefaultFileProvider /
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
        // The System.IO.Abstractions wrapper layer (PLangFileSystem + PLang*Wrapper
        // siblings) was deleted as part of the path-polymorphism work (Stage 8).
        // Scan by simple name across the whole assembly so a reintroduction under
        // any namespace gets caught — a single-namespace probe would miss a
        // refactor that moves the type. Same pattern as
        // NoProductionType_References_IFile below.
        string[] wrapperNames =
        {
            "PLangFileSystem",
            "PLangFile",
            "PLangDirectory",
            "PLangDirectoryWrapper",
            "PLangPath",
        };
        var offenders = AppAssembly.GetTypes()
            .Where(t => wrapperNames.Contains(t.Name))
            .Select(t => t.FullName)
            .ToList();
        await Assert.That(offenders.Count).IsEqualTo(0);
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
        var fp = global::app.type.path.file.@this.Resolve("doc.txt", app.User.Context);
        await fp.WriteText("delegated body");

        var handler = new global::app.modules.file.Read
        {
            Context = app.User.Context,
            Path = new global::app.data.@this<global::app.type.path.@this>("", fp),
        };
        var viaHandler = await handler.Run();
        var viaPath = await global::app.type.path.file.@this.Resolve("doc.txt", app.User.Context).ReadText();

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

        var fp = new global::app.type.path.file.@this(target, app.User.Context);
        var handler = new global::app.modules.file.Read
        {
            Context = app.User.Context,
            Path = new global::app.data.@this<global::app.type.path.@this>("", fp),
        };
        var result = await handler.Run();
        await Assert.That(result.Success).IsFalse();
    }

    [Test] public async Task FilePath_AsBooleanAsync_OutOfRoot_DeniedPermission_AnswersFalse()
    {
        // FilePath.AsBooleanAsync must stay behind AuthGate.
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

        var fp = new global::app.type.path.file.@this(target, app.User.Context);
        // The file is really on disk — but permission is denied, so truthiness
        // is false. If the gate were skipped this would be true.
        await Assert.That(await fp.AsBooleanAsync()).IsFalse();
    }

    private sealed class CannedNoChannel : global::app.channel.@this
    {
        public CannedNoChannel() { Name = "input"; Direction = global::app.channel.ChannelDirection.Bidirectional; }
        public override Task<global::app.data.@this> Write(global::app.data.@this data, CancellationToken ct = default) => Task.FromResult(global::app.data.@this.Ok());
        public override Task<global::app.data.@this> Read(CancellationToken ct = default) => Task.FromResult(global::app.data.@this.Ok((object?)null));
        public override Task<global::app.data.@this> Ask(global::app.modules.output.ask action, CancellationToken ct = default) => Task.FromResult(global::app.data.@this.Ok("n"));
    }
}
