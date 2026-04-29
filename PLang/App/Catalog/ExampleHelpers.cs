namespace App.Catalog;

/// <summary>
/// Compact author-side syntax for declaring <see cref="ExampleSpec"/> arrays
/// from a static <c>ExamplesForLlm()</c> method on an action class. Use
/// <c>using static App.Catalog.ExampleHelpers;</c> at the top of the file.
/// </summary>
public static class ExampleHelpers
{
    /// <summary>Build an <see cref="ExampleSpec"/> from user intent and a chain of actions.</summary>
    public static ExampleSpec Example(string userIntent, params ActionSpec[] chain)
        => new(userIntent, chain);

    /// <summary>
    /// Build an <see cref="ActionSpec"/>. The first arg is "module.action" — split on the
    /// first dot. Params is a name→value dictionary; modifiers attach via the optional arg.
    /// </summary>
    public static ActionSpec Action(
        string moduleAndAction,
        System.Collections.Generic.Dictionary<string, object?>? @params = null,
        ActionSpec[]? modifiers = null)
    {
        var dot = moduleAndAction.IndexOf('.');
        if (dot <= 0 || dot == moduleAndAction.Length - 1)
            throw new System.ArgumentException(
                $"Action identifier must be 'module.action', got '{moduleAndAction}'",
                nameof(moduleAndAction));

        return new ActionSpec(
            moduleAndAction[..dot],
            moduleAndAction[(dot + 1)..],
            @params ?? new System.Collections.Generic.Dictionary<string, object?>(),
            modifiers);
    }
}
