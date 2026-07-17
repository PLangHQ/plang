namespace app.goal.steps.step.actions.action;

// The action IS a plang value (item) — Ingi's 2026-07-17 ruling reversed the hosts-stay-hosts
// model: goal/step/action/modifier are items, holding their C# internals behind faces. This is
// what lets an action value enter the apex at rung 1 (`is item`) instead of bouncing
// item.Create ⇄ type.Create through a synthetic ("list", element) entity (the layer-4 stack
// overflow). The engine still reads the typed internals directly (Module, Parameters, …) — the
// item faces are the boundary layer only.
//
// TRANSITION: Output delegates to the reflection (*) kind — the SAME code that wrote the action
// as a clr-host today, so the .pr wire is byte-identical while the graph flips to items. The
// explicit item Write + serializer/Reader.cs (the recipe, defining-plang-types.md) replace this
// delegation in the follow-up; until then the reflection read path still constructs the action
// from its [Store] props.
public partial class @this : global::app.type.item.@this, global::app.type.item.ICreate<@this>
{
    /// <summary>The action's own type entity — an item names its own type (no namespace reflection).</summary>
    protected internal override global::app.type.@this Type => new("action", typeof(@this));

    /// <summary>A structure, never a single-token leaf — drives the serializer's structure branch.</summary>
    public override bool IsLeaf => false;

    /// <summary>The action writes ITSELF — the bare [Store] shape it owns:
    /// <c>{module, action, parameters, defaults?, modifiers}</c> (nulls omitted, matching the reflected
    /// write). Parameters/Defaults are Data rows (their own self-describing envelope); modifiers are
    /// action-shaped items (each writes itself). The DEBUG view (the live --debug channel, never the
    /// persisted wire) still routes through the reflection (*) kind so diagnostic props ride.</summary>
    public override async System.Threading.Tasks.ValueTask Output(
        global::app.channel.serializer.IWriter writer, global::app.View mode,
        global::app.actor.context.@this? context)
    {
        if (mode == global::app.View.Debug)
        {
            await new global::app.type.item.kind.reflection.@this(context).Output(this, writer, mode, context);
            return;
        }
        writer.BeginObject();
        writer.Name("module"); writer.String(Module);
        writer.Name("action"); writer.String(ActionName);
        writer.Name("parameters");
        writer.BeginArray(Parameters.Count);
        foreach (var p in Parameters) await p.Output(writer, mode, context);
        writer.EndArray();
        if (Defaults != null)
        {
            writer.Name("defaults");
            writer.BeginArray(Defaults.Count);
            foreach (var d in Defaults) await d.Output(writer, mode, context);
            writer.EndArray();
        }
        writer.Name("modifiers");
        writer.BeginArray(Modifiers.Count);
        foreach (var m in Modifiers) await m.Output(writer, mode, context);
        writer.EndArray();
        writer.EndObject();
    }

    /// <summary>The child-WRITE door — reflects onto a public property of this item, the mirror of the
    /// base Get (which already reflects via the clr carrier). The member value lowers ITSELF to the
    /// property type (<c>value.Clr(propType)</c> — a clr(json) actions array builds the action hosts),
    /// then rides into the slot. Returns THIS: an item's child-write mutates in place (no clr wrapper).
    /// TRANSITIONAL alongside the delegating Output, until the graph items are born-with-context and
    /// delegate to the reflection (*) kind like the base Get does.</summary>
    public override async System.Threading.Tasks.ValueTask<global::app.type.item.@this> Set(string key, bool isIndex, object? value)
    {
        // A Data opens its door to the concrete value first (mirror of clr.Set — a host takes a
        // typed child, never a lazy Data).
        if (value is global::app.data.@this dv) value = await dv.Value();
        var prop = GetType().GetProperty(key, System.Reflection.BindingFlags.Public
            | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.IgnoreCase);
        if (prop == null || !prop.CanWrite)
            throw new System.NotSupportedException($"'{Type.Name}' has no writable property '{key}'");
        if (value is global::app.type.item.@this iv && !prop.PropertyType.IsInstanceOfType(value))
            value = iv.Clr(prop.PropertyType);
        prop.SetValue(this, value);
        return this;
    }
}
