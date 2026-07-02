using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using PLangPath = global::app.type.path.@this;
using FilePath = global::app.type.path.file.@this;
using Conversion = global::app.type.catalog.@this;

namespace PLang.Tests.App.Types.PathTests;

/// <summary>
/// PLang <c>path</c> type-mapper dispatches through the scheme registry.
/// </summary>
public class PathTypeMapperTests
{
    private static (global::app.@this app, global::app.actor.context.@this context) MakeApp()
    {
        var dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plang-tm-" + System.Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(dir);
        var app = TestApp.Create(dir);
        return (app, app.User.Context);
    }

    [Test] public async Task PathParameter_SchemedFileValue_ResolvesTo_FilePath()
    {
        var (_, context) = MakeApp();
        var (value, error) = Conversion.TryConvert("file:///abs/x.txt", typeof(PLangPath), context);
        await Assert.That(error).IsNull();
        await Assert.That(value).IsTypeOf<FilePath>();
    }

    [Test] public async Task PathParameter_BareValue_ResolvesTo_FilePath()
    {
        var (_, context) = MakeApp();
        var (value, error) = Conversion.TryConvert("/abs/x.txt", typeof(PLangPath), context);
        await Assert.That(error).IsNull();
        await Assert.That(value).IsTypeOf<FilePath>();
    }

    [Test] public async Task PathParameter_UnknownScheme_BecomesDataFail_NoExceptionEscape()
    {
        var (_, context) = MakeApp();
        // s3 is not registered — the type-mapper must catch SchemeNotRegistered
        // and shape it as an Error, never let the exception escape.
        var (value, error) = Conversion.TryConvert("s3://bucket/key", typeof(PLangPath), context);
        await Assert.That(value).IsNull();
        await Assert.That(error).IsNotNull();
        await Assert.That(error!.Key).IsEqualTo("SchemeNotRegistered");
    }

    [Test] public async Task PathParameter_RelativeValue_ResolvesAgainstGoalDirectory()
    {
        var (_, context) = MakeApp();
        var (value, error) = Conversion.TryConvert("rel.txt", typeof(PLangPath), context);
        await Assert.That(error).IsNull();
        var fp = (FilePath)value!;
        // Relative resolution preserves Raw and produces an absolute path.
        await Assert.That(fp.Raw).IsEqualTo("rel.txt");
        await Assert.That(System.IO.Path.IsPathRooted(fp.Absolute)).IsTrue();
    }

    [Test] public async Task FileReadStep_StringPathParameter_StillRuns_AfterRegistryRewire()
    {
        var (app, context) = MakeApp();
        var filePath = FilePath.Resolve("greeting.txt", context);
        await filePath.WriteText("hello from a string param");

        // Resolve a Path the way a handler parameter does, then run file.read.
        var (value, _) = Conversion.TryConvert("greeting.txt", typeof(PLangPath), context);
        var read = new global::app.module.file.Read
        {
            Context = context,
            Path = new global::app.data.@this<PLangPath>("", (PLangPath)value!),
        };
        var result = await read.Run();
        await result.IsSuccess();
        await Assert.That((await result.Value())?.ToString()).IsEqualTo("hello from a string param");
    }
}
