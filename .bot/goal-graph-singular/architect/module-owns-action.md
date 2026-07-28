# module ⇒ action ⇒ parameter — the module owns action construction

**Status:** RELEASED to coder 2026-07-28 (via `validate-trilogy-answer.md` Q1 — implement the core before the Validate trilogy). Where this doc conflicts with later rulings (wiring-snag CONDEMNED bridges, attribute vocabulary, the landed LLM-wire reader work), the LATER ruling wins — reconcile, don't redo. Answers coder's `to-architect-module-owns-action.md`.

## Why

An action holds its module as a naked string (`action.Module = "variable"`), and everything downstream re-resolves that string: dispatch walks the registry dictionary per execution, the Schema partial resolves `Context.App.Module[Module]` in three separate places, `IsCondition` string-compares, error messages concat `$"{Module}.{ActionName}"`. The module is a first-class element (`app.module.@this`) that already owns its catalog actions — but the .pr action references it by name and every consumer pays the re-lookup. Ingi's framing: "should it be a module rather than a string? the module could then load the action — module ⇒ action ⇒ …".

## The framing

Two trees, not one. The catalog tree `module ⇒ action` already exists: `module.@this` builds catalog elements from the registry (routes by `[Modifier]`, stamps Position/Cacheable/Context). The .pr tree is `goal ⇒ step ⇒ action ⇒ parameter` — the action's *parent* is the step. The module is the action's **type**, not its parent. So this change is instance-holds-its-type (`action.Module : module.@this`) plus type-constructs-instances (the module creates actions). Holding the element is safe: it is a thin `(Name, _list)` handle whose registry reads delegate live to the list — nothing staleable rides on it.

## Vocabulary: everything that constructs is `Create`

`Mint`, `Load`, `Populate` all die as words. One word for "build from raw" at every level, matching the ICreate convention and what the registry's own `entry.Create(context)` already says:

| Owner | Signature | Builds |
|---|---|---|
| `action.@this` (static, ICreate) | `Create(object? raw, data)` | shape-check + delegate — zero `d.Get` |
| `app.module.list.@this` | `Create(dict, data)` | reads `"module"` (the one key it owns) → module |
| `app.module.@this` | `Create(dict, data)` | reads `"name"` (the one key it owns) → catalog |
| catalog `action.@this` (instance, virtual) | `Create()` | blank execution instance of ITS OWN kind — Module/Name/Cacheable stamped; `modifier.@this` overrides (covariant) adding Position |
| catalog `action.@this` (instance) | `Create(dict, data)` | `Create()` + fill (parameter/default/modifier/child) |
| `app.module.@this` | `Create(name, context)` | the runnable shell — old `GetCodeGenerated` body, i.e. `entry.Create(context)` |
| `app.module.@this` (private) | `Create()` | the catalog element lists (old `Mint()` body) |

Principle that fell out: **each owner reads exactly the one dict key it owns.** The action's static door contains no `d.Get` at all; the collection reads `module`, the module reads `name`, the catalog fills the rest. Nested modifier rows re-enter the static door (full chain per row) and are role-checked at load — a "modifier" the catalog says is a plain action fails loud.

## The action — `PLang/app/goal/step/action/this.cs`

```csharp
private global::app.module.@this? _module;

/// <summary>The module this action belongs to — the element. Every construction door sets it;
/// reading it unset is a bug and throws. (Events.Stamp's legacy placeholder never reads it.)</summary>
[Debug]
public global::app.module.@this Module
{
    get => _module ?? throw new System.InvalidOperationException($"action '{Name}' has no module — constructed outside a door");
    set => _module = value;
}

[Store, LlmBuilder, Debug, Default]
public string Name { get; set; } = "";                    // was ActionName; wire key stays "name"

public override string ToString()
    => _module is null ? Name : $"{_module.Name}.{Name}"; // "file.read" — never throws
```

