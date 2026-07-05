# Settings / Config / Options — one concept, three surfaces

**Author:** coder
**Branch:** `settings-config-unification` (cut from `cli-app-property-override`)
**Status:** design writeup for architect review — no implementation on this branch yet
**Origin:** surfaced while fixing the `plang '--build={"files":[...]}'` startup crash on
`cli-app-property-override`. Fixing that "properly" (Ingi) means resolving the config/settings
overlap first, rather than taping a fourth mechanism on top.

---

## 1. The trigger

`cli-app-property-override` set out to fix one crash: `--build={"files":[...]}` throws
`InvalidCastException: String cannot lower to this` at startup. Root cause is
`app.type.catalog.@this.Populate` (`PLang/app/type/catalog/Conversion.cs`), which **lifts then
LOWERS** each CLI value onto the target property — `text → path` is a *convert*, not a lower,
so it blows up.

The obvious fix: walk the app-tree node's properties and convert leaves via `TryConvert`
(the correct door). I started building that as an `IConfigurable` interface + `ConfigWalk`.

Then the name collided with an **existing** `app.config.IConfig`. Pulling that thread showed
the collision is not nominal — **we already have this concept, twice, under two names**, and
`--build`/`--test`/`--debug` are a *third* variant bolted beside them. Building a fourth walk
would deepen the mess.

---

## 2. What actually exists today (traced, with file:line)

Three overlapping surfaces, all meaning "configurable values on the app":

### A. `app.Config` — in-memory settings **registry** (scope chain)
`PLang/app/config/this.cs` — `app.config.@this`, aliased `AppConfig`.

- Owns `Defaults` (app-level scope) + `Resolve<T>` which walks
  `context.ConfigScope → parent.ConfigScope → … → Defaults → classDefault`
  (`config/this.cs:31`).
- `app.config.IConfig` (`config/IConfig.cs`) is a **marker** for module settings types.
  Implementers: `http.Config`, `signing.Config`, `environment.number.Config`
  (`grep ": IConfig"`). These are plain-CLR records (`int TimeoutInSec`, `string? BaseUrl`, …
  — `module/http/Config.cs`), **not** plang types.
- **Read:** `app.Config.For<http.Config>(ctx).Resolve("timeout", 30)`
  (`ModuleView<T>`, `config/ModuleView.cs`; call sites `module/http/code/Default.cs:78`,
  `module/llm/code/OpenAi.cs:65`, `module/math/MathPolicy.cs:21`, `signing/code/Ed25519.cs:80`).
- **Write:** `app.Config.Apply<Config>(source, ctx, isDefault)` (`config/this.cs:` `Apply<TConfig>`)
  — a **reflection walk** of the source's properties into the scope
  (`module/http/code/Default.cs:243`, from the `settings` action).
- `IConfigure<TConfig>` (`module/IConfigure.cs`) links a configure-action to its `IConfig` class
  for the builder.

### B. `app.Settings` — persistent settings **store**
`PLang/app/module/settings/this.cs` — `app.module.settings.@this`, property `app.Settings`
(`app/this.cs:182`).

- Sqlite-backed (`settings` table, `settings/Sqlite.cs`, `IStore`), get/set/remove actions
  (`settings/{get,set,remove}.cs`). This is where settings **persist to disk/db**.

So "setting" is **already the name of two different things** — the in-memory registry is
*misnamed* "Config", and the persistent store *is* `Settings`.

### C. `--test / --debug / --build / --app` — born-on-flag **live subsystems**
`PLang/Executor.cs` `Configure(...)` — the four-way flag branch.

- Each flag borns a **live object**: `app.Test = new app.test.list.@this(ctx)`,
  `app.Debug = new …debug.@this(ctx)`, `app.Build = new …builder.@this(ctx)`.
