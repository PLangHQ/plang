namespace app.module.llm.setting;

/// <summary>
/// The llm module's own settings — <c>%!llm.setting.cache%</c>: what every llm action takes when neither the step
/// nor the action's own setting says.
/// </summary>
public sealed class @this : global::app.type.item.setting.module.@this
{
    /// <summary>Whether an answer kept from before is used — used, unless a step or this setting skips it.</summary>
    [Out, Store] public global::app.type.item.choice.@this<global::app.module.cache.type.cache> Cache { get; set; }
        = new(global::app.module.cache.type.cache.use);

    /// <summary>The key a query is sent with — <c>%!llm.setting.key%</c>; when none is saved, the
    /// <c>OPENAI_API_KEY</c> environment variable's. Never shown.</summary>
    [Out, Store, Sensitive] public global::app.type.item.text.@this Key { get; set; }
        = System.Environment.GetEnvironmentVariable("OPENAI_API_KEY") ?? "";

    /// <summary>The key, as it stands after asking: one held (saved, or the environment's) answers at once; none held,
    /// the user is asked once, secretly, through the actor's ask, and the answer is saved on the asking actor's row —
    /// never asked again. With no one to ask, <c>MissingLlmKey</c>, naming both ways to give one.</summary>
    public async System.Threading.Tasks.Task<global::app.data.@this<global::app.type.item.text.@this>> Asked(global::app.actor.context.@this context)
    {
        if (Key.ToString().Length > 0) return context.Ok(Key);
        var asked = await new global::app.goal.step.action.@this(new global::app.module.output.ask(context)
        {
            Question = (global::app.type.item.text.@this)"This program uses an LLM and needs a key. Paste it:",
            Secret = (global::app.type.item.@bool.@this)true,
        }, context).Start(context);
        // no one answered — no input, or an input with nothing in it — or nothing was given
        if (!asked.Success || await asked.Value() is not global::app.type.item.secret.@this { Characters.Length: > 0 } secret)
            return context.Error<global::app.type.item.text.@this>(new global::app.error.Error(
                "This program uses an LLM and has no key, and no one gave one: set %!llm.setting.key% or the OPENAI_API_KEY environment variable."
                + (asked.Error is { } why ? $" ({why.Message})" : ""),
                "MissingLlmKey", 401));
        Key = secret.Characters;
        var saved = await context.Setting.Save(this);
        return saved.Success ? context.Ok(Key) : context.Error<global::app.type.item.text.@this>(saved.Error!);
    }

    /// <summary>Where a query is sent — when none is saved, the <c>OPENAI_API_ENDPOINT</c> environment variable's,
    /// else OpenAI's.</summary>
    [Out, Store] public global::app.type.item.text.@this Endpoint { get; set; }
        = System.Environment.GetEnvironmentVariable("OPENAI_API_ENDPOINT") is { Length: > 0 } endpoint
            ? endpoint : "https://api.openai.com/v1/chat/completions";

    /// <summary>The model a query asks when its step names none.</summary>
    [Out, Store] public global::app.type.item.text.@this Model { get; set; } = "gpt-5.4-nano";
}
