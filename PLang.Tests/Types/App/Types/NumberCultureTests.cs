namespace PLang.Tests.App.Types;

// A number reads and writes in the invariant culture, whatever the machine's: on an Icelandic machine (where the
// decimal separator is a comma) 1.5 is still written "1.5", and "3.14" still reads as a number.
[NotInParallel(nameof(NumberCultureTests))]
public class NumberCultureTests
{
    private static async Task<T> InIcelandic<T>(Func<Task<T>> run)
    {
        var culture = System.Globalization.CultureInfo.CurrentCulture;
        System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("is-IS");
        try { return await run(); }
        finally { System.Globalization.CultureInfo.CurrentCulture = culture; }
    }

    [Test]
    public async Task ADecimal_IsWritten_WithAPoint()
    {
        await using var app = new global::app.@this("/tmp/numculture", global::PLang.Tests.Parent.Actor).Testing();
        var ctx = app.actor.list.User.Context;

        var written = await InIcelandic(async () =>
        {
            var number = global::app.type.item.@this.Create(1.5, ctx);
            using var stream = new System.IO.MemoryStream();
            await (await app.type.list["text"].kind.Encode(stream, ctx.Ok(number), ctx)).IsSuccess();
            return (number.ToString(), System.Text.Encoding.UTF8.GetString(stream.ToArray()));
        });

        await Assert.That(written.Item1).IsEqualTo("1.5");
        await Assert.That(written.Item2).IsEqualTo("1.5");
    }

    [Test]
    public async Task ADecimalInText_ReadsAsANumber()
    {
        await using var app = new global::app.@this("/tmp/numculture", global::PLang.Tests.Parent.Actor).Testing();
        var ctx = app.actor.list.User.Context;

        var read = await InIcelandic(async () => (await ctx.Format("text/plain").Deserialize<global::app.type.item.number.@this>("3.14", ctx).Value())?.ToString());

        await Assert.That(read).IsEqualTo("3.14");
    }
}
