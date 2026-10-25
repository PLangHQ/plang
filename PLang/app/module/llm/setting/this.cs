namespace app.module.llm.setting;

/// <summary>
/// The llm module's own settings — <c>%!llm.setting.cache%</c>: what every llm action takes when neither the step
/// nor the action's own setting says.
/// </summary>
public sealed class @this : global::app.type.item.setting.module.@this, IKey
{
    /// <summary>Born holding its type — the app's list hands it its own class.</summary>
    public @this(global::app.type.@this type) : base(type) { }

    /// <summary>A settings class is a type but never LLM vocabulary — kept out of the catalog.</summary>
    public static bool Internal => true;

    /// <summary>Whether an answer kept from before is used — used, unless a step or this setting skips it.</summary>
    [Out, Store] public global::app.type.item.choice.@this<global::app.module.cache.type.cache> Cache { get; set; }
        = global::app.module.cache.type.cache.use;

    /// <summary>The key a query is sent with — <c>%!llm.setting.key%</c>; when none is saved, the
    /// <c>OPENAI_API_KEY</c> environment variable's. Never shown.</summary>
    [Out, Store, Sensitive] public global::app.type.item.text.@this Key { get; set; }
        = System.Environment.GetEnvironmentVariable("OPENAI_API_KEY") ?? "";

    /// <summary>What a query asks for when no key is held.</summary>
    global::app.type.item.text.@this IKey.Question => "This program uses an LLM and needs a key. Paste it:";

    /// <summary>No key, and no one gave one.</summary>
    global::app.error.@this IKey.Missing => new(
        "This program uses an LLM and has no key, and no one gave one: set %!llm.setting.key% or the OPENAI_API_KEY environment variable.",
        "MissingLlmKey", 401);

    /// <summary>Where a query is sent — when none is saved, the <c>OPENAI_API_ENDPOINT</c> environment variable's,
    /// else OpenAI's.</summary>
    [Out, Store] public global::app.type.item.text.@this Endpoint { get; set; }
        = System.Environment.GetEnvironmentVariable("OPENAI_API_ENDPOINT") is { Length: > 0 } endpoint
            ? endpoint : "https://api.openai.com/v1/chat/completions";

    /// <summary>The model a query asks when its step names none.</summary>
    [Out, Store] public global::app.type.item.text.@this Model { get; set; } = "gpt-5.4-nano";
}
