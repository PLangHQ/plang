using System.Reflection;
using System.Text.Json.Serialization;

namespace app.goal.step.action;

// The class-zoom face of the action host — the catalog view. A .pr action carries its steps;
// the same host at class zoom answers its declared parameter slots (the reflection leaf) for
// the builder catalog. Reflection happens ONCE here, cached on the element.
public partial class @this : global::app.type.item.note.IAbout
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

    /// <summary>This action is a question — its answer is the verdict an if tests (<c>file.exists</c>,
    /// <c>list.contains</c>); its handler says so with <c>[Question]</c>.</summary>
    [JsonIgnore]
    public bool IsQuestion => Handler?.IsDefined(typeof(global::app.Attributes.QuestionAttribute), inherit: false) == true;

    /// <summary>The variables this action binds when the step names none — each write-target option's default
    /// (foreach's <c>%item%</c>). Choosing one for an option is choosing none, so no option is offered them.</summary>
    [JsonIgnore]
    public IEnumerable<global::app.type.item.variable.@this> Bound
        => Property.Where(p => p.Type.IsName && p.Default?.ToString() is { Length: > 0 })
            .Select(p => new global::app.type.item.variable.@this(p.Default!.ToString()!));

    /// <summary>The property this action reads and answers a new value of, changing nothing (<c>list.query</c>'s
    /// <c>List</c>) — where a step with no destination writes the answer; null when the action has none.</summary>
    [JsonIgnore]
    public global::app.type.property.@this? Input => Property.FirstOrDefault(p => p.IsInput);

    /// <summary>The goal this action calls, as written: the value of its property that holds a goal —
    /// typed goal (goal.call's Name), or marked <c>[Goal]</c> (window.call's Name, a page's goal:
    /// text until app-systems says how a goal elsewhere is typed); null for every other action.</summary>
    [JsonIgnore]
    internal string? Goal
        => Handler?.GetProperties().FirstOrDefault(p => p.PropertyType == typeof(global::app.data.@this<global::app.goal.@this>)
                || p.IsDefined(typeof(global::app.Attributes.GoalAttribute))) is { } held
            ? this[held.Name]?.Value?.ToString()?.Trim('"')
            : null;

    private global::app.type.@this? _return;
    private bool _returnComputed;

    /// <summary>The PLang type this action returns, read off <c>Start()</c>'s signature. Every Start
    /// returns a Data, so there is always a type: <c>Task&lt;Data&lt;T&gt;&gt;</c> names T, and an
    /// undefined T (bare <c>Task&lt;Data&gt;</c> or <c>Data&lt;object&gt;</c>) is <c>item</c> — the
    /// unconstrained plang type, C#'s <c>object</c>. Null only when <c>Run</c> isn't Data-shaped
    /// at all, which no real action is.</summary>
    [JsonIgnore]
    public global::app.type.@this? Return
    {
        get
        {
            if (_returnComputed) return _return;
            _returnComputed = true;

            var handler = Handler;
            if (handler == null || App == null) return null;
            // the handler's own Start() — the dispatcher is ICodeGenerated's, implemented explicitly (not public)
            var start = handler.GetMethod("Start", BindingFlags.Public | BindingFlags.Instance, System.Type.EmptyTypes);
            if (start == null) return null;

            var ret = start.ReturnType;
            if (ret.IsGenericType && ret.GetGenericTypeDefinition() == typeof(System.Threading.Tasks.Task<>))
                ret = ret.GetGenericArguments()[0];

            if (ret == typeof(global::app.data.@this)) return _return = App.type.list["item"];
            if (!ret.IsGenericType || ret.GetGenericTypeDefinition() != typeof(global::app.data.@this<>))
                return null;
            var t = ret.GetGenericArguments()[0];
            return _return = t == typeof(object) ? App.type.list["item"] : App.type.list[t];
        }
    }

    // The action's docs, in its module's folder, born unread. The description and examples are lazy file handles:
    // content materializes at the Value door, and an absent file is falsy (existence truthiness), so
    // `{% if action.Examples %}` guards presence without reading. The notes are read line by line (note.@this).
    private global::app.type.item.file.@this? _description;
    private global::app.type.item.note.@this? _note;
    private global::app.type.item.file.@this? _examples;
    private global::app.type.item.file.@this? _guide;

    /// <summary>The action's description — {Name}.description.md.</summary>
    [JsonIgnore]
    public global::app.type.item.file.@this Description => _description ??= new(Module.Folder.Combine($"{Name}.description.md"), App!.actor.list.System.Context!);

    /// <summary>The action's notes, read line by line — {Name}.notes.md; no lines when the action has none.</summary>
    [JsonIgnore]
    public global::app.type.item.note.@this Note => _note ??= new(this, new(Module.Folder.Combine($"{Name}.notes.md"), App!.actor.list.System.Context!));

    // What its notes are about: the action, its properties the parts each line names
    string global::app.type.item.note.IAbout.Noted => $"{Module.Name}.{Name}";
    string global::app.type.item.note.IAbout.Part => "property";
    System.Collections.Generic.IEnumerable<string> global::app.type.item.note.IAbout.Parts => Property.Select(property => property.Name);
    bool global::app.type.item.note.IAbout.Names(string name) => this[name] is not null;

    /// <summary>The action's examples — {Name}.examples.md.</summary>
    [JsonIgnore]
    public global::app.type.item.file.@this Examples => _examples ??= new(Module.Folder.Combine($"{Name}.examples.md"), App!.actor.list.System.Context!);

    /// <summary>The action's guide — {Name}.guide.md: prose for a learner, shown on the module's page after the
    /// action's Returns line and never read by the builder; falsy when the action has none.</summary>
    [JsonIgnore]
    public global::app.type.item.file.@this Guide => _guide ??= new(Module.Folder.Combine($"{Name}.guide.md"), App!.actor.list.System.Context!);
}