- **No `[JsonIgnore]` anywhere** — the plang attribute set (`[Store]`/`[Debug]`/`[Out]`/`[Default]`/`[LlmBuilder]`) is the only serialization contract; absence means it doesn't ride. `Module` carries `[Debug]` only (wire is the explicit `Output`).
- `module.@this.Name` gets `[Debug, Out]` so an element crossing a debug/wire view rides as `{name: "file"}` (property-bag rule); `module.@this.ToString() => Name` so `{{ a.Module }}` in templates keeps rendering `file` unchanged.
- **`ActionName` → `Name`** (was verb-free but compound; obpv). The qualified face `"file.read"` stops being a property — it is `ToString()`. All `$"{a.Module}.{a.ActionName}"` sites become `$"{a}"`.
- **`Module` is never null and never nullable** — the throwing getter is the enforcement. `Events.Stamp`'s placeholder (`Events.cs:35`, legacy, on todo) never reads it. Only `ToString` and `Snapshot.Capture` read the backing field defensively.
- Dispatch (`DispatchAsync`, and `modifier.Wrap`): `var (handler, error) = Module.Create(Name, context);` — the string re-lookup of the module row dies. `app.module.list.GetCodeGenerated(action, ctx)` (public) dies; its body becomes internal `Handler(module, name, ctx)` on the list that the element's `Create(name, ctx)` delegates to (`ActionEntry` stays private).

## No `Handler` property — consumers' questions become action members

The forwarding member `internal Type? Handler => Module.Handler(Name)` was considered and rejected: a member whose whole body is another member's call is a middleman. Deeper: consumers reach for the raw CLR type to answer questions that are the action's own:

- `getTypes.cs:194-206` `DetermineReturnType` re-implements the existing `Return` property (`this.Schema.cs:50-72`) — same `Run()`/`Task<Data<T>>` reflection walk. It dies; getTypes asks `action.Return`. With the module element held, `Return`/`Reflect()` reach App through `Module` (`_list.App`, the reach old Mint used), so they answer on .pr-zoom actions, not just catalog elements — the Context gate softens.
- `discover.cs:257-260` reflects `RequiresCapabilityAttribute` by hand → NEW action member (e.g. `action.Capabilities`) owning that reflection.
- `Default.cs:648-654` reflects `IBuildValidatable` + invokes `ValidateBuild` by hand → action member owning its build validation.

The CLR type never leaves the module/action boundary. The one survivor is `module.Handler(string) → System.Type?` (`module/this.cs:28`, internal) — the module's own door to `_list`'s private index, called inline inside action members that genuinely reflect. Scope flag: the discover/Default member extraction can ride along or queue as its own worklist item — Ingi's call, lean ride-along (same disease).

## The pr reader — `action/serializer/Reader.cs`

The reader stops pre-creating the instance (`new action.@this()` dies) and stops deciding role by array position. It reads the two leading identity keys off the stream, asks the catalog for the blank (`modules[m][n].Create()`), then pull-fills the rest (its mirror of `Output` — the serializer leaf is the one place that reads both keys itself, deliberately).

- **Wire key order becomes a contract**: `module`,`name` lead the object (`Output` already writes them first). Violation / unknown module / unknown action = throw with a rebuild message — `LoadFromFileAsync`'s catch turns it into a typed Error. Order-tolerance only ever protected hand-edited .pr files (a rebuild case).
- The `"modifier"` array case recurses into `Read` and asserts `is modifier.@this` — same load-time role check as the dict door.
- Free fix: wire-read modifiers get `Position` back (not on the wire; today silently 0) — stamped from the registry, its true source.
- **Fail-at-load semantics** (Ingi to confirm, implicitly accepted): a .pr naming a missing module/action fails at read, not mid-execution. Safe by construction: the registry populates in the `module.list` constructor, before any goal can load.

## Site sweep

