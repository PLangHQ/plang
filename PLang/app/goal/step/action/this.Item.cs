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
[global::app.Attributes.PlangType("action")]
public partial class @this : global::app.type.item.@this, global::app.type.item.ICreate<@this>
{
    /// <summary>An action passes through; anything else is declined. The one way in is its reader
    /// (<c>serializer/Reader.cs</c>) — the .pr wire, a held callback and a nested modifier chain all
    /// read there — so an action is built by its step, never converted from a value.</summary>
    public static @this? Create(object? raw, global::app.type.@this? type, global::app.data.@this data)
    {
        if (raw is @this a) return a;
        data.Fail(new global::app.error.@this(
            $"%{data.Name}% holds a {(raw as global::app.type.item.@this)?.Type.Name ?? raw?.GetType().Name ?? "null"} — " +
            "an action is built by its step, never converted from a value.", "CreateItemDeclined", 400));
        return null;
    }

    /// <summary>A structure, never a single-token leaf — drives the serializer's structure branch.</summary>
    public override bool IsLeaf => false;

    /// <summary>This action's settings — <c>%!llm.decider.setting%</c>, <c>%!llm.query.setting%</c>: its own class when
    /// it has one, else its options as the catalog has them. None once its module was taken out of the app
    /// (module.Remove empties it): no catalog holds it then.</summary>
    protected override async System.Threading.Tasks.ValueTask<global::app.data.@this?> Setting(global::app.data.@this parent)
    {
        if (Module[Name] is not { } catalog) return null;
        // its own class, as the asker's settings build it; else its options, read and written through it
        var own = await parent.Context.Setting.Get(catalog.Address);
        return own.IsInitialized || !own.Success ? own
            : new global::app.data.@this(catalog.Address, new global::app.type.item.setting.action.@this(catalog), context: parent.Context);
    }

    /// <summary>Where this action's settings are kept — its module's name, its own, then <c>.setting</c>
    /// (<c>llm.query.setting</c>).</summary>
    internal string Address => $"{Module.Name}.{Name}.setting";

    /// <summary>The action writes ITSELF — the bare shape it owns, in every view:
    /// <c>{module, name, property, default?, child?}</c>. Its module is a reference, written by name,
    /// never walked. Each property writes its own row (<c>{name, type, value, properties?}</c>);
    /// a child step writes itself.</summary>
    protected internal override async System.Threading.Tasks.ValueTask Write(global::app.type.format.IWriter writer)
    {
        writer.BeginObject();
        writer.Name("module"); writer.String(Module.Name);
        writer.Name("name"); writer.String(Name);
        writer.Name("property");
        await Property.Output(writer);
        if (Default.Count > 0)
        {
            writer.Name("default");
            await Default.Output(writer);
        }
        // The branch body of a control-flow action — omitted on ordinary actions (empty Child).
        // Each child step writes itself; the tree serializes recursively.
        if (Child.Count > 0)
        {
            writer.Name("child");
            await Child.Output(writer);   // step.list writes its own bare array
        }
        writer.EndObject();
    }

    /// <summary>An action in formal is its call — <c>module.name(rows)</c>, its properties then its frozen defaults
    /// (<c>?=</c>) — then a condition's body inline after it (<c>{ a; b }</c>). A clause (<c>on.error</c>, …) is its
    /// step's next call, written by the step like any action.</summary>
    protected internal override async System.Threading.Tasks.ValueTask Write(global::app.goal.step.action.formal.Writer writer)
    {
        writer.BeginCall(Module.Name, Name);
        foreach (var p in Property) await p.Row(writer, frozen: false);
        foreach (var p in Default) await p.Row(writer, frozen: true);
        writer.EndCall();
        if (Child.Count > 0)
        {
            writer.BeginBody();
            foreach (var step in Child.Items())
                foreach (var held in step.Code.Items()) await held.Output(writer);
            writer.EndBody();
        }
    }
}
