# Stage 2.1: Make the door actually async — handler reads, navigation, and param resolution

> **Context.** Stage 2 shipped the async **signature** (`Value()` returns `ValueTask`) but sync **substance**: `Value() => new(Materialize())`, navigation stayed sync `@this`, and the source-gen param getter still resolves eagerly. Three async conversions Stage 2 specified were deferred — this stage builds all three, because **all three gate Stage 3** (the actual async I/O read). The coder's audit (`coder/v5/stage-2.1-audit.md`) caught that the first draft of this stage covered only one of the three; it's now corrected. Found in conversation with Ingi (the verbose-read thread) — expected work, not a surprise.

**Goal:** The async value door is real end-to-end. Every read that can reach content — a handler value-read, a `%x.field%` navigation, a param getter — routes through `await Value()` / the `ValueTask` nav chain, so when Stage 3 slots the async I/O read inside `Value()`, **nothing bypasses it**. `Materialize()` reverts to the internal sync core (Data→Data plumbing + the genuinely-sync surfaces ToString/Equals/serialize).
**Scope:** Three parts (below): **A** handler value-reads, **B** the navigation chain → `ValueTask`, **C** the `GetParameter<T>` lazy getter + source-gen emission. Excluded — Stage 3's actual async read path (this stage only makes the call sites door-routed so Stage 3 lands as a one-file change); the comparison work (Stages 4–6); the two-phase `sort` (Stage 6 owns it).
**Why now (the latent bug):** today `Value()` ≡ `Materialize()`, navigation is sync, params resolve eagerly — so nothing is broken *yet*. But the moment Stage 3 puts the async read inside `Value()`, every sync `.Materialize()`, every sync `GetChild`, and every eager param getter **skips it**. `read dir/` → `list<path>` → `count`/`where` over those paths would never load content. Routing all three through the door now means Stage 3 is the promised one-file change.
**Dependencies:** Stage 2. **A and C are coupled** — A tells handlers to `await Param.Value()`, but until C makes the getter lazy, that await returns an already-resolved value, so the laziness isn't real for params; land C with or before A. B (navigation) is the separable axis.

---

## Part A — handler value-reads → `await Value()`

The ~272 direct `.Materialize()` calls across 60 `app/module/` files become `await x.Value()`; a sync `Run()` (`Task<T>` + `Task.FromResult`) flips to `async Task<T>`. `count.cs` is the textbook case: sync `Run()` reaching `.Materialize()` for both a name slot and a real value read.

**The per-site rule:**
1. **Value read (content that could become a `file`/`url` reference → I/O)** → `await x.Value()`; flip a sync method to `async`.
2. **Name slot (`Data<Variable>` / `IRawNameResolvable` — resolving *which* variable, never content)** → `await x.Value()` too. The generic door returns the typed value, so `var v = await ListName.Value();` gives the `Variable` directly (no `as` cast). I/O-free and sync-completing, but routed through the door for one rule.
3. **Data-internal plumbing + genuinely-sync framework surfaces (ToString/Equals/GetHashCode, serializer `Write`)** → leave as `Materialize()` (the internal sync core).

**Worklist (from the audit):**
- **flip method `async` + `await .Value()`:** `builder/validateResponse.cs`, `mock/intercept.cs`, `goal/getTypes.cs`, `list/{sort,set,get,indexof,flatten,reverse,last,remove,unique,add,count,first}.cs`, `crypto/code/Default.cs`, `settings/Sqlite.cs`, `math/{add,subtract,multiply,divide,intdiv,modulo,power,min,max}.cs`, `error/throw.cs`, `module/remove.cs`, `signing/Signature.cs`, `event/skipAction.cs`, `code/this.Snapshot.cs`. (`list/sort.cs` overlaps the two-phase sort — Stage 6.)
- **name-slot-only sync handlers (trivial flip, I/O-free):** `mock/{verify,reset}.cs`, `cache/wrap.cs`, `timer/{start,end}.cs`, `event/remove.cs`, `code/this.cs`, `crypto/{encrypt,decrypt}.cs`, `debug/tag.cs`, `variable/{exists,get,remove}.cs`.
- **already `async`, straight swap:** `builder/code/Default.cs` (38), `assert/code/Default.cs` (37), `llm/code/OpenAi.cs` (29), `debug/this.cs`, `error/handle.cs`, `ui/code/Fluid.cs`, `condition/Operator.cs`, `condition/code/Default.cs`, `test/{run,discover}.cs`, `http/code/Default.cs`, `variable/set.cs`, `list/where.cs`, `llm/query.cs`, `signing/code/Ed25519.cs`, `identity/code/Default.cs`.

