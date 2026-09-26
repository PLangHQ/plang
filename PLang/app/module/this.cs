using System.Reflection;

namespace app.module;

/// <summary>
/// One module — a HOST (never authored, never created from values; item⟺ICreate rules
/// it out as a plang type). Carried as <c>clr(module)</c>, navigated by reflection, read
/// by templates through its own doors. The element at the concept node <c>app.module</c>;
/// the collection is <c>app.module.list.@this</c>, which owns selection and lifecycle and
/// mints these elements.
/// </summary>
public sealed class @this
{
    private readonly list.@this _list;

    /// <summary>The module name — "file", "variable", "list".</summary>
    [Debug, Out]
    public string Name { get; }

    /// <summary>The module IS its name in text — so a site composing the qualified form
    /// (<c>$"{action.Module}.{action.Name}"</c>, <c>{{ a.Module }}.{{ a.Name }}</c>) reads
    /// naturally without reaching for <c>.Name</c>.</summary>
    public override string ToString() => Name;

    // The module's actions — ITS OWN storage, filled as each one registers. One map, because the
    // ROLE is decided once, here: an action carrying [Modifier] is minted as the modifier subtype
    // at registration, so "the type IS the role" needs no second home and no flag. Action and
    // Modifier are filtered views over this one map.
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, Row> _action
        = new(System.StringComparer.OrdinalIgnoreCase);

    // The lifecycle record (per-call Type vs shared Instance) beside the catalog element it describes.
    private sealed record Row(global::app.module.list.ActionEntry Entry, global::app.goal.step.action.@this Element);

    internal @this(string name, list.@this list)
    {
        Name = name;
        _list = list;
    }

    /// <summary>The App this module belongs to — reached by NAVIGATION (module → its collection →
    /// App), never stamped. Registration runs inside the collection's constructor, before App is
    /// attached, so nothing here can be born holding one; every reader asks later, when it exists.</summary>
    internal global::app.@this App => _list.App;

    /// <summary>Takes ownership of one action: its lifecycle entry AND its catalog element, born
    /// as the subtype its <c>[Modifier]</c> attribute says it is. The module is the only thing that
    /// ever adds to its own contents.</summary>
    internal void Add(string actionName, System.Type? type, IAction? instance)
    {
        var clr = type ?? instance?.GetType();
        var order = clr?.GetCustomAttribute<global::app.module.ModifierAttribute>()?.Order;
        // The catalog element carries the [Action] cache flag so the teaching template can tag
        // [no-cache] — read off the attribute, its single source, not defaulted.
        var cacheable = clr?.GetCustomAttribute<global::app.module.ActionAttribute>()?.Cacheable ?? true;
        // The catalog element is born with its class's properties, reflected on first read.
        global::app.goal.step.action.@this element = order != null
            ? new global::app.goal.step.action.modifier.@this
                { Module = this, Name = actionName, Position = order.Value, Cacheable = cacheable, Property = new(this, actionName) }
            : new global::app.goal.step.action.@this
                { Module = this, Name = actionName, Cacheable = cacheable, Property = new(this, actionName) };
        _action[actionName] = new Row(new global::app.module.list.ActionEntry(type, instance), element);
    }

    /// <summary>The module's standalone actions as the NATIVE plang list — a view over the one map;
    /// the type IS the role. Filterable by the list module, renderable by templates.</summary>
    public global::app.type.item.list.@this Action => View(modifiers: false);

    /// <summary>The module's modifiers as the NATIVE plang list — the catalog's "# Modifiers"
    /// section renders from here.</summary>
    public global::app.type.item.list.@this Modifier => View(modifiers: true);

    private global::app.type.item.list.@this View(bool modifiers)
        => new(_action.Values.Select(r => r.Element)
                 .Where(e => e is global::app.goal.step.action.modifier.@this == modifiers)
                 .Select(e => (object?)e).ToList());

    /// <summary>Select one catalog element by action name — action OR modifier; the type answers
    /// the role. Null when the name isn't in this module.</summary>
    public global::app.goal.step.action.@this? this[string actionName]
        => _action.TryGetValue(actionName, out var row) ? row.Element : null;

    /// <summary>The handler CLR type for one of this module's actions — the owner's answer, read
    /// off its OWN entry and handed TRANSIENTLY to the reflection leaf. It never rides on the action.</summary>
    internal System.Type? Handler(string actionName)
        => _action.TryGetValue(actionName, out var row) ? row.Entry.Type ?? row.Entry.Instance?.GetType() : null;

    /// <summary>The runnable shell for one of this module's actions — the module reads its own entry.</summary>
    internal ICodeGenerated? Create(string actionName, actor.context.@this context)
        => _action.TryGetValue(actionName, out var row) ? row.Entry.Create(context) : null;

    internal bool Contains(string actionName) => _action.ContainsKey(actionName);

    /// <summary>The names this module answers to — its own keys.</summary>
    internal IEnumerable<string> ActionNames => _action.Keys;

    internal int Count => _action.Count;

    /// <summary>The shared instances this module holds (per-call Type registrations have none).</summary>
    internal IEnumerable<IAction> Instances
        => _action.Values.Where(r => r.Entry.Instance != null).Select(r => r.Entry.Instance!);

    /// <summary>Sheds every action this module owns. Unregistering a module must be authoritative
    /// even for code already holding the element (a revoked DLL's actions must stop resolving), and
    /// only the module can empty itself.</summary>
    internal void Clear() => _action.Clear();

    /// <summary>Disposes the shared instances this module owns — lifecycle follows ownership.</summary>
    internal async System.Threading.Tasks.ValueTask DisposeAsync()
    {
        foreach (var row in _action.Values)
        {
            if (row.Entry.Instance is IAsyncDisposable async) await async.DisposeAsync();
            else if (row.Entry.Instance is IDisposable sync) sync.Dispose();
        }
    }

    /// <summary>The module's docs folder — os/system/modules/{Name}. Its actions reach their own doc
    /// files through it.</summary>
    internal global::app.type.item.path.@this Folder
        => (_list.Teaching ?? throw new System.InvalidOperationException(
               "module docs need the teaching root — it exists once the System context does."))
           .Combine(Name);

    // A lazy file handle: born unread, content materializes at the Value door (AuthGate'd path
    // verbs), and an absent file is falsy (existence truthiness), so `{% if module.Description %}`
    // guards presence without reading.
    private global::app.type.item.file.@this? _description;

    /// <summary>The module's description — module.description.md.</summary>
    public global::app.type.item.file.@this Description => _description ??= new(Folder.Combine("module.description.md"), App.System.Context!);

    private global::app.type.item.file.@this? _notes;

    /// <summary>The module's notes — module.notes.md; falsy when the module has none.</summary>
    public global::app.type.item.file.@this Notes => _notes ??= new(Folder.Combine("module.notes.md"), App.System.Context!);
}
