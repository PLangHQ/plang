using System.Reflection;
using System.Text.Json.Serialization;

namespace app.goal.steps.step.actions.action;

// The class-zoom face of the action host — the catalog view. A .pr action carries its steps;
// the same host at class zoom answers its declared parameter slots (the reflection leaf) for
// the builder catalog. Reflection happens ONCE here, cached on the element.
public partial class @this
{
    /// <summary>Catalog-zoom context — stamped when the module element mints this action for the
    /// catalog; null on a .pr-loaded action (which navigates via the clr carrier, not this list).</summary>
    [JsonIgnore]
    internal global::app.actor.context.@this? Context { get; init; }

    // The handler CLR type — reached TRANSIENTLY through the module element's door (the owner),
    // never stored on this action. Null when there's no catalog Context or the identity is unknown.
    private System.Type? Handler
        => Context != null && Context.App.Module.Contains(Module)
            ? Context.App.Module[Module].Handler(ActionName)
            : null;

    private System.Collections.Generic.IReadOnlyList<property.@this>? _rows;

    /// <summary>The declared parameter rows, TYPED — the ONE reflection site's output, cached per
    /// element. Build validation reads Nullable / Default / Name here instead of re-reflecting the
    /// handler (the four scattered NullabilityInfoContext probes collapse to this single crossing).
    /// The plang-facing <see cref="Properties"/> wraps these same rows.</summary>
    internal System.Collections.Generic.IReadOnlyList<property.@this> ParameterRows => _rows ??= Reflect();

    /// <summary>The declared parameter slots as the NATIVE plang list — the plang face of
    /// <see cref="ParameterRows"/>. Filters exactly as the catalog: capability-interface props, [Code],
    /// EqualityContract, host (clr) params are dropped; Data&lt;T&gt;/Nullable&lt;T&gt; unwrap to the
    /// type entity; Data&lt;variable&gt; → IsVariable; [Default] → Default; the IChannel synthetic
    /// "channel" row rides along.</summary>
    [JsonIgnore]
    public global::app.type.item.list.@this Properties
        => new(ParameterRows.Select(r => (object?)r).ToList(),
               Context ?? throw new System.InvalidOperationException(
                   "action.Properties needs the catalog context — stamp it at mint; a .pr-zoom action navigates via the clr carrier."));

    private global::app.type.@this? _return;
    private bool _returnComputed;

    /// <summary>The action's declared return type as an ENTITY — read off <c>Run()</c>'s
    /// <c>Task&lt;Data&lt;T&gt;&gt;</c> signature (compounds ride the kind axis). Null when the
    /// return is polymorphic: a bare <c>Task&lt;Data&gt;</c> or <c>Data&lt;object&gt;</c> declares
    /// no concrete type. Cached; the twin of <see cref="Properties"/>, feeding goal.variables.</summary>
    [JsonIgnore]
    public global::app.type.@this? Return
    {
        get
        {
            if (_returnComputed) return _return;
            _returnComputed = true;
            return _return = ReflectReturn();
        }
    }

    // Action-level teaching prose — file handles over os/system/modules/{Module}/{ActionName}.{facet}.md,
    // the twins of the module element's module.{facet}.md doors. Lazy references: born unread, content
    // materializes at the Value door, an absent file is falsy (existence truthiness) so
    // `{% if action.Notes %}` guards presence without reading. The template concats module-first + action.
    private global::app.type.item.file.@this? _description;
    private global::app.type.item.file.@this? _notes;
    private global::app.type.item.file.@this? _examples;

    /// <summary>The action's description prose — {ActionName}.description.md as a lazy file handle.</summary>
    [JsonIgnore]
    public global::app.type.item.file.@this Description => _description ??= Prose("description");

    /// <summary>The action's notes prose — {ActionName}.notes.md as a lazy file handle.</summary>
    [JsonIgnore]
    public global::app.type.item.file.@this Notes => _notes ??= Prose("notes");

    /// <summary>The action's examples prose — {ActionName}.examples.md as a lazy file handle.</summary>
    [JsonIgnore]
    public global::app.type.item.file.@this Examples => _examples ??= Prose("examples");

    // The module's teaching prose, reached THROUGH the module element (module.{facet}.md) — navigation,
    // not copy. The per-action detail template concats module-first + action for a full teaching block.
    private global::app.module.@this ModuleElement
        => (Context ?? throw new System.InvalidOperationException(
                "action module prose needs the catalog context — a .pr-zoom action navigates via the clr carrier."))
            .App.Module[Module];

    /// <summary>The module's description prose (module.description.md), through the module element.</summary>
    [JsonIgnore]
    public global::app.type.item.file.@this ModuleDescription => ModuleElement.Description;

    /// <summary>The module's notes prose (module.notes.md), through the module element.</summary>
    [JsonIgnore]
    public global::app.type.item.file.@this ModuleNotes => ModuleElement.Notes;

    /// <summary>The module's examples prose (module.examples.md), through the module element.</summary>
    [JsonIgnore]
    public global::app.type.item.file.@this ModuleExamples => ModuleElement.Examples;

