using System.Text;
using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace PLang.Tests.App.LazyDeserialize.LazyDataTests;

/// <summary>
/// Stage 1b guard: a text with values inside it ("Hello %name%") written into a json channel is ONE json
/// string, byte for byte as today — the IWriter door (BeginText/EndText) gathers the literal and the
/// embedded value's text into a single string, never a run of json tokens. This is the regression guard
/// for the door's effect on json/plang: if it emitted the parts separately, this would be invalid json or
/// two strings.
/// </summary>
public class ComposedTextIntoJsonTests
{
    [Test]
    public async Task PartialTemplate_IntoJson_IsOneString()
    {
        await using var app = new global::app.@this("/test").Testing();
        var ctx = app.actor.list.User.Context;
        await ctx.Variable.Set("name", "World");

        var template = new global::app.type.item.text.@this(
            "Hello %name%", new global::app.type.item.template.kind.plang.@this());

        using var ms = new System.IO.MemoryStream();
        var written = await ctx.Format("application/json")
            .Encode(ms, new global::app.data.@this("body", template, context: ctx), ctx);

        await written.IsSuccess();
        await Assert.That(Encoding.UTF8.GetString(ms.ToArray())).IsEqualTo("\"Hello World\"");
    }
}
