using app.module.browser.code;

namespace app.module.browser;

/// <summary>
/// Gives a running browser (from <c>browser.start</c>) one input event, as one line of JSON:
/// <c>{"mouse":"move|down|up|wheel","x":…,"y":…,"button":"left|right|middle|none","clicks":…,"dx":…,"dy":…,"mods":…}</c>,
/// <c>{"key":"down|up","name":"Enter","vk":13,"mods":…}</c>, <c>{"text":"a"}</c>,
/// <c>{"nav":"back|forward|reload"}</c> or <c>{"nav":"url","url":"https://…"}</c>. Anything else is ignored.
/// </summary>
[Action("send", Cacheable = false)]
public partial class send : IContext
{
    /// <summary>The input event, one line of JSON.</summary>
    public partial data.@this Data { get; init; }

    /// <summary>The running browser, from <c>browser.start</c>.</summary>
    public partial data.@this<Browser> Browser { get; init; }

    [Code]
    public partial IBrowser Provider { get; }

    public async Task<data.@this> Start() => await Provider.Send(this);
}
