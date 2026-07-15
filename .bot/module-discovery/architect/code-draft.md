# Stage 4 — reviewed code draft (settled with Ingi 2026-07-15)

The code shapes from the architect/Ingi review round, in landing order. Traced against `b3d5dcd9b`.

> **You own this.** Every block is a suggestion reviewed for SHAPE — bodies, naming inside methods, factoring are yours. Where a body says "= today's X, relocated", the incumbent's body is the spec; where something is uncertain the block says verify.

## 4a — the split: the registry IS the collection

`module/this.cs` relocates to `module/list/this.cs` (it already is the collection: `_modules` index, `Discover`, `Register`/`RegisterType`, `Remove`/`Clear`, `DisposeAsync`, `GetCodeGenerated`) and SHEDS: teaching (`Describe`, `DescribeReturnType/Name`, `GetAllTypesInNamespace`, `FormatDefault`, `IsVariableNameSlot`, `CapabilityInterfaces`, `MarkdownTeachingRoot`/`ResolveMarkdownTeachingRoot`/`WarnOrphansAsync`) and per-action queries (`IsCacheable`, `IsModifier`, `GetModifierOrder`, `GetDefaults`, `GetActionType` — behavior onto the action element). `GetChannelInventory` dies outright — a middleman over `actor.Channel.ChannelNames`; callers go to the actor. `Schema` (the type view) stays on the collection, untouched. App property: lowercase `app.module`.

```csharp
namespace app.module.list;

public sealed class @this : IAsyncDisposable
{
    // KEPT with today's bodies: _modules, Discover, RegisterType, Register, GetCodeGenerated,
    // Contains ×2, Names, Remove, Clear, Count, All, DisposeAsync, Schema.

    // Elements cached — invalidated by the registry's own mutations (RegisterType/Register/
    // Remove/Clear each do _elements.TryRemove(module, out _)). Reflection rows + prose live
    // as long as the element: once per registry change, not per ask. (Today's Describe()
    // rebuilds the WHOLE catalog per call — build/code/Default.cs:24 pays it per build call.)
    private readonly ConcurrentDictionary<string, global::app.module.@this> _elements = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Select a module element. Throws on miss (authored names).</summary>
    public global::app.module.@this this[string name]
        => _modules.ContainsKey(name)
            ? _elements.GetOrAdd(name, n => new global::app.module.@this(n, this))
            : throw new KeyNotFoundException($"No module named '{name}'.");

    /// <summary>Enumerate as the NATIVE list — %modules% in builder goals; the list module
    /// filters it directly. Fresh cheap wrapper per ask over the SAME cached elements.</summary>
    public global::app.type.item.list.@this list
        => new(Names.Select(n => (object?)this[n]).ToList(), App.System.Context);

    // The one handler walk, part 2 — choice registration. Fires (a) once when App attaches
    // (sweep existing entries) and (b) inline on RegisterType/Register when App != null.
    // Leg (b) FIXES A LATENT BUG: today RegisterModuleChoiceTypes runs once at boot, so a
    // code.load module with a choice param never registers its closed set or its Reader<T>.
    // Body = today's RegisterModuleChoiceTypes inner loop + the closed Reader<T> registration
    // (choice-reader-no-reflection-answer.md on the parent branch).
    private global::app.@this _app = null!;
    public global::app.@this App
    {
        get => _app;
        internal set { _app = value; foreach (var m in Names) RegisterChoices(m); }
    }
    private void RegisterChoices(string module) { /* per-prop choice<T> → App.Type.Register + Reader.Register(new Reader<T>(kind)) */ }
}
```

Delete in the same commit: `RegisterModuleChoiceTypes` (`type/list/this.cs:483`) + its call (`app/this.cs:307`).

## 4a/4b — the module element at the freed slot

```csharp
namespace app.module;

/// <summary>One module — a HOST (never authored, never created from values; item⟺ICreate).</summary>
public sealed class @this
{
    private readonly list.@this _list;
    public string Name { get; }
    internal @this(string name, list.@this list) { Name = name; _list = list; }

    // Cached action elements (same invalidation story: the element dies with registry mutation).
    private global::System.Collections.Generic.List<global::app.goal.steps.step.actions.action.@this>? _actions;

    /// <summary>The module's actions as the NATIVE list — filterable, renderable.</summary>
    public global::app.type.item.list.@this Actions
        => new((_actions ??= Mint()).Select(a => (object?)a).ToList(), _list.App.System.Context);

    private List<global::app.goal.steps.step.actions.action.@this> Mint()
        => _list.ActionTypes(Name)   // NEW small internal on list: (name, Type) pairs off the index
            .Select(a => new global::app.goal.steps.step.actions.action.@this
                { Module = Name, ActionName = a.Name, ParameterSchema = a.Type, Context = _list.App.System.Context })
            .ToList();

    /// <summary>Module prose — its OWN markdown (os/system/modules/{Name}/module.{facet}.md),
    /// read through path verbs (AuthGate), cached on the element, null when absent.
    /// Body = MarkdownTeaching.Load's module half, relocated.</summary>
    public async Task<string?> Description() => await Prose("description");
    public async Task<string?> Notes() => await Prose("notes");
    public async Task<string?> Examples() => await Prose("examples");
    private async Task<string?> Prose(string facet) { /* relocated */ }
}
```

