using System.Reflection;
using System.Text.Json.Serialization;

namespace app.goal.step.action;

// The class-zoom face of the action host — the catalog view. A .pr action carries its steps;
// the same host at class zoom answers its declared parameter slots (the reflection leaf) for
// the builder catalog. Reflection happens ONCE here, cached on the element.
public partial class @this
{
    // The catalog faces reach App by NAVIGATION — this action → its module → the module collection
    // → App — live, at ask time. A catalog element owns a MODULE, not a context: it is one entry in
    // a registry, and the App it should answer for is whichever one that registry serves. Holding a
    // context would be holding something it does not own, and would go stale against its module.
    // An action built outside a construction door has no module, and these faces say so.
    [JsonIgnore]
    private global::app.@this? App => _module?.App;

    // The handler CLR type — reached TRANSIENTLY through the module element's own door (the owner),
    // never stored on this action. Reads the backing field: an action built outside a construction
    // door has no module, and that answers "unknown handler" rather than throwing.
    private System.Type? Handler => _module?.Handler(Name);

    /// <summary>What this action reaches outside the process — <c>network</c>, <c>llm</c>. The
    /// handler declares it with <c>[RequiresCapability]</c>; test discovery unions these into a
    /// test's auto-tags so a run can skip them (<c>--test={"exclude":["network"]}</c>). It is a
    /// DECLARATION, not a gate: nothing consults it before Run. Empty when the action declares
    /// nothing, so callers never null-check.</summary>
    [JsonIgnore]
    public IEnumerable<global::app.type.item.text.@this> Requirement
        => Handler?.GetCustomAttribute<global::app.Attributes.RequiresCapabilityAttribute>()
               ?.Capabilities.Select(c => new global::app.type.item.text.@this(c))
           ?? Enumerable.Empty<global::app.type.item.text.@this>();

    /// <summary>The handler's own complaint about this action's parameters, or null when it has
    /// none — a handler opts in by implementing <c>IBuildValidatable</c>. Only the handler knows
    /// which parameter COMBINATIONS are legal (the catalog rows describe slots one at a time), so
    /// it is asked rather than re-derived. Read at build time; the builder reacts to the answer.</summary>
    [JsonIgnore]
    public global::app.error.IError? BuildError
    {
        get
        {
            if (Handler is not { } handler
                || !typeof(global::app.module.IBuildValidatable).IsAssignableFrom(handler)) return null;
            var validate = handler.GetMethod("ValidateBuild", BindingFlags.Public | BindingFlags.Static);
            return validate == null ? null : (global::app.error.IError?)validate.Invoke(null, [Parameter.ToList()]);
        }
    }

    private global::app.goal.step.action.property.list.@this? _properties;

    /// <summary>The action's declared parameter slots — its own <c>property.list</c> collection, the
    /// ONE reflection site (the collection owns the reflect + catalog filter). Build validation reads
    /// Nullable / Default / Name off the rows; the catalog templates render each row. Needs the
    /// catalog context (to resolve the handler) — a .pr-zoom action navigates via the clr carrier and
    /// has none. Cached per element.</summary>
    [JsonIgnore]
    public global::app.goal.step.action.property.list.@this Property
    {
        get
        {
            var app = App ?? throw new System.InvalidOperationException(
                "action.Property needs the catalog — this action has no module, so it was built outside a construction door.");
            return _properties ??= new global::app.goal.step.action.property.list.@this(
                Handler, app.Type, app.System.Context);
        }
    }

    private string? _return;
    private bool _returnComputed;

    /// <summary>The PLang type this action returns, read off <c>Run()</c>'s signature. Every Run
    /// returns a Data, so there is always a type: <c>Task&lt;Data&lt;T&gt;&gt;</c> names T, and an
    /// undefined T (bare <c>Task&lt;Data&gt;</c> or <c>Data&lt;object&gt;</c>) is <c>item</c> — the
    /// unconstrained plang type, C#'s <c>object</c>. Null only when <c>Run</c> isn't Data-shaped
    /// at all, which no real action is.</summary>
    [JsonIgnore]
    public string? Return
    {
        get
        {
            if (_returnComputed) return _return;
            _returnComputed = true;

            var handler = Handler;
            if (handler == null || App == null) return null;
            var run = handler.GetMethod("Run", BindingFlags.Public | BindingFlags.Instance, System.Type.EmptyTypes);
            if (run == null) return null;

            var ret = run.ReturnType;
            if (ret.IsGenericType && ret.GetGenericTypeDefinition() == typeof(System.Threading.Tasks.Task<>))
                ret = ret.GetGenericArguments()[0];

            if (ret == typeof(global::app.data.@this)) return _return = "item";
            if (!ret.IsGenericType || ret.GetGenericTypeDefinition() != typeof(global::app.data.@this<>))
                return null;
            var t = ret.GetGenericArguments()[0];
            return _return = t == typeof(object) ? "item" : App!.Type.GetTypeName(t);
        }
    }

