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

    private global::app.goal.step.action.property.list.@this? _properties;

    /// <summary>The action's declared parameter slots — its own <c>property.list</c> collection, the
    /// ONE reflection site (the collection owns the reflect + catalog filter). Build validation reads
    /// Nullable / Default / Name off the rows; the catalog templates render each row. Reached through
    /// the module the action was born with (its handler and the type registry are the module's to
    /// know). Cached per element.</summary>
    [JsonIgnore]
    public global::app.goal.step.action.property.list.@this Property
        => _properties ??= new(Handler, Module.App.Type);

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
            return _return = t == typeof(object) ? "item" : App!.Type[t].ToString();
        }
    }

    // The action's docs — lazy file handles in its module's folder: born unread, content materializes
    // at the Value door, and an absent file is falsy (existence truthiness), so `{% if action.Notes %}`
    // guards presence without reading.
    private global::app.type.item.file.@this? _description;
    private global::app.type.item.file.@this? _notes;
    private global::app.type.item.file.@this? _examples;

    /// <summary>The action's description — {Name}.description.md.</summary>
    [JsonIgnore]
    public global::app.type.item.file.@this Description => _description ??= new(Module.Folder.Combine($"{Name}.description.md"));

    /// <summary>The action's notes — {Name}.notes.md.</summary>
    [JsonIgnore]
    public global::app.type.item.file.@this Notes => _notes ??= new(Module.Folder.Combine($"{Name}.notes.md"));

    /// <summary>The action's examples — {Name}.examples.md.</summary>
    [JsonIgnore]
    public global::app.type.item.file.@this Examples => _examples ??= new(Module.Folder.Combine($"{Name}.examples.md"));
}