## 4b/4c — the action class-zoom partial + the property row host

The `[JsonIgnore]` stuffed teaching fields on `action/this.cs` (`Description`, `ModuleDescription`, `Notes`, `ModuleNotes`, `ExamplesMd`, `ModuleExamplesMd`, `Examples`, `ReturnType`, `ReturnTypeName`, `IsModifier` — verified transient, never in the `.pr`) DIE from the class; the partial computes them off `ParameterSchema`, lazily, cached on the instance.

```csharp
// app/goal/steps/step/actions/action/property/this.cs — NEW
namespace app.goal.steps.step.actions.action.property;

/// <summary>One declared parameter slot — the class-zoom row. (A row is more than a type:
/// the type ENTITY + nullability + default + the %var% marker.)</summary>
public sealed class @this
{
    public required string Name { get; init; }
    public required global::app.type.@this Type { get; init; }
    public bool Nullable { get; init; }
    public object? Default { get; init; }
    public bool IsVariable { get; init; }
}
```

```csharp
// action/this.Schema.cs — NEW partial
public sealed partial class @this
{
    /// <summary>Stamped at catalog mint; null on .pr-zoom instances.</summary>
    [JsonIgnore] internal global::app.actor.context.@this? Context { get; init; }

    private IReadOnlyList<property.@this>? _rows;

    /// <summary>Declared parameter slots — THE reflection leaf, once per element. Filters
    /// exactly as the old Describe (capability interfaces IContext/IStep/IChannel/IEvent/
    /// IStatic, [Code], EqualityContract); Data<T>/[Code]T/Nullable<T> unwrap to the type
    /// entity; Data<variable> → IsVariable; [Default] → Default; the IChannel synthetic
    /// "channel" row rides along (module/this.cs:360-361 today).</summary>
    public global::app.type.item.list.@this Properties
        => new((_rows ??= Reflect()).Select(r => (object?)r).ToList(),
               Context ?? throw new System.InvalidOperationException(
                   "Properties as a plang list needs the catalog context — a .pr-zoom action navigates its rows via the clr carrier."));

    public string? ReturnTypeName => …;                    // body = DescribeReturnTypeName, relocated
    public bool IsModifier => …;                           // [Modifier] read
    public int ModifierOrder => …;                         // = GetModifierOrder, relocated
    public bool Cacheable…                                 // verify: the .pr-stored Cacheable vs attribute read — one owner
    public List<data.@this>? Defaults(HashSet<string> exclude) => …;   // = GetDefaults, relocated

    /// <summary>Action prose — os/system/modules/{Module}/{ActionName}.{facet}.md; attribute
    /// fallback ([Description]) preserved as today (module/this.cs:425). Cached.</summary>
    public async Task<string?> Description() => …;
    public async Task<string?> Notes() => …;
    public async Task<string?> Examples() => …;            // merges ExamplesForLlm()/spec render + markdown, = :374-393 relocated
}
```

`MarkdownTeaching`'s parsing guts split between the two prose homes; `ScanOrphans` relocates to the collection (`WarnOrphansAsync` body, same channel write); the static class + name die.

## 4d — templates + builder goals (parity gate before 4e)

`os/system/builder/templates/modules.md` (illustrative — the PARITY GATE against today's `Describe()`-rendered prompt owns exactness; golden covers: module with prose, module without, a `[Code]`-bearing action, a choice param):

```markdown
{%- for module in modules %}
## {{ module.Name }}{% if module.Description %} — {{ module.Description }}{% endif %}
{%- for action in module.Actions %}
### {{ module.Name }}.{{ action.ActionName }}{% if action.Description %} — {{ action.Description }}{% endif %}
{%- for p in action.Properties %}
- {{ p.Name }}: {% if p.IsVariable %}%var%{% else %}{{ p.Type.Name }}{% if p.Nullable %}?{% endif %}{% endif %}{% if p.Default %} = {{ p.Default }}{% endif %}
{%- endfor %}
{%- if action.ReturnTypeName %}→ returns {{ action.ReturnTypeName }}{% endif %}
{%- endfor %}
{%- endfor %}
```

Builder goals: `- get all modules, write to %modules%` / `- render 'templates/modules.md' with %modules%, write to %catalog%`. Fluid's async member accessors carry the prose doors. Format = extension (one stem, `.md`/`.json`/…); a `.json` template embeds values via the Fluid `json` filter, which MUST route through our json writer (verify the stock filter; rewire if it serializes on its own).

## 4e — repoint + delete

Per the plan's leaf-trace table. Adds from this round: `GetChannelInventory` (middleman) and the `ExamplesForLlm` reflection block (`module/this.cs:374-393`, relocated into the action's examples door).