---

## Part B — navigation chain → `ValueTask`

This is the v3 finding-A resolution: designed in Stage 2, never built. Navigation is currently sync `@this` (`GetChild` is `public virtual @this GetChild(...)`, `app/data/this.Navigation.cs`), so `%x.field%` reads content synchronously and bypasses the door regardless of Part A. Convert the chain to `ValueTask`:

- `GetChild` / `GetChildValue` (`app/data/this.Navigation.cs`) → `ValueTask`-shaped; sync-completing in memory, awaits only the first content read.
- `Variable.Get` / `Variable.Resolve` (`app/variable/list/this.cs`) — the chain `GetChild → Variable.Get → Variable.Resolve → Value()`.
- The three navigators `app/variable/navigator/{List,Dictionary,Snapshot}.cs` — they read `data.Materialize()` today; route through the awaited door.
- **The await-once gate** — stand up the analyzer/grep gate Stage 2 called for (`ValueTask` awaited once per call site, no store-and-await-twice).
- **The sync surfaces that genuinely can't `await`** (the three Stage 2 named): `ToString`/`Equals`/`GetHashCode` read the already-materialised backing only (never navigate/parse); template render (Fluid) materialises param values up-front at `SetValue`, then renders over in-memory views.

`count.cs:13` (`data.GetChild("Count")` — sync, no await, then `countData.Materialize()`) is the combined A+B fix in one handler: the navigation becomes awaited and the value read becomes `await Value()`.

---

## Part C — `GetParameter<T>` lazy getter + source-gen emission

The getter resolves eagerly today — `__ResolveData(name).As<T>(Context)` / `.AsCanonical(Context)` runs the instant a handler touches the property, before any `await` (generator `Emission/Property/Data/this.cs:40,44,54,58`). So `await Param.Value()` (Part A) returns an already-resolved value; the door's laziness (no I/O at `read X`, I/O on first navigation) isn't achieved for params.

- Add `GetParameter<T>(name)` returning a **lazy, door-backed `Data<T>`** — wraps the param, sets context, does **not** call `As<T>`/navigate. (`GetParameter(string, context)` non-generic already exists at `app/goal/steps/step/actions/action/this.cs:220`.)
- Rewrite the four getter emissions to bind that lazy `Data<T>`; resolution + the content read move into the handler's `await Param.Value()`.
- **Coordinate with Stage 8.** Stage 8 rewrites the *same* getter emission (non-null `Data.Uninitialized` for optional params + the `[NotNull]` stamp + `[Default]`-on-null). The lazy-getter change (here) and the non-null change (Stage 8) are one getter rewrite — do them together so the emission isn't churned twice.
- The resolution-error guard reorder you already applied (`var v = await x.Value(); if (!x.Success) return …; … v …`) is the consumer side of this.

---

## The gate (widened)

The Stage-2-draft gate ("zero `.Materialize()` in `app/module/`") was too narrow — it misses the navigators in `app/variable`, the exact `%x.field%` bypass sites. The full invariant after 2.1:

- `.Materialize()` callers are **only** the Data-internal sync core (`app/data` plumbing) and ToString/Equals/serializer surfaces.
- **Zero `.Materialize()`** in `app/module/`, `app/variable/navigator/`, and the nav chain (`Variable.Get`/`Resolve`).
- Navigation and param getters route through the awaited door.

Grep gate (mirrors the System.IO PLNG discipline): `grep -rn "\.Materialize()" PLang/app/module PLang/app/variable/navigator` returns zero.

## You own this (coder)

You audited this — trust your read of each site over the bucket it landed in. Non-negotiable outcome: the door is async in substance, not just signature — handler value-reads, `%x.field%` navigation, and param getters all route through `await Value()`/the `ValueTask` chain; `Materialize()` is the internal sync core only; the gate above passes. Sequence A+C together (the laziness coupling) and B as its own pass. If a site genuinely must read synchronously and provably never touches I/O, flag it rather than quietly keeping `Materialize()` — that's the one judgment to bring back, not decide silently. Where this overlaps Stage 6 (two-phase sort) or Stage 8 (getter emission), do the shared piece once.
