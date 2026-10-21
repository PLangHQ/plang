using app.Attributes;

namespace app.warning;

/// <summary>
/// A build-time diagnostic about a program node — a {Key, Message} pair the builder attaches to a
/// goal/step/action (and, in a later pass, Data results). A "warning" by definition: a build error
/// that aborts never produces a node to hang itself on, and one that doesn't abort IS a warning.
/// Runtime failures are a different thing — they are errors, and they land on the run (Call.Errors,
/// App.Errors, the Data result), never on the graph, because the same node can be mid-flight in many
/// concurrent actors at once. A plang value, written by its tags.
/// </summary>
[PlangType("warning")]
public sealed class @this : global::app.type.item.@this, global::app.type.item.ICreate<@this>
{
    /// <summary>A warning, born holding its type from <paramref name="types"/>, the app's type list (read at birth,
    /// never kept).</summary>
    public @this(global::app.type.list.@this types) : base(types[typeof(@this)]) { }

    /// <summary>The builder's diagnostic; no program names it.</summary>
    public static bool Internal => true;

    [Store, Out, LlmBuilder, global::app.Debug] public string Key { get; init; } = "";
    [Store, Out, LlmBuilder, global::app.Debug] public string Message { get; init; } = "";

    /// <summary>A warning passes through; anything else is declined. A warning is the builder's, never converted from
    /// a value.</summary>
    public static @this? Create(object? raw, global::app.type.@this? type, global::app.data.@this data)
    {
        if (raw is @this warning) return warning;
        data.Fail(new global::app.error.@this(
            $"%{data.Name}% holds a {(raw as global::app.type.item.@this)?.Type.Name ?? raw?.GetType().Name ?? "null"} — " +
            "a warning is the builder's, never converted from a value.", "CreateItemDeclined", 400));
        return null;
    }

    /// <summary>A structure, never a single-token leaf.</summary>
    public override bool IsLeaf => false;
}
