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

    /// <summary>The action builds ITSELF from a dict — the read-side twin of <see cref="Output"/>,
    /// the same <c>{module, name, parameter, default?, modifier, child?}</c> shape. A nested action
    /// (a modifier's on-error recovery chain, a compiled step's action set) arrives as a materialised
    /// dict, so the action owns that construction instead of falling to the generic reflection lower
    /// door (which can't rebuild the <c>Data</c> parameter rows). Parameter/default rows already rode
    /// in as <c>Data</c> (the json reader's typed-entry path). A non-dict, or a dict without the
    /// canonical <c>module</c>/<c>name</c> keys (an old <c>{action, parameters}</c> .pr), fails LOUD —
    /// wrong shape is a rebuild signal, never silently coerced to an empty action.</summary>
    public static @this? Create(object? raw, global::app.data.@this data)
    {
        if (raw is @this a) return a;
        if (raw is not global::app.type.item.dict.@this d)
        {
            data.Fail(new global::app.error.Error(
                $"cannot build an action from a {(raw as global::app.type.item.@this)?.Type.Name ?? raw?.GetType().Name ?? "null"} — " +
                $"an action reads from a dict of {{module, name, parameter}}.", "ActionShape", 400));
            return null;
        }
        var module = d.Get("module")?.Peek()?.ToString();
        var name = d.Get("name")?.Peek()?.ToString();
        if (string.IsNullOrEmpty(module) || string.IsNullOrEmpty(name))
        {
            data.Fail(new global::app.error.Error(
                $"an action needs 'module' and 'name' — got keys [{string.Join(", ", d.KeyNames)}]. " +
                $"An old {{action, parameters}} .pr must be rebuilt.", "ActionShape", 400));
            return null;
        }
        return Populate(d, new @this { Module = module, ActionName = name }, data);
    }

    // The populate walk — shared by the action itself and its modifiers (a modifier IS an action +
    // Position, so the same fields fill it). Module/name are already set by the caller (it validated
    // them); this fills the rest. Child steps read through their own door (threading the binding so a
    // malformed child surfaces there, not here).
    private static @this Populate(global::app.type.item.dict.@this d, @this act, global::app.data.@this data)
    {
        if (act.Module.Length == 0) act.Module = d.Get("module")?.Peek()?.ToString() ?? "";
        if (act.ActionName.Length == 0) act.ActionName = d.Get("name")?.Peek()?.ToString() ?? "";
        if (d.Get("parameter")?.Peek() is global::app.type.item.list.@this ps)
            foreach (var row in ps.Items) act.Parameter.Add(row);
        if (d.Get("default")?.Peek() is global::app.type.item.list.@this ds)
        {
            act.Default = new();
            foreach (var row in ds.Items) act.Default.Add(row);
        }
        if (d.Get("modifier")?.Peek() is global::app.type.item.list.@this ms)
            foreach (var row in ms.Items)
                if (row.Peek() is global::app.type.item.dict.@this md)
                    act.Modifiers.Add((modifier.@this)Populate(md, new modifier.@this(), data));
        if (d.Get("child")?.Peek() is global::app.type.item.list.@this cs)
        {
            var steps = new System.Collections.Generic.List<global::app.goal.step.@this>();
            foreach (var row in cs.Items)
                if (Made<global::app.goal.step.@this>(row.Peek(), data) is { } s) steps.Add(s);
            act.Child = new(steps);
        }
        return act;
    }

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
        writer.Name("name"); writer.String(ActionName);
        writer.Name("parameter");
        writer.BeginArray(Parameter.Count);
        foreach (var p in Parameter) await p.Output(writer, mode, context);
        writer.EndArray();
        if (Default != null)
        {
            writer.Name("default");
            writer.BeginArray(Default.Count);
            foreach (var d in Default) await d.Output(writer, mode, context);
            writer.EndArray();
        }
        writer.Name("modifier");
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
