namespace app.builder.Types.Spec;

/// <summary>
/// Structured Example for the LLM builder catalog. Authored by an action class
/// via <c>public static Example[] ExamplesForLlm()</c> and rendered by
/// <see cref="Schema.@this.Render(Example)"/> into the formal-language string
/// the LLM sees in the catalog "e.g. ..." line.
///
/// Why structured: the renderer derives all type tags (<c>[path]</c>, <c>[string]</c>,
/// <c>[list&lt;action&gt;]</c>) from reflection on the action class, and emits
/// nested-action JSON shapes from <see cref="Action"/> values. The author
/// writes meaning (which action, which parameter, what value); the framework
/// writes syntax. Drift between Examples and the type catalog becomes
/// structurally impossible.
/// </summary>
/// <param name="UserIntent">The PLang step text as a developer would write it
/// — e.g. "read %path%, on error key 404, write out missing".</param>
/// <param name="Chain">The action chain the example expands to. Top-level
/// peers are joined with " | "; modifiers attach to their action via the
/// <see cref="Action.Modifiers"/> collection.</param>
public sealed record Example(string UserIntent, Action[] Chain);