    private global::app.type.item.file.@this Prose(string facet)
    {
        var ctx = Context ?? throw new System.InvalidOperationException(
            "action prose needs the catalog context — a .pr-zoom action navigates via the clr carrier, not the prose doors.");
        var root = ctx.App.Module.ResolveMarkdownTeachingRoot()
            ?? throw new System.InvalidOperationException(
                "action prose needs the teaching root — the module collection resolves it from App.OsDirectory.");
        var path = root.Combine(Module).Combine($"{ActionName}.{facet}.md");
        return new global::app.type.item.file.@this(path);
    }

    private global::app.type.@this? ReflectReturn()
    {
        var handler = Handler;
        if (handler == null || Context == null) return null;
        var run = handler.GetMethod("Run", BindingFlags.Public | BindingFlags.Instance, System.Type.EmptyTypes);
        if (run == null) return null;

        var ret = run.ReturnType;
        if (ret.IsGenericType && ret.GetGenericTypeDefinition() == typeof(System.Threading.Tasks.Task<>))
            ret = ret.GetGenericArguments()[0];

        // Only Data<T> declares a concrete return; bare Data (or Data<object>) is polymorphic → null.
        if (!ret.IsGenericType || ret.GetGenericTypeDefinition() != typeof(global::app.data.@this<>))
            return null;
        var t = ret.GetGenericArguments()[0];
        return t == typeof(object) ? null : Context.App.Type[t];   // entity (compounds ride kind)
    }

    // The execution-context slots the source generator wires (not user-supplied params) — filtered
    // out of the catalog so the LLM never emits them.
    private static readonly System.Type[] CapabilityInterfaces =
    {
        typeof(global::app.module.IContext), typeof(global::app.module.IStep),
        typeof(global::app.module.IChannel), typeof(global::app.module.IEvent),
        typeof(global::app.module.IStatic),
    };

    private System.Collections.Generic.List<property.@this> Reflect()
    {
        var rows = new System.Collections.Generic.List<property.@this>();
        var handler = Handler;
        if (handler == null) return rows;
        var type = Context!.App.Type;
        var nCtx = new NullabilityInfoContext();

        var capabilityProps = new System.Collections.Generic.HashSet<string>(
            CapabilityInterfaces.Where(i => i.IsAssignableFrom(handler))
                .SelectMany(i => i.GetProperties().Select(p => p.Name)),
            System.StringComparer.OrdinalIgnoreCase);

        foreach (var prop in handler.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (prop.Name == "EqualityContract") continue;
            if (capabilityProps.Contains(prop.Name)) continue;
            if (prop.GetCustomAttribute<global::app.module.CodeAttribute>() != null) continue;

            bool isNullable = System.Nullable.GetUnderlyingType(prop.PropertyType) != null;
            if (!isNullable && !prop.PropertyType.IsValueType)
                isNullable = nCtx.Create(prop).WriteState == NullabilityState.Nullable;

            var value = UnwrapToValue(prop.PropertyType);
            // bare Data is the polymorphic "object" slot; every other value type resolves its
            // entity through the identity door (compounds ride the kind axis).
            var entity = value == typeof(global::app.data.@this) ? type["object"] : type[value];

            // Host params (Goal, Step, SignOptions, BuildResponse, StepActions, ...) resolve to
            // `clr` — the LLM can never author a host object, and naming one would leak the C# type
            // (the door refuses to). Filter them from the catalog like the capability interfaces so
            // ONLY plang types reach the LLM.
            if (entity.Name == "clr") continue;

            rows.Add(new property.@this
            {
                Name = prop.Name,
                Type = entity,
                Nullable = isNullable,
                IsVariable = IsVariableNameSlot(prop.PropertyType),
                Default = prop.GetCustomAttribute<global::app.module.DefaultAttribute>()?.Value,
            });
        }

        // IChannel actions: source-gen resolves the Channel slot off a "channel" param — surface it
        // so the LLM can emit a name from the actor's inventory.
        if (typeof(global::app.module.IChannel).IsAssignableFrom(handler))
            rows.Add(new property.@this { Name = "channel", Type = type["string"], Nullable = true });

        return rows;
    }

    // Data<T>/Nullable<T> unwrap to the value type; the door owns the rest (list.@this<T>,
    // choice<T>, scalars). Mirrors GetTypeName's Data/Nullable unwrapping.
    private static System.Type UnwrapToValue(System.Type t)
    {
        var n = System.Nullable.GetUnderlyingType(t);
        if (n != null) t = n;
        if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(global::app.data.@this<>))
            t = t.GetGenericArguments()[0];
        return t;
    }

    private static bool IsVariableNameSlot(System.Type propType)
    {
        var underlying = System.Nullable.GetUnderlyingType(propType) ?? propType;
        if (!underlying.IsGenericType) return false;
        if (underlying.GetGenericTypeDefinition() != typeof(global::app.data.@this<>)) return false;
        return underlying.GetGenericArguments()[0] == typeof(global::app.variable.@this);
    }
}
