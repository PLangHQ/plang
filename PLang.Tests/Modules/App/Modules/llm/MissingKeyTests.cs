namespace PLang.Tests.App.Modules.llm;

/// <summary>
/// A program that uses an LLM with no key asks its user once — secretly — saves the answer on the user's own settings
/// row, and never asks again; with no one to ask it fails naming both ways to give one. The key is never shown: the
/// answer is a secret, written as **** everywhere but plang's own store.
/// </summary>
public class MissingKeyTests
{
    private const string Pasted = "test-key-4711";

    private static (global::app.@this App, System.IO.MemoryStream Written) Asking(string? typed)
    {
        var app = new global::app.@this("/tmp/llmkey-" + System.Guid.NewGuid().ToString("N")[..8]).Testing();
        var written = new System.IO.MemoryStream();
        var user = app.actor.list.User;
        user.Channel.Register(new global::app.channel.type.stream.@this(global::app.channel.list.@this.Output, written,
            global::app.channel.ChannelDirection.Output, ownsStream: false) { Mime = "text/plain", Framed = true });
        if (typed != null)
            user.Channel.Register(new global::app.channel.type.stream.@this(global::app.channel.list.@this.Input,
                new System.IO.MemoryStream(System.Text.Encoding.UTF8.GetBytes(typed)), global::app.channel.ChannelDirection.Input, ownsStream: true));
        return (app, written);
    }

    [Test]
    public async Task AMissingKey_IsAskedOnce_Saved_AndNeverShown()
    {
        var (app, written) = Asking(Pasted + "\n");
        await using var _ = app;
        var ctx = app.actor.list.User.Context;

        var first = await new global::app.module.llm.setting.@this { Key = "" }.Asked(ctx);
        var again = await ctx.Setting.Of<global::app.module.llm.setting.@this>().Asked(ctx);
        var stored = await new global::app.type.item.variable.@this("%!llm.setting.key%").Start(ctx);

        await first.IsSuccess();
        await Assert.That((await first.Value())!.ToString()).IsEqualTo(Pasted);
        await again.IsSuccess();   // the input is spent: asking again would have failed
        await Assert.That((await again.Value())!.ToString()).IsEqualTo(Pasted);
        await Assert.That((await stored.Value())?.ToString()).IsEqualTo(Pasted);
        var shown = System.Text.Encoding.UTF8.GetString(written.ToArray());
        await Assert.That(shown).Contains("needs a key");
        await Assert.That(shown).DoesNotContain(Pasted);
    }

    [Test]
    public async Task NoOneToAsk_FailsNamingBothWays()
    {
        var (app, _) = Asking(null);
        await using var held = app;
        var ctx = app.actor.list.User.Context;
        ctx.Actor!.Channel.Remove(global::app.channel.list.@this.Input, ctx);

        var asked = await new global::app.module.llm.setting.@this { Key = "" }.Asked(ctx);

        await asked.IsFailure();
        await Assert.That(asked.Error!.Key).IsEqualTo("MissingLlmKey");
        await Assert.That(asked.Error.Message).Contains("%!llm.setting.key%");
        await Assert.That(asked.Error.Message).Contains("OPENAI_API_KEY");
    }

    // a secret writes **** in a text template and every view but plang's store, where it is whole
    [Test]
    public async Task ASecret_ShowsMasked_ButIsKeptWhole()
    {
        await using var app = new global::app.@this("/app").Testing();
        var ctx = app.actor.list.User.Context;
        await ctx.Variable.Set(new global::app.data.@this("pw", new global::app.type.item.secret.@this(Pasted), context: ctx));

        var set = Make.Action(ctx, "variable", "set", Make.Param(ctx, "Name", "shown", "variable"), ("Value", "password: %pw%"));
        await (await set.Start(ctx)).IsSuccess();

        await Assert.That((await (await ctx.Variable.Get("shown")).Value())?.ToString()).IsEqualTo("password: ****");
        using var store = new System.IO.MemoryStream();
        await (await ctx.App.type.list.Kind("plang").Encode(store, await ctx.Variable.Get("pw"), ctx, global::app.View.Store)).IsSuccess();
        await Assert.That(System.Text.Encoding.UTF8.GetString(store.ToArray())).Contains(Pasted);
        using var debug = new System.IO.MemoryStream();
        await (await ctx.App.type.list.Kind("plang").Encode(debug, await ctx.Variable.Get("pw"), ctx, global::app.View.Debug)).IsSuccess();
        await Assert.That(System.Text.Encoding.UTF8.GetString(debug.ToArray())).DoesNotContain(Pasted);
    }
}