    /// <summary>Judges this action and reports what is wrong with it — the build-time verdicts that
    /// are the ACTION's to give: it exists in its module, it emitted every required parameter, and
    /// its own handler accepts the combination. Pure judgement: it changes nothing, so it runs after
    /// construction and never re-flags a default the builder filled or a name it repaired. The
    /// builder reacts to what comes back (re-prompt, abort); deciding is not its job.</summary>
    public IEnumerable<global::app.error.IError> Validate()
    {
        var element = _module?[Name];
        if (element == null)
        {
            yield return new global::app.error.ActionError(
                $"{Module}.{Name}: action not found in module '{Module}'.", "ActionNotFound", 400);
            yield break;
        }

        // A required slot is a declared row that is non-nullable with no [Default] — read off the
        // catalog element's rows, the one reflection site, never a fresh reflect here.
        var emitted = new HashSet<string>(Parameter.Select(p => p.Name), StringComparer.OrdinalIgnoreCase);
        foreach (var row in element.Property.Rows)
        {
            if (row.Nullable || row.Default != null || emitted.Contains(row.Name)) continue;
            yield return new global::app.error.ActionError(
                $"{Module}.{Name}: required parameter '{row.Name}' is missing. " +
                "Every action must emit all non-nullable, non-default parameters.",
                "MissingRequiredParameter", 400);
        }

        if (BuildError is { } complaint) yield return complaint;
    }

    // Action-level teaching prose — file handles over os/system/modules/{Module}/{Name}.{facet}.md,
    // the twins of the module element's module.{facet}.md doors. Lazy references: born unread, content
    // materializes at the Value door, an absent file is falsy (existence truthiness) so
    // `{% if action.Notes %}` guards presence without reading. The template concats module-first + action.
    private global::app.type.item.file.@this? _description;
    private global::app.type.item.file.@this? _notes;
    private global::app.type.item.file.@this? _examples;

    /// <summary>The action's description prose — {Name}.description.md as a lazy file handle.</summary>
    [JsonIgnore]
    public global::app.type.item.file.@this Description => _description ??= Prose("description");

    /// <summary>The action's notes prose — {Name}.notes.md as a lazy file handle.</summary>
    [JsonIgnore]
    public global::app.type.item.file.@this Notes => _notes ??= Prose("notes");

    /// <summary>The action's examples prose — {Name}.examples.md as a lazy file handle.</summary>
    [JsonIgnore]
    public global::app.type.item.file.@this Examples => _examples ??= Prose("examples");

    // The module's teaching prose, reached THROUGH the module element the action already holds
    // (module.{facet}.md) — navigation, not copy. The per-action detail template concats
    // module-first + action for a full teaching block.

    /// <summary>The module's description prose (module.description.md), through the module element.</summary>
    [JsonIgnore]
    public global::app.type.item.file.@this ModuleDescription => Module.Description;

    /// <summary>The module's notes prose (module.notes.md), through the module element.</summary>
    [JsonIgnore]
    public global::app.type.item.file.@this ModuleNotes => Module.Notes;

    /// <summary>The module's examples prose (module.examples.md), through the module element.</summary>
    [JsonIgnore]
    public global::app.type.item.file.@this ModuleExamples => Module.Examples;

    private global::app.type.item.file.@this Prose(string facet)
    {
        var app = App ?? throw new System.InvalidOperationException(
            "action prose needs the catalog — this action has no module, so it was built outside a construction door.");
        var root = app.Module.Teaching
            ?? throw new System.InvalidOperationException(
                "action prose needs the teaching root — the module collection resolves it from App.OsDirectory.");
        var path = root.Combine(Module.Name).Combine($"{Name}.{facet}.md");
        return new global::app.type.item.file.@this(path);
    }
}
