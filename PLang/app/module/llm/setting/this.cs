namespace app.module.llm.setting;

/// <summary>
/// The llm module's own settings — <c>%!llm.setting.cache%</c>: what every llm action takes when neither the step
/// nor the action's own setting says.
/// </summary>
public sealed class @this : global::app.type.item.setting.module.@this
{
    /// <summary>Whether an answer is cached. A build with <c>--build={"cache":false}</c> turns it off.</summary>
    [Out, Store] public global::app.type.item.@bool.@this Cache { get; set; } = true;

    /// <summary>The key a query is sent with — <c>%!llm.setting.key%</c>; when none is saved, the
    /// <c>OPENAI_API_KEY</c> environment variable's. Never shown.</summary>
    [Out, Store, Sensitive] public global::app.type.item.text.@this Key { get; set; }
        = System.Environment.GetEnvironmentVariable("OPENAI_API_KEY") ?? "";

    /// <summary>Where a query is sent — when none is saved, the <c>OPENAI_API_ENDPOINT</c> environment variable's,
    /// else OpenAI's.</summary>
    [Out, Store] public global::app.type.item.text.@this Endpoint { get; set; }
        = System.Environment.GetEnvironmentVariable("OPENAI_API_ENDPOINT") is { Length: > 0 } endpoint
            ? endpoint : "https://api.openai.com/v1/chat/completions";

    /// <summary>The model a query asks when its step names none.</summary>
    [Out, Store] public global::app.type.item.text.@this Model { get; set; } = "gpt-5.4-nano";
}
