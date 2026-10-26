namespace app.goal.step;

// The step IS a plang value (item), as an action is (action/this.Item.cs). The engine reads the
// typed internals (Index, Text, Actions, …) directly; the item faces are the boundary only. The step
// is written by its tags, in declaration order; its serializer/Reader.cs reads itself back.
[global::app.Attributes.PlangType("step")]
public partial class @this : global::app.type.item.@this, global::app.type.item.ICreate<@this>,
    global::app.type.item.property.IOwner
{
    /// <summary>What a program adds to the step — kept as long as the step is loaded.</summary>
    [Store, global::app.LlmBuilder]
    public global::app.type.item.property.list.@this property { get; }

    /// <summary>A step passes through; anything else is declined. A step is built by its goal, never
    /// converted from a value.</summary>
    public static @this? Create(object? raw, global::app.type.@this? type, global::app.data.@this data)
    {
        if (raw is @this s) return s;
        data.Fail(new global::app.error.@this(
            $"%{data.Name}% holds a {(raw as global::app.type.item.@this)?.Type.Name ?? raw?.GetType().Name ?? "null"} — " +
            "a step is built by its goal, never converted from a value.", "CreateItemDeclined", 400));
        return null;
    }

    /// <summary>A structure, never a single-token leaf.</summary>
    public override bool IsLeaf => false;

    /// <summary>A step is part of a program, which is shared, never copied: a copy of it is it.</summary>
    protected internal override global::app.type.item.@this Clone() => this;

    /// <summary>The step takes its own children. A write to <c>code</c> is the step taking the actions it is
    /// handed — actions, each the step's own; any other value is refused with why, the step's code as it was (the
    /// builder writes code in formal, read into actions). Every other key is one of the step's own members.</summary>
    public override async System.Threading.Tasks.ValueTask<global::app.data.@this> Set(
        string key, bool isIndex, object? value, global::app.actor.context.@this context)
    {
        if (isIndex || !string.Equals(key, "code", System.StringComparison.OrdinalIgnoreCase))
            return await base.Set(key, isIndex, value, context);

        var binding = value as global::app.data.@this;
        var incoming = binding != null ? await binding.Value() : value as global::app.type.item.@this;

        var node = new global::app.goal.step.action.list.@this(_code.Type);
        foreach (var row in Rows(incoming, context))
        {
            if (row is not global::app.goal.step.action.@this action)
                return context.Error(new global::app.error.@this(
                    $"a step's code is actions; a {row.Type.Name} is not one — the builder writes code in formal", "NotAnAction", 400));
            node.Add(action);
        }
        _code = node;
        return context.Ok(this);
    }

    /// <summary>The rows of an incoming action write. A several-valued write enumerates ITSELF — a
    /// plang list its elements, a json array its own (each value owns how it is walked); a single
    /// action is one row.</summary>
    private static System.Collections.Generic.IEnumerable<global::app.type.item.@this> Rows(
        global::app.type.item.@this? incoming, global::app.actor.context.@this context)
    {
        if (incoming == null) yield break;
        if (incoming is global::app.goal.step.action.@this) { yield return incoming; yield break; }
        var any = false;
        foreach (var (_, row) in incoming.EnumerateItems(context))
        {
            if (row.Peek() is { } item && !ReferenceEquals(item, incoming)) { any = true; yield return item; }
        }
        if (!any) yield return incoming;
    }
}