| Site | Becomes |
|---|---|
| `step/this.cs:76-77` Nest | `a.Module[a.Name] is action.modifier.@this catalog` — `modules` param + `Contains` guard die |
| `step/this.cs:88-89` Nest | shape unchanged — `a.Module` is already the element |
| `goal/this.cs:292` goalEntryAction | `{ Module = context.App.Module["goal"], Name = "enter" }` |
| `this.Schema.cs:18-21` private `Handler` | dies — `Module.Handler(Name)` inline in `Reflect()`/`Return` |
| `this.Schema.cs:96-99` `ModuleElement` | dies — `ModuleDescription => Module.Description` etc.; prose path `root.Combine(Module.Name)` |
| `actor/context/this.cs:492,494` LifecycleFor | `module: action.Module.Name, actionName: action.Name` |
| `call/this.Snapshot.cs:34` | `s.Write("actionModule", _module-safe Name)`; wire keys unchanged, write-only |
| `error/CallChainRenderer.cs:42,51` | `.Module.Name` compares (not element reference identity) |
| `build/code/Default.cs:640,656` | `$"{a}"` |
| `build/code/Default.cs:647,694,973` | build-validate member / `a.Module.Create(a.Name, ctx)` |
| `getTypes.cs:198-206` | dies — `action.Return` |
| `discover.cs:257,263` | `action.Capabilities` / `action.Module.Name == "goal"` |
| `type/spec/render/this.cs`, `type/list/this.cs:639`, list internals | untouched (`ActionSpec`/namespace strings, not `action.@this`) |

## Demolition list

Dies: `action.Module: string` (+ its `[Store, LlmBuilder]` ride); `action.ActionName` (renamed `Name`); computed `Name => $"{Module}.{ActionName}"` property (→ `ToString()`); `Populate` (static, `this.Item.cs`); old static `Create` body's `d.Get("module"/"name")` reads; `module.Mint()` (renamed private `Create()`); `list.GetCodeGenerated(action, ctx)` public door; Schema partial's private `Handler` property and `ModuleElement` property; `Reader`'s `new action.@this()` / `new modifier.@this()` pre-creation; Nest's `modules.Contains` guard + `modules` parameter (if otherwise unused); `getTypes.DetermineReturnType`; discover's inline attribute reflection; Default.cs's inline `IBuildValidatable` reflection.

Stays: registry string index (`_modules` dict, `ActionEntry` private); `module.Handler(string) → Type` internal; `Contains(string)` (collection door's guard); step/goal readers; `GetActionType` for non-action-@this callers (`type/list:639`, list internals); Events code untouched (legacy, on todo).

## Verifications for coder (when this becomes a handoff)

1. `item.@this` base: confirm nothing already claims `Name` on the item base (modifier's Position-not-Order note shows the base owns names).
2. Transitional reflection read (`this.Item.cs:10-14` note): with `[Store]` off `Module`, any surviving reflection-built action path breaks — grep remaining `clr<action>` construction paths, route through the dict door.
3. Debug view routes reflection kind (`this.Item.cs:91-93`): verify element-valued `[Debug]` prop renders as the `{name}` property bag, tolerates computed props.
4. Template sweep (language boundary): `{{ a.ActionName }}` → `{{ a.Name }}` in `os/system/builder/templates/actionFormal.template`, `os/system/builder/llm/templates/stepForLlm.template`, `os/system/actions/summary.md`, `os/system/actions/v2/summary.md`. `{{ a.Module }}` unchanged (element ToString). ~82 C# `ActionName` sites, ~123 test `Module = "..."` sites — mechanical.
5. C# overload sanity: `Create()` / `Create(dict, data)` / static `Create(object?, data)` on `action.@this`; `Create()` private / `Create(dict, data)` / `Create(name, ctx)` on `module.@this` — distinct signatures, legal; verify no resolution surprises at call sites.

## OBP validation pass (new surfaces)

| Surface | Check |
|---|---|
| `Create` (×7 doors) | single verb, caller intent, one vocabulary word ✓ |
| `Name` (property) | single noun ✓ (was ActionName compound) |
| `Module` (property) | single noun, element-typed, throwing getter ✓ |
| `ToString()` | display identity owned by the value ✓ |
| `Handler(string)` internal on module | noun-method — legacy, internal-only, the single registry mechanism door; acceptable residue, revisit if it grows callers |
| `Capabilities` (NEW) | noun property ✓ |
| Each dict key read by its owner | no opened-box at doors ✓ |
| Catalog subtype via virtual `Create()` | no type-switch in registry ✓ |
