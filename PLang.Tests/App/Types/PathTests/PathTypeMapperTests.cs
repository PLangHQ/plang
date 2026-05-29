using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using PLangPath = global::app.type.path.@this;
using FilePath = global::app.type.path.file.@this;
using Conversion = global::app.type.list.@this;

namespace PLang.Tests.App.Types.PathTests;

/// <summary>
/// PLang <c>path</c> type-mapper dispatches through the scheme registry.
/// </summary>
public class PathTypeMapperTests
{
    private static (global::app.@this app, global::app.actor.context.@this ctx) MakeApp()
    {
        var dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plang-tm-" + System.Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(dir);
        var app = new global::app.@this(dir);
        return (app, app.User.Context);
    }

    [Test] public async Task PathParameter_SchemedFileValue_ResolvesTo_FilePath()
    {
        var (_, ctx) = MakeApp();
        var (value, error) = Conversion.TryConvertTo("file:///abs/x.txt", typeof(PLangPath), ctx);
        await Assert.That(error).IsNull();
        await Assert.That(value).IsTypeOf<FilePath>();
    }

    [Test] public async Task PathParameter_BareValue_ResolvesTo_FilePath()
    {
        var (_, ctx) = MakeApp();
        var (value, error) = Conversion.TryConvertTo("/abs/x.txt", typeof(PLangPath), ctx);
        await Assert.That(error).IsNull();
        await Assert.That(value).IsTypeOf<FilePath>();
    }

    [Test] public async Task PathParameter_UnknownScheme_BecomesDataFail_NoExceptionEscape()
    {
        var (_, ctx) = MakeApp();
        // s3 is not registered — the type-mapper must catch SchemeNotRegistered
        // and shape it as an Error, never let the exception escape.
        var (value, error) = Conversion.TryConvertTo("s3://bucket/key", typeof(PLangPath), ctx);
        await Assert.That(value).IsNull();
        await Assert.That(error).IsNotNull();
        await Assert.That(error!.Key).IsEqualTo("SchemeNotRegistered");
    }

    [Test] public async Task PathParameter_RelativeValue_ResolvesAgainstGoalDirectory()
    {
        var (_, ctx) = MakeApp();
        var (value, error) = Conversion.TryConvertTo("rel.txt", typeof(PLangPath), ctx);
        await Assert.That(error).IsNull();
        var fp = (FilePath)value!;
        // Relative resolution preserves Raw and produces an absolute path.
        await Assert.That(fp.Raw).IsEqualTo("rel.txt");
        await Assert.That(System.IO.Path.IsPathRooted(fp.Absolute)).IsTrue();
    }

    [Test] public async Task FileReadStep_StringPathParameter_StillRuns_AfterRegistryRewire()
    {
        var (app, ctx) = MakeApp();
        var filePath = FilePath.Resolve("greeting.txt", ctx);
        await filePath.WriteText("hello from a string param");

        // Resolve a Path the way a handler parameter does, then run file.read.
        var (value, _) = Conversion.TryConvertTo("greeting.txt", typeof(PLangPath), ctx);
        var read = new global::app.modules.file.Read
        {
            Context = ctx,
            Path = new global::app.data.@this<PLangPath>("", (PLangPath)value!),
        };
        var result = await read.Run();
        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Value).IsEqualTo("hello from a string param");
    }
}
