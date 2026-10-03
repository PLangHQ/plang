using System.Text.Json;
using System.Text.Json.Nodes;

namespace app.module.browser.type.cdp.session;

/// <summary>
/// One page's session on a DevTools connection (<c>Target.attachToTarget</c>, flattened): what is asked goes to that
/// page, and what the page says on its own comes to <see cref="Heard"/> — over the browser's one pipe.
/// </summary>
public sealed class @this : global::app.type.item.@this
{
    private readonly cdp.@this _cdp;
    private readonly string _id;

    internal @this(cdp.@this cdp, string target, string id)
    {
        _cdp = cdp;
        _id = id;
        Target = target;
        _cdp.Heard += (method, parameters, session) =>
            session == _id && Heard is { } hear ? hear(method, parameters) : Task.CompletedTask;
    }

    /// <summary>DevTools' id for the page.</summary>
    internal string Target { get; }

    /// <summary>What the page said on its own: the event's method and its params.</summary>
    internal event Func<string, JsonElement, Task>? Heard;

    internal Task<JsonElement> Ask(string method, JsonObject parameters, TimeSpan? within = null)
        => _cdp.Ask(method, parameters, _id, within);

    internal Task Tell(string method, JsonObject parameters) => _cdp.Tell(method, parameters, _id);

    /// <summary>The session ends; the page stays.</summary>
    internal Task Detach() => _cdp.Tell("Target.detachFromTarget", new JsonObject { ["sessionId"] = _id });

    public override string ToString() => $"DevTools session on {Target}";
}
