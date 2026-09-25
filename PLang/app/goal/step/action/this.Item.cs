namespace app.goal.step.action;

// The action IS a plang value (item) — Ingi's 2026-07-17 ruling reversed the hosts-stay-hosts
// model: goal/step/action/modifier are items, holding their C# internals behind faces. This is
// what lets an action value enter the apex at rung 1 (`is item`) instead of bouncing
// item.Create ⇄ type.Create through a synthetic ("list", element) entity (the layer-4 stack
// overflow). The engine still reads the typed internals directly (Module, Property, …) — the
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

    /// <summary>An action passes through; anything else is declined. The one way in is its reader
    /// (<c>serializer/Reader.cs</c>) — the .pr wire, a held callback and a nested modifier chain all
    /// read there — so an action is built by its step, never converted from a value.</summary>
    public static @this? Create(object? raw, global::app.data.@this data)
    {
        if (raw is @this a) return a;
        data.Fail(new global::app.error.Error(
            $"%{data.Name}% holds a {(raw as global::app.type.item.@this)?.Type.Name ?? raw?.GetType().Name ?? "null"} — " +
            "an action is built by its step, never converted from a value.", "CreateItemDeclined", 400));
        return null;
    }

    /// <summary>A structure, never a single-token leaf — drives the serializer's structure branch.</summary>
    public override bool IsLeaf => false;

    /// <summary>The action writes ITSELF — the bare [Store] shape it owns:
    /// <c>{module, name, property, default?, modifier}</c>. Each property writes its own row
    /// (<c>{name, type, value, properties?}</c>); modifiers are
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
        if (writer is global::app.channel.serializer.formal.Writer formal)
        {
            await Formal(formal, mode, context);
            return;
        }
        writer.BeginObject();
        writer.Name("module"); writer.String(Module.Name);
        writer.Name("name"); writer.String(Name);
        writer.Name("property");
        await Property.Output(writer, mode, context);
        if (Default.Count > 0)
        {
            writer.Name("default");
            await Default.Output(writer, mode, context);
        }
        // The modifiers wrapping the action — omitted when there are none, like child and recovery.
        if (Modifier.Count > 0)
        {
            writer.Name("modifier");
            writer.BeginArray(Modifier.Count);
            foreach (var m in Modifier) await m.Output(writer, mode, context);
            writer.EndArray();
        }
        // The branch body of a control-flow action — omitted on ordinary actions (empty Child).
        // Each child step writes itself; the tree serializes recursively.
        if (Child.Count > 0)
        {
            writer.Name("child");
            await Child.Output(writer, mode, context);   // step.list writes its own bare array
        }
        // The body of an `on error` clause — omitted on every action but error.handle.
        if (Recovery.Count > 0)
        {
            writer.Name("recovery");
            await Recovery.Output(writer, mode, context);   // action.list writes its own bare array
        }
        writer.EndObject();
    }

    /// <summary>The action in formal: its own call; a condition's body inline after it (<c>{ a; b }</c>);
    /// then its modifiers after it, innermost first (the list is outermost first) — each the next call in
    /// the same sequence: <c>file.read(…); cache.wrap(…); error.handle(…)</c>.</summary>
    private async System.Threading.Tasks.ValueTask Formal(global::app.channel.serializer.formal.Writer writer,
        global::app.View mode, global::app.actor.context.@this? context)
    {
        await Call(writer, mode, context);
        if (Child.Count > 0)
        {
            writer.BeginBody();
            foreach (var step in Child.Items())
                foreach (var action in step.Code.Items()) await action.Output(writer, mode, context);
            writer.EndBody();
        }
        for (var i = Modifier.Count - 1; i >= 0; i--) await Modifier[i].Call(writer, mode, context);
    }

    /// <summary>The action's call alone: <c>module.name(rows)</c> — its properties, its frozen defaults
    /// (<c>?=</c>), and a modifier's Recovery (<c>Recovery: list&lt;action&gt; = [a, b]</c>).</summary>
    private async System.Threading.Tasks.ValueTask Call(global::app.channel.serializer.formal.Writer writer,
        global::app.View mode, global::app.actor.context.@this? context)
    {
        writer.BeginCall(Module.Name, Name);
        foreach (var p in Property) await p.Row(writer, frozen: false, mode, context);
        foreach (var p in Default) await p.Row(writer, frozen: true, mode, context);
        if (Recovery.Count > 0)
        {
            writer.Row("Recovery", "list<action>");
            writer.BeginArray((int)Recovery.Count);
            foreach (var action in Recovery.Items()) await action.Output(writer, mode, context);
            writer.EndArray();
        }
        writer.EndCall();
    }
}