- Presence **is** the enable signal (`app.Test != null` ⇒ test mode).
- Config is applied by bespoke code: `Test.Apply(dict)` (`test/list/this.cs:105`, hand-rolled
  switch + bounds validation), `Debug.Apply(...)`, and `catalog.Populate(app.Build, dict, ctx)`
  — the **buggy lower** (`Executor.cs:99`).
- Unlike A, these hold **live state**, not scope key-values: `app.Test` owns a `tests`
  collection + `Current` + `Coverage` + `StartedAt`; `app.Debug` owns watchers.

---

## 3. The key question Ingi raised — and the answer

> "`--http={timeout:50}` / `--llm={model:".."}` is basically what we are doing … is source,
> mechanism, lifecycle really config?"

**For modules (A): yes, identical.** A CLI `--http={timeout:50}` is exactly
`app.Config.Apply<http.Config>(dict, ctx, isDefault:true)` — the *same reflection walk* the
`settings` action already runs, just sourced from argv with `isDefault:true`. My `ConfigWalk`
would **duplicate `Apply<TConfig>`** (OBP smell #3: same logical thing, two implementations).
→ CLI module-config must **reuse** `Apply<TConfig>`, never a parallel walk.

My earlier "different source / mechanism / lifecycle" distinction was **wrong** — Ingi caught
it. Those aren't different concepts; they're **read vs write of one store**:
- *source* — argv is just another writer into the scope (alongside the `settings` action).
- *mechanism* — reflection-walk-props-into-target already exists (`Apply<TConfig>`).
- *lifecycle* — CLI writes early (startup); modules read lazily. Same store, one write is
  just earlier.

**For subsystems (C): no — genuinely different.** `--test/--debug/--build` born **live
objects**, not scope key-values. `app.Test != null` is the enable bit; its config lives on the
object; it carries runtime state (the tests collection). You cannot model those as scope
settings without losing the object and the presence signal.

---

## 4. The honest split

Not *config vs setting*. It's **settings (scope values) vs subsystems (live objects)**:

```
--http, --llm, --signing   → scope key-values, no live object, read lazily        = SETTINGS (A/B)
                              with goal→app precedence
--test, --debug, --build    → born-on-flag LIVE objects with runtime state         = SUBSYSTEMS (C)
--app                       → the root object's own scalar props (Create, etc.)     = the root, a subsystem-like node
```

The **unifying OBP idea is real**: *every configurable thing applies its own config* (the
object is responsible for itself). It just has **two sinks**:
- a **setting** writes the scope (`Apply<TConfig>` → `Resolve` reads it back), and
- a **subsystem** sets its own live properties (the property walk — the thing I was building).

That's one interface, two polymorphic implementations — which is correct OBP, *if* we name and
place it so it doesn't fork.

---

## 5. Proposed end-state (for architect to accept / redirect)

### 5.1 Collapse the naming — "setting" is the domain word
Today: `app.Config` (registry, misnamed) + `app.Settings` (store) + `IConfig` (marker) +
`IConfigure` + `catalog.Populate` + three `.Apply` variants. Six names, one domain.

Proposal:
- **Rename `app.config` → `app.setting`** (registry). `app.Config` property → `app.Setting`.
  It *is* the settings registry; "config" is the misnomer.
- The marker `app.config.IConfig` → **`ISetting`** in the same namespace. (This is the
  collision Ingi foresaw — it's fine *because* it replaces the misnamed one; the persistent
  store `app.module.settings` keeps its name, or see 5.4.)
- Frees the word **"config"** entirely — no more `Config` property, `IConfig` marker,
  `catalog.Populate`.

### 5.2 One "apply settings to self" abstraction
An interface (working name `ISettable` / `IConfigurable` — **naming is open**, see §6) whose
default walks public-set properties, converting leaves via **`TryConvert`** (never `.Clr`-lower
— *this is the crash fix*) and recursing into composites:

- **subsystems** (`app`, `Test`, `Debug`, `Build`) use the default walk; `Test`/`Debug`
  override to add inline validation / watcher-wiring right where config is set.
- **module settings** (`http`, `llm`, …) apply through the registry
  (`Apply<TConfig>(isDefault:true)`), which is the *same* reflection walk — unify the two walk
  bodies into one so there's a single implementation.

### 5.3 Delete the three redundant appliers
`catalog.Populate` (the buggy lower), `Test.Apply` (bespoke switch), `Debug.Apply` — all fold
into the one walk. `--build` crash dies with `Populate`.

### 5.4 Open: does the persistent **store** (B) fold in too?
`app.Config` (registry, in-mem) and `app.Settings` (store, on-disk) are the read-cache and the
persistence of *the same settings*. They may want to be one concept with two tiers
(`app.setting` with an in-mem scope + a persistent backing), or stay split. **Architect call** —
this is the largest reshape and touches the source generator + the `settings` action.

---

## 6. Open questions for architect

1. **Naming.** `app.config → app.setting`, `IConfig → ISetting`? And the self-apply interface —
   `ISettable`? `IConfigurable`? something else? (Ingi leans "setting"; `ISetting` collides with
   `app.Settings`/`app.module.settings` unless we also address B.)
2. **Registry + store (A + B) merge, or leave B alone?** How far does "one setting concept" go?
3. **Do module-settings types (`http.Config`) become plang types** (like the subsystems now are),
   or stay CLR records? The scope store holds raw CLR today; subsystems hold `number`/`path`/etc.
4. **Source generator.** `config/IConfig.cs` doc says the generator emits scope-aware property
   bodies + a `settings` action from `IConfig` — but `grep IConfig PLang.Generators` returns
   nothing. Need to confirm what actually generates before renaming the marker.
5. **Scope of first branch.** Minimal = fix the subsystem walk (crash) under a final name +
   delete `Populate`. Full = the §5 collapse. Stage it how?

---

## 7. Reference sketch (the walk I started, then reverted)

Not a proposal for the final shape — just the mechanism, for reference. The load-bearing line is
`TryConvert` (the fix) replacing `Create(v).Clr(propType)` (the crash).

```csharp
// each node configs itself; default walks public-set props
public interface IConfigurable
{
    void Config(IDictionary<string, object?> config, actor.context.@this ctx)
        => ConfigWalk.Apply(this, config, ctx);
}

internal static class ConfigWalk
{
    public static void Apply(object node, IDictionary<string, object?> config, actor.context.@this ctx)
    {
        foreach (var (key, raw) in config)
        {
            if (raw is null) continue;
            var prop = node.GetType().GetProperty(key,
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (prop?.SetMethod?.IsPublic != true)         // access level = exposure control
                throw new ArgumentException($"--{key}: no public-settable property on {node.GetType().Name}");

            if (raw is IDictionary<string, object?> sub)   // composite: sub-node configs itself
            {
                var child = prop.GetValue(node) ?? Activator.CreateInstance(prop.PropertyType);
                if (child is IConfigurable ic) ic.Config(sub, ctx);
                else if (child != null) Apply(child, sub, ctx);
                prop.SetValue(node, child);
            }
            else                                           // leaf: convert (NOT lower) — the fix
            {
                var (converted, err) = app.type.catalog.@this.TryConvert(raw, prop.PropertyType, ctx);
                if (err != null) throw new ArgumentException($"--{key}: {err.Message}");
                prop.SetValue(node, converted);
            }
        }
    }
}
```

Note: this sketch still has the *duplication* problem of §3 — it re-implements `Apply<TConfig>`'s
walk. The final design should have **one** walk body serving both sinks.

---

## 8. Current state of `cli-app-property-override`

The crash is **not yet fixed** on that branch — Stage 3 (the walk) was paused here to resolve
this. Committed & pushed there: the test-subsystem collapse + plang-typing (Stages 1–2). The
exploratory Stage-3 WIP (`IConfigurable.cs`, `engine→app`, `Debugging→Debug`) was reverted, not
committed. The crash fix should land **after** this design settles, using whatever name/shape
the architect signs off.
