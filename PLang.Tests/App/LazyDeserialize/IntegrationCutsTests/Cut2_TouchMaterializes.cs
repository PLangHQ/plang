using System.Numerics;
using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using data = global::app.data.@this;
using type = global::app.type.@this;
using filechannel = global::app.channel.type.file.@this;

namespace PLang.Tests.App.LazyDeserialize.IntegrationCutsTests;

// Cut 2 — touch materialises correctly. A `config.json` read as
// `{object, json}`; a `report.csv` read as `{table, csv}`; a number read
// as `{number, biginteger}`; an image read as `{image, png}`. **Stamping
// the type does not parse** — untouched, `%cfg%` is still the json
// *string* even though `type=object`. Touched, `%cfg.port%` materialises
// and returns the parsed field. The csv navigates by row/column once
// touched.
public class Cut2_TouchMaterializes
{
    // 1x1 png.
    private const string Png1x1 =
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAIAAACQd1PeAAAADElEQVR4nGNgYGAAAAAEAAH2FzhVAAAAAElFTkSuQmCC";

    private static global::app.@this NewApp(out string root)
    {
        root = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang-cut2-" + System.Guid.NewGuid().ToString("N")[..8]);
        return new(root);
    }

    [Test] public async Task Cut2_ConfigJson_UntouchedIsRawString_NavigatedReturnsField()
    {
        await using var app = NewApp(out var root);
        var p = new filepath(System.IO.Path.Combine(root, "config.json"), app.User.Context);
        await (await p.WriteText("{\"port\":8080}")).IsSuccess();

        var d = await new filechannel(p).Read();
        await Assert.That(d.Peek()).IsEqualTo((object)"{\"port\":8080}"); // untouched = raw
        await Assert.That(d.MaterializeCount).IsEqualTo(0);
        await Assert.That((await (await d.GetChild("port")).Value())?.ToString()).IsEqualTo("8080"); // navigate materializes
        await Assert.That(d.MaterializeCount).IsEqualTo(1);
    }

    [Test] public async Task Cut2_ReportCsv_UntouchedIsRawString_NavigatedReturnsRowColumn()
    {
        await using var app = NewApp(out var root);
        var p = new filepath(System.IO.Path.Combine(root, "report.csv"), app.User.Context);
        await (await p.WriteText("name,age\nAda,36\n")).IsSuccess();

        var d = await new filechannel(p).Read();
        await Assert.That(d.Peek()).IsEqualTo((object)"name,age\nAda,36\n"); // untouched = raw csv
        await Assert.That(d.MaterializeCount).IsEqualTo(0);
        await Assert.That((await (await d.GetChild("rows")).GetChild("0").GetChild("name").Value())?.ToString()).IsEqualTo("Ada");
    }

    [Test] public async Task Cut2_BigIntegerString_ReadsLossless_OnArithmetic()
    {
        await using var app = NewApp(out _);
        var ctx = app.User.Context;
        const string big = "9999999999999999999999";
        var d = data.FromRaw(big, type.Create("number", "biginteger", context: ctx), ctx, "n");
        await Assert.That((await d.Value())).IsTypeOf<BigInteger>();
        await Assert.That((BigInteger)(await d.Value())!).IsEqualTo(BigInteger.Parse(big)); // lossless
    }

    // The image materialises only when its value is touched (e.g. width), not
    // at scalar/output access — scalar hands back the raw bytes.
    [Test] public async Task Cut2_ImagePng_MaterializesOnly_WhenWidthRead()
    {
        await using var app = NewApp(out _);
        var ctx = app.User.Context;
        var bytes = System.Convert.FromBase64String(Png1x1);
        var d = data.FromRaw(bytes, type.Create("image", "png", context: ctx), ctx, "img");

        await Assert.That(d.Peek() is byte[]).IsTrue();   // scalar = raw bytes, no decode
        await Assert.That(d.MaterializeCount).IsEqualTo(0);

        await Assert.That((await d.Value())).IsTypeOf<global::app.type.image.@this>(); // touch materializes
        await Assert.That(d.MaterializeCount).IsEqualTo(1);
    }
}
