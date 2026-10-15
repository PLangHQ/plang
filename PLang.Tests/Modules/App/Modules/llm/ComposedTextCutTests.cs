using PLangEngine = global::app.@this;

namespace PLang.Tests.App.Modules.llm;

/// <summary>
/// Stage 1b of the llm-writer plan: a value inside a sentence ("summarize %doc%") is cut the same as
/// %doc% alone — the IWriter door (BeginText/EndText) hands the llm writer the literals and the embedded
/// value in order, so each VALUE is cut, not the whole composed string. The distinguishing proof: the
/// literal prefix rides WHOLE and the marker counts the VALUE's overflow (150), never the whole string's
/// (160). Writer-level and deterministic — no network.
/// </summary>
public class ComposedTextCutTests
{
    private static global::app.type.item.text.@this Template(string value)
        => new(value, new global::app.type.item.template.kind.plang.@this());

    private static string Composed(global::app.type.item.text.@this template, int length,
        global::app.actor.context.@this ctx)
    {
        using var ms = new System.IO.MemoryStream();
        var w = new global::app.module.llm.Writer(ms, System.Text.Encoding.UTF8,
            global::app.type.item.culture.@this.Machine, length, rows: 100);
        template.Output(w, global::app.View.Out, ctx).AsTask().GetAwaiter().GetResult();
        return System.Text.Encoding.UTF8.GetString(ms.ToArray());
    }

    [Test]
    public async Task PartialTemplate_CutsTheValue_LiteralWhole_MarkerIsTheValuesOverflow()
    {
        await using var app = new PLangEngine("/test").Testing();
        var ctx = app.actor.list.User.Context;
        await ctx.Variable.Set("doc", new string('a', 200));   // 150 over a length of 50

        var result = Composed(Template("summarize %doc%"), 50, ctx);

        // the literal rides whole, then the value's first 50 chars
        await Assert.That(result).StartsWith("summarize " + new string('a', 50));
        // the marker counts the VALUE's overflow (200-50=150), not the whole composed string's (210-50=160)
        await Assert.That(result).Contains("--- 150 more characters ---");
        await Assert.That(result).DoesNotContain("160 more");
    }

    [Test]
    public async Task PartialTemplate_ShortValue_GoesWhole()
    {
        await using var app = new PLangEngine("/test").Testing();
        var ctx = app.actor.list.User.Context;
        await ctx.Variable.Set("name", "World");

        var result = Composed(Template("Hello %name%"), 50, ctx);

        await Assert.That(result).IsEqualTo("Hello World");
        await Assert.That(result).DoesNotContain("more characters");
    }
}
