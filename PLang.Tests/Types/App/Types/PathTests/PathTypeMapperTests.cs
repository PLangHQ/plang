using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using PLangPath = global::app.type.item.path.@this;
using FilePath = global::app.type.item.path.file.@this;

namespace PLang.Tests.App.Types.PathTests;

/// <summary>
/// PLang <c>path</c> builds itself from a raw string through its own <c>Create</c>
/// courier — scheme dispatch via the registry, an unknown scheme declining onto
/// <c>data.Fail</c> (no central conversion door).
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

    // Build a path the way a handler parameter does: the type constructs itself from
    // the raw string, landing any failure on the carrier Data.
    private static (PLangPath? value, global::app.error.IError? error) Build(string raw, global::app.actor.context.@this ctx)
    {
        var d = new global::app.data.@this("", new global::app.type.item.@null.@this("path", null), context: ctx);
        var v = PLangPath.Create(raw, d);
        return (v, d.Error);
    }

    [Test] public async Task PathParameter_SchemedFileValue_ResolvesTo_FilePath()
    {
        var (_, context) = MakeApp();
        var (value, error) = Build("file:///abs/x.txt", context);
        await Assert.That(error).IsNull();
        // (object) cast: path has an implicit string operator; assert on the runtime type.
        await Assert.That((object?)value).IsTypeOf<FilePath>();
    }

    [Test] public async Task PathParameter_BareValue_ResolvesTo_FilePath()
    {
        var (_, context) = MakeApp();
        var (value, error) = Build("/abs/x.txt", context);
        await Assert.That(error).IsNull();
        await Assert.That((object?)value).IsTypeOf<FilePath>();
    }

    [Test] public async Task PathParameter_UnknownScheme_BecomesDataFail_NoExceptionEscape()
    {
        var (_, context) = MakeApp();
        // s3 is not registered — Create must catch SchemeNotRegistered and shape it
        // as an Error on the Data, never let the exception escape.
        var (value, error) = Build("s3://bucket/key", context);
        await Assert.That(value).IsNull();
        await Assert.That(error).IsNotNull();
        await Assert.That(error!.Key).IsEqualTo("SchemeNotRegistered");
    }

    [Test] public async Task PathParameter_RelativeValue_ResolvesAgainstGoalDirectory()
    {
        var (_, context) = MakeApp();
        var (value, error) = Build("rel.txt", context);
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
        var (value, _) = Build("greeting.txt", context);
        var read = new global::app.module.action.file.Read(context) { Path = new global::app.data.@this<PLangPath>("", (PLangPath)value!),
        };
        var result = await read.Run();
        await result.IsSuccess();
        await Assert.That((await result.Value())?.ToString()).IsEqualTo("hello from a string param");
    }
}
