namespace app.module.debug.setting.llm;

/// <summary>
/// Granular LLM trace flags — <c>%!debug.setting.llm%</c>. Each flag dumps one slice of the API exchange — set
/// only what you want to see. Flags compose: enabling System and Response gives the system prompt plus the raw
/// model response, with no user-message or schema noise.
/// Set via: <c>--debug={"llm":{"system":true,"user":true,"response":true,"schema":true}}</c>
/// </summary>
public sealed class @this : global::app.type.item.setting.@this
{
    /// <summary>Dump system messages from each LLM API call.</summary>
    [Out, Store] public global::app.type.item.@bool.@this System { get; set; } = false;

    /// <summary>Dump user (and any non-system) messages from each LLM API call.</summary>
    [Out, Store] public global::app.type.item.@bool.@this User { get; set; } = false;

    /// <summary>Dump the raw response string returned by the LLM API.</summary>
    [Out, Store] public global::app.type.item.@bool.@this Response { get; set; } = false;

    /// <summary>Dump the JSON Schema string passed via the format instruction.</summary>
    [Out, Store] public global::app.type.item.@bool.@this Schema { get; set; } = false;
}