## 4f — the test report

`os/system/tests/report.txt` (junit DELETED, nothing replaces it until something consumes it; a second format only when a consumer exists):

```markdown
{%- for r in results %}
{{ r.Status }}  {{ r.Goal.Path }}  ({{ r.Duration }}){% if r.Error %}
    {{ r.Error }}{% endif %}
{%- endfor %}
{{ results | size }} tests
```

Dies: `module/test/report.cs`, `app/test/junit/this.cs`. The report action becomes a render call. Durations/counts ride as plang types in the model, not preformatted strings.

## 4g — error presentation via templates (Ingi, this round; per-CODE overrides)

`Error.Format()` + `FormatError` + `FormatVerboseValue` (`error/Error.cs:233+`, ~130 lines of StringBuilder presentation, and Error.cs's PLNG004 hit) DIE; the layout becomes:

```
os/system/error/default.txt      ← the Format() layout as a template
os/system/error/default.json     ← same stem, json face (values via the json filter)
os/system/error/404.txt          ← per-code override wins over default (the existing convention)
```

```markdown
{{ error.Category }} [{{ error.Key }}] {{ error.Message }}
{%- if error.Goal %}  at {{ error.Goal.Path }}{% if error.Step %}:{{ error.Step.Index }}{% endif %}{% endif %}
{%- if error.FixSuggestion %}  fix: {{ error.FixSuggestion }}{% endif %}
{%- for inner in error.ErrorChain %}
	{{ inner.Key }}: {{ inner.Message }}
{%- endfor %}
```

STAYS: `Error.Write(IWriter)` — the wire face is serialization, not presentation. Verify item: trace `Format()`'s callers (the channel error-output path expected) and repoint each to the render; list any caller that needs the text SYNC (a template render is async — a sync wall here → stop and surface).

## For the comment round — where your knowledge beats my trace

This is read-and-comment, no code yet. The spots I most want your read on:

1. **Hot-path check on the moved queries.** `IsModifier`/`GetModifierOrder`/`IsCacheable`/`GetActionType` move onto the action element — trace who calls them and WHEN. If any sit on the per-step run path (modifier scheduling?), minting/holding elements there changes a hot path; say so and we keep an index-level fast door for those instead of forcing the element hop.
2. **Fluid over hosts — spike it FIRST, not at 4d.** The known precedent is bad: exposing `item.list` publicly broke 3 Modules tests because Fluid's reflecting enumeration choked on an unexpected member type. The templates will enumerate host elements with async prose doors and plang-list properties — one early spike (a module element through `render`) de-risks the whole stage.
3. **The parity gate's capture point.** Where does the golden get captured — the final rendered prompt string that `build/code/Default.cs` assembles from `Describe()`, or the StepActions structure? Name the seam; the gate is only as good as its capture.
4. **`Cacheable`'s two owners** — the `.pr`-stored `Cacheable` on the action vs the attribute read. Inventory who reads which and propose the single owner.
5. **The two deliberate behavior changes** — push back if you see a consumer that breaks: choice registration on App-attach + inline (a `code.load` module NOW gets its closed sets — today it silently doesn't), and catalog cost moving from rebuild-per-`Describe()`-call to cached-elements-invalidated-on-mutation.
6. **Order flexibility**: 4g (error templates) is independent of 4a–4e and can land whenever; 4f needs only the template machinery. If 4a's split turns out bigger than it reads, say so before starting, not after.

## Cost/caching model (settled)

- Running an action never touches the views (dispatch reads the internal index).
- Elements + their reflection rows + prose are CACHED, invalidated by registry mutations (`RegisterType`/`Register`/`Remove`/`Clear`) — once per registry change, not per ask; strictly better than today's rebuild-per-Describe-call.
- The plang `list` wrappers mint fresh per ask (cheap shallow wrap over cached elements) — no invalidation problem, mutation-safe.
- No extra loop vs the IEnumerable shape: the one materialization the lift would do anyway just moved into the owner, and the lazy-iterator-as-`clr` trap is dodged.
