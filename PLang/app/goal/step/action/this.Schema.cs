using System.Reflection;
using System.Text.Json.Serialization;

namespace app.goal.step.action;

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

    private global::app.goal.step.action.property.list.@this? _properties;

    /// <summary>The action's declared parameter slots — its own <c>property.list</c> collection, the
    /// ONE reflection site (the collection owns the reflect + catalog filter). Build validation reads
    /// Nullable / Default / Name off the rows; the catalog templates render each row. Needs the
    /// catalog context (to resolve the handler) — a .pr-zoom action navigates via the clr carrier and
    /// has none. Cached per element.</summary>
    [JsonIgnore]
    public global::app.goal.step.action.property.list.@this Properties
    {
        get
        {
            if (Context == null)
                throw new System.InvalidOperationException(
                    "action.Properties needs the catalog context — stamp it at mint; a .pr-zoom action navigates via the clr carrier.");
            return _properties ??= new global::app.goal.step.action.property.list.@this(Handler, Context.App.Type, Context);
        }
    }

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

            var handler = Handler;
            if (handler == null || Context == null) return _return = null;
            var run = handler.GetMethod("Run", BindingFlags.Public | BindingFlags.Instance, System.Type.EmptyTypes);
            if (run == null) return _return = null;

            var ret = run.ReturnType;
            if (ret.IsGenericType && ret.GetGenericTypeDefinition() == typeof(System.Threading.Tasks.Task<>))
                ret = ret.GetGenericArguments()[0];

            // Only Data<T> declares a concrete return; bare Data (or Data<object>) is polymorphic → null.
            if (!ret.IsGenericType || ret.GetGenericTypeDefinition() != typeof(global::app.data.@this<>))
                return _return = null;
            var t = ret.GetGenericArguments()[0];
            return _return = t == typeof(object) ? null : Context.App.Type[t];   // entity (compounds ride kind)
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
}
