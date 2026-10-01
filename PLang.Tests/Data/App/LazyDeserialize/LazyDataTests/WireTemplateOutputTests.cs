using System.Text;
using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using Wire = global::app.type.item.wire.@this;

namespace PLang.Tests.App.LazyDeserialize.LazyDataTests;

/// <summary>
/// A wire holding a template — a .pr row's dict with %variables%, marked by the builder — names its variables,
/// not their values. Written between actors (Out) into its own format (json, plang) it is decoded and its parts
/// render their variables; in what plang keeps (a .pr, Store) it is its slice as authored. A plain wire relays
/// its slice byte for byte in every view (Cut1_VerbatimPassthrough).
/// </summary>
public class WireTemplateOutputTests
{
    private const string Slice = "{\"deep\": \"%x%\", \"fixed\": \"pcm\"}";

    // The row as the goal reader captures it (type.Read): the slice, made by the row's marked type, holding
    // the variables the row lists — the build's own bytes (the goal reader grants them).
    private static Wire Template(global::app.actor.context.@this ctx)
        => ctx.App.type.list[new global::app.type.@this("dict", template: "plang"), ctx].Make(Slice,
            (global::app.type.item.wire.kind.plang.@this)ctx.App.type.list["wire"].kind["plang"]!,
            new global::app.type.item.variable.parser.@this(Slice).Variable, built: true);

    [Test]
    public async Task TemplateWire_IntoJson_WritesItsVariablesValues()
    {
        await using var app = new global::app.@this("/test").Testing();
        var ctx = app.actor.list.User.Context;
        await ctx.Variable.Set("x", "m-1");

        using var ms = new System.IO.MemoryStream();
        var written = await ctx.Format("application/json").Encode(ms, new global::app.data.@this("body", Template(ctx), context: ctx), ctx);

        await written.IsSuccess();
        await Assert.That(Encoding.UTF8.GetString(ms.ToArray())).IsEqualTo("{\"deep\":\"m-1\",\"fixed\":\"pcm\"}");
    }

    // What plang keeps holds the template as authored: a .pr write is its slice, byte for byte.
    [Test]
    public async Task TemplateWire_InAPr_IsItsSliceAsAuthored()
    {
        await using var app = new global::app.@this("/test").Testing();
        var ctx = app.actor.list.User.Context;
        await ctx.Variable.Set("x", "m-1");

        var pr = await ctx.Pr(Template(ctx));

        await Assert.That(pr).IsEqualTo(Slice + "\n");
    }
}
