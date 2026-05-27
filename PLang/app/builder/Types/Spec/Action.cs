namespace app.builder.Types.Spec;

/// <summary>
/// One action node inside an <see cref="Example"/>. Composes recursively:
/// values in <see cref="Params"/> may themselves be <see cref="Action"/> or
/// <see cref="Action"/>[] for parameters typed <c>action</c> / <c>list&lt;action&gt;</c>.
/// </summary>
/// <param name="Module">The module namespace, e.g. "file", "error", "output".</param>
/// <param name="Name">The action name within the module, e.g. "read", "handle", "write".</param>
/// <param name="Params">Parameter name → value. Value type drives rendering: strings
/// follow the formal-filter rules (quote on space/comma, %vars% bare); Action values
/// emit as nested-action JSON; primitive scalars emit as literals.</param>
/// <param name="Modifiers">Modifier actions wrapping this one (cache.wrap, error.handle,
/// timeout.after). Rendered after the action with " | " separator. Null = no modifiers.</param>
public sealed record Action(
    string Module,
    string Name,
    System.Collections.Generic.Dictionary<string, object?> Params,
    Action[]? Modifiers = null);
