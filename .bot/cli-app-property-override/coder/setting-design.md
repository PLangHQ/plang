# `app.Setting` — the unified setting authority (design, agreed with Ingi 2026-07-06)

Settled through the Stage-3 design conversation. This supersedes the plan's "convert walk in
Configure" wording: the walk is one method on a single unified `app.Setting`, not a free helper.

## The thesis
Everything configurable is a **setting**. There is ONE setting authority, `app.Setting`
(type `app.setting.@this`), and it holds **both lifetimes** — in-memory (this run) and persistent
(sqlite) — behind a `Storage` switch. Nothing is a sibling; the persistent sqlite wrapper folds
*in*, it does not move *out*.

## Two axes of resolution (name them so "action" isn't ambiguous)

**Axis 1 — Scope (where a value was set), walked local → global:**
```
context (running goal's door) → parent contexts (outer calls) → app (root door) → [Default]
        in-code `set %!x%` lands per-context          CLI --flags land at app root      code floor
```
**Axis 2 — Specificity (which key), tried most-specific first within each scope level:**
```
module.action.param → module.param
```
One resolve = walk Axis-1 levels; at each, try Axis-2 keys; miss everywhere → `[Default]`.

## The type

```csharp
namespace app.setting;

public enum Storage { InMemory, Persistent }

public sealed class @this
{
    internal const string Table = "settings";                       // sqlite table name (data-compat), internal
    private readonly @this? _parent;                                // scope chain
    private readonly actor.context.@this _context;                  // born-with-context (not-found Data + walk conversion)
    private readonly ConcurrentDictionary<string, data.@this> _values = new(OrdinalIgnoreCase);  // in-memory
    private readonly Task<IStore>? _store;                          // sqlite — ONLY the app-level root holds one

    private @this Root => _parent?.Root ?? this;                    // persistent always resolves at the root

    // ── ONE reader — storage is the switch, value is always Data ──
    public ValueTask<data.@this> Get(Storage storage, params string[] keys)
        => storage == Storage.InMemory ? new(InMemory(keys)) : Persistent(keys);

    private data.@this InMemory(string[] keys)                     // walk this → parent → app root; stored Data AS-IS
    {
        for (@this? s = this; s != null; s = s._parent)
            foreach (var key in keys)
                if (s._values.TryGetValue(key, out var hit)) return hit;
        return _context.NotFound(keys is [var k, ..] ? k : "setting");   // unset → seam falls to [Default]
    }

    private async ValueTask<data.@this> Persistent(string[] keys)  // sqlite at Root; path "ApiKey" or "ApiKey.Sub"
    {
        var path = keys is [var p, ..] ? p : "";
        if (string.IsNullOrEmpty(path)) return _context.NotFound("setting");
        var dot = path.IndexOf('.'); var key = dot >= 0 ? path[..dot] : path;
        var remaining = dot >= 0 ? path[(dot + 1)..] : null;
        var result = await (await Root._store!).Get<global::app.type.item.@this>(Table, key);
        if (!result.Success) return result;
        var value = await result.Value();
        if (value is null || await value.IsEmpty())
            return _context.Error(new AskError($"Setting '{key}' is not set.", Table, key));   // unset → ASK, not Default
        return string.IsNullOrEmpty(remaining) ? result : await result.GetChild(remaining);
    }

    // ── ONE writer (mirror of Get) ──
    public ValueTask<data.@this> Set(Storage storage, string key, data.@this value);  // InMemory: _values[key]=value; Persistent: sqlite at Root

    // ── the CLI tree-config walk (different job: many typed props onto a node) ──
    public data.@this Set(object node, IDictionary<string,object?> settings);
}
```

**Not-found is deliberately asymmetric:**
- InMemory unset → `NotFound` → seam falls through to `[Default]` (a run-override that's simply absent).
- Persistent unset → `AskError` → runtime prompts the user (an `ApiKey` never provided *should* stop and ask).

## Wiring
- `app.Setting = new(app.System.Context, parent: null, store: _store)` — the ONE instance holding `_store`.
- `context.Setting = new(thisContext, parent: app.Setting)` — scoped; persistent delegates to `Root`.
- The persistent wrapper `app.module.setting.@this` (1 reader) is **deleted** — its Get/Set fold onto this type.
  The raw `IStore` stays as `_store` (internal — the public `SettingsStore` property name dies; Ingi disliked it).

## The CLI walk — `Set(object node, IDictionary settings)` (the bug fix)
Recursive, app-owned, public-setter-gated. Replaces `catalog.Populate` (the lift-lower crash source).
```csharp
foreach (var (key, raw) in settings) {
    var prop = node.GetType().GetProperty(key, Public|Instance|IgnoreCase);
    if (prop?.SetMethod?.IsPublic != true) return _context.Error($"unknown setting '{key}' on {node.GetType().Name}");
    if (raw is IDictionary<string,object?> sub && IsComposite(prop.PropertyType)) {
        var child = prop.GetValue(node) ?? Construct(prop.PropertyType);   // born-on-descend
        var r = Set(child, sub); if (!r.Success) return r;
        prop.SetValue(node, child);
    } else {
        var (val, err) = catalog.@this.TryConvert(raw, prop.PropertyType, _context, key);   // leaf — fixes the crash
        if (err != null) return _context.Error(err);
        prop.SetValue(node, val);
    }
}
return _context.Ok();
// catalog.Populate DELETED. TryConvert stays (what the walk calls). SetMethod.IsPublic gate = exposure-is-access-level.
```
**One call from the root** — Executor merges all `!`-flags into one dict, `app.Setting.Set(app, merged)`:
- `--app` spreads at root (Q2 alias); `--build` nests under `build`; subsystems **born-on-descend**
  (null composite prop + present key → `Construct` → new subsystem, "presence = enabled").

## Consequences / ripples
- **Seam (generator):** emits `await context.Setting.Get(Storage.InMemory, "module.action.param", "module.param") is { IsInitialized: true } __s`
  instead of sync `context.Setting.Resolve(...) is object __s`. Reads **`context.Setting`** (scoped, walks up to the
  app root) — NOT `context.App.Setting` (which would miss goal-scope). `IsInitialized` (not `Success`) is the "set"
  signal — `NotFound` has `Success == true`. `Resolve` is gone (Ingi disliked it). One regen.
- **As-built note:** the root reaches sqlite via `Root._context.App.SettingsStore` (kept internal for now — the
  full `_store`-on-root fold-in + `SettingsStore`-name death is a follow-up); the persistent wrapper
  `app.module.setting.@this` IS deleted (its Get/Set folded onto `app.setting.@this`), and `app.config.@this` (dead) deleted.
- `set %!x%` → `context.Setting.Set(Storage.InMemory, key, Value)` (stores the whole Data, no `.Value()` unwrap).
- `setting.set`/`get` actions → `Storage.Persistent`. `%setting.%` navigable → `Get(Storage.Persistent, path)`.
- `context.Setting` keeps its parent chain (in-code scope), root parent = `app.Setting`.
- `ScopeTests` retire; `SettingsTests` simplify; `config/this.cs` (the Resolve/Set facade) routes here or is deleted.

## Staging (one call needs 5/6)
Fully collapsing the four-way branch into one `Set(app, merged)` needs Debug activation (Stage 6) and
Test validation (Stage 5) off `Apply` first, so the walk can born+populate them. Until then, `!debug`/`!test`
stay born+`Apply`, with `Apply` calling the walk internally (kills the lift-lower everywhere now).
