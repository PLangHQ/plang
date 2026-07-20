namespace app.goal.step.action;

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
        // The branch body of a control-flow action — omitted on ordinary actions (empty Child).
        // Each child step writes itself; the tree serializes recursively.
        if (Child.Count > 0)
        {
            writer.Name("child");
            writer.BeginArray(Child.Count);
            foreach (var s in Child) await s.Output(writer, mode, context);
            writer.EndArray();
        }
        writer.EndObject();
    }
}
