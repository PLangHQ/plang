namespace App.Catalog;

/// <summary>
/// One action node inside an <see cref="ExampleSpec"/>. Composes recursively:
/// values in <see cref="Params"/> may themselves be <see cref="ActionSpec"/> or
/// <see cref="ActionSpec"/>[] for parameters typed <c>action</c> / <c>list&lt;action&gt;</c>.
/// </summary>
/// <param name="Module">The module namespace, e.g. "file", "error", "output".</param>
/// <param name="Name">The action name within the module, e.g. "read", "handle", "write".</param>
/// <param name="Params">Parameter name → value. Value type drives rendering: strings
/// follow the formal-filter rules (quote on space/comma, %vars% bare); ActionSpec values
/// emit as nested-action JSON; primitive scalars emit as literals.</param>
/// <param name="Modifiers">Modifier actions wrapping this one (cache.wrap, error.handle,
/// timeout.after). Rendered after the action with " | " separator. Null = no modifiers.</param>
public sealed record ActionSpec(
    string Module,
    string Name,
    System.Collections.Generic.Dictionary<string, object?> Params,
    ActionSpec[]? Modifiers = null);
