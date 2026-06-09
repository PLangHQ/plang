# Stage 2.1: Make the door actually async — handler reads, navigation, and param resolution

> **Context.** Stage 2 shipped the async **signature** (`Value()` returns `ValueTask`) but sync **substance**: `Value() => new(Materialize())`, navigation stayed sync `@this`, and the source-gen param getter still resolves eagerly. Three async conversions Stage 2 specified were deferred — this stage builds all three, because **all three gate Stage 3** (the actual async I/O read). The coder's audit (`coder/v5/stage-2.1-audit.md`) caught that the first draft of this stage covered only one of the three; it's now corrected. Found in conversation with Ingi (the verbose-read thread) — expected work, not a surprise. **The former Stage 8** (optional-param `[Default]`/`[NotNull]`/non-null `Data`) is folded into **Part C**, at Ingi's call: it's the same getter emission and the same handler sites as 2.1c, so doing it here is one rewrite and one migration instead of two.

> **Two decisions settled with Ingi** (the coder's `coder/v6/stage-2.1-bc-report.md` asked):
> 1. **Navigation goes async via Design-1** — `ValueTask GetChild`, async climbs the chain (`Variable.Get`/`Resolve`/`As<T>` go async). The lazy-child Design-2 was considered and rejected: it makes `GetChild` *look* sync while secretly deferring the read, and carries a write-back-identity trap — hidden complexity that bites later. Async should be visible all the way up.
> 2. **No gate exemptions.** There is no allowlist of "genuinely-sync" sites. In-memory reads use `Peek()`; `Materialize()` goes `internal` to `Data` so handlers *can't* call it. A gate with carve-outs rots — we're building a language.

**Goal:** The async value door is real end-to-end. Every read that can reach content — a handler value-read, a `%x.field%` navigation, a param getter — routes through `await Value()` / the `ValueTask` nav chain, so when Stage 3 slots the async I/O read inside `Value()`, **nothing bypasses it**. Two public read verbs only: `Value()` (load if needed) and `Peek()` (what's in memory now, force nothing). `Materialize()` (sync force-load) becomes `internal` to `Data` — the compiler keeps it off the public surface.
**Scope:** Three parts (below): **A** handler value-reads, **B** the navigation chain → `ValueTask`, **C** the getter rewrite — lazy param resolution **+ the optional-param null model** (`[Default]`/`[NotNull]`/non-null `Data`, folded in from the former Stage 8). Excluded — Stage 3's actual async read path (this stage only makes the call sites door-routed so Stage 3 lands as a one-file change); the comparison work (Stages 4–6); the two-phase `sort` (Stage 6 owns it).
**Why now (the latent bug):** today `Value()` ≡ `Materialize()`, navigation is sync, params resolve eagerly — so nothing is broken *yet*. But the moment Stage 3 puts the async read inside `Value()`, every sync `.Materialize()`, every sync `GetChild`, and every eager param getter **skips it**. `read dir/` → `list<path>` → `count`/`where` over those paths would never load content. Routing all three through the door now means Stage 3 is the promised one-file change.
**Dependencies:** Stage 2. **Under Design-1, B and C are one interlocked unit** (the coder proved it): B makes `Variable.Get` → `ValueTask`, which forces `As<T>` async, which a sync C# property getter can't call → forces C's lazy getter. And C's `.Value(fallback)` overload can't land additively — a second `Value` overload makes the `data.Value` method group ambiguous, breaking the ~200 silent method-group sites — so the overload + those ~200 sites land in the same pass. Net: B (nav async) + C (lazy getter + null model + `.Value(fallback)`) + the ~200 method-group migration + the optional-param retrofit are **one green-or-red unit** — the door-cutover shape again. A (handler reads) is largely landed already.

---

## Part A — handler value-reads → `await Value()`

The ~272 direct `.Materialize()` calls across 60 `app/module/` files become `await x.Value()`; a sync `Run()` (`Task<T>` + `Task.FromResult`) flips to `async Task<T>`. `count.cs` is the textbook case: sync `Run()` reaching `.Materialize()` for both a name slot and a real value read.

**The per-site rule:**
1. **Value read (content that could become a `file`/`url` reference → I/O)** → `await x.Value()`; flip a sync method to `async`.
2. **Name slot (`Data<Variable>` / `IRawNameResolvable` — resolving *which* variable, never content)** → `await x.Value()` too. The generic door returns the typed value, so `var v = await ListName.Value();` gives the `Variable` directly (no `as` cast). I/O-free and sync-completing, but routed through the door for one rule.
3. **In-memory / raw read (diagnostics, `ToString`/`Equals`/`GetHashCode`, verbatim serializer `Write`, build-time literals)** → `Peek()` (the current rung, forces no load) — **not** `Materialize()`, and **not** an exemption. `Peek()` can't bypass Stage 3's read because it does no I/O; misusing it on an unloaded reference returns the reference form (visibly wrong, caught in tests), never a silent sync load.

**Optional-param sites go straight to the clean shape** (the null model lands here too — Part C): `Mime = await Mime.Value()` with `[Default("text/plain")]`, or `Actor = await Actor.Value(Context.Actor)` — never the verbose `(X == null ? null : await X.Value()) ?? default` intermediate. Because Part C ships in the same stage, migrate optional params once, clean.

**Retrofit what's already done.** The handler files you migrated during v5 (the door cutover's reference set — `variable/set.cs`, `file/read.cs`, `list/{contains,any,group,join}.cs`, and any others) predate the null model. Sweep them too: any verbose `(X == null ? null : await X.Value()) ?? default`, leftover `X?.Value`, or `== null` optional-param guard gets retrofit to the clean shape. Don't leave the already-migrated sites in the old form — they're the ones most likely to be missed because they look "done."

**Worklist (from the audit):**
- **flip method `async` + `await .Value()`:** `builder/validateResponse.cs`, `mock/intercept.cs`, `goal/getTypes.cs`, `list/{sort,set,get,indexof,flatten,reverse,last,remove,unique,add,count,first}.cs`, `crypto/code/Default.cs`, `settings/Sqlite.cs`, `math/{add,subtract,multiply,divide,intdiv,modulo,power,min,max}.cs`, `error/throw.cs`, `module/remove.cs`, `signing/Signature.cs`, `event/skipAction.cs`, `code/this.Snapshot.cs`. (`list/sort.cs` overlaps the two-phase sort — Stage 6.)
- **name-slot-only sync handlers (trivial flip, I/O-free):** `mock/{verify,reset}.cs`, `cache/wrap.cs`, `timer/{start,end}.cs`, `event/remove.cs`, `code/this.cs`, `crypto/{encrypt,decrypt}.cs`, `debug/tag.cs`, `variable/{exists,get,remove}.cs`.
- **already `async`, straight swap:** `builder/code/Default.cs` (38), `assert/code/Default.cs` (37), `llm/code/OpenAi.cs` (29), `debug/this.cs`, `error/handle.cs`, `ui/code/Fluid.cs`, `condition/Operator.cs`, `condition/code/Default.cs`, `test/{run,discover}.cs`, `http/code/Default.cs`, `variable/set.cs`, `list/where.cs`, `llm/query.cs`, `signing/code/Ed25519.cs`, `identity/code/Default.cs`.

---

## Part B — navigation chain → `ValueTask`

This is the v3 finding-A resolution: designed in Stage 2, never built — and **Design-1** is the chosen mechanism (lazy-child Design-2 rejected, see the decisions note up top). Navigation is currently sync `@this` (`GetChild` is `public virtual @this GetChild(...)`, `app/data/this.Navigation.cs`), so `%x.field%` reads content synchronously and bypasses the door regardless of Part A. `GetChild` awaits the parent's value to navigate, so async climbs the chain — convert it to `ValueTask`:

- `GetChild` / `GetChildValue` (`app/data/this.Navigation.cs`) → `ValueTask`-shaped; sync-completing in memory, awaits only the first content read.
- `Variable.Get` / `Variable.Resolve` (`app/variable/list/this.cs`) — the chain `GetChild → Variable.Get → Variable.Resolve → Value()`.
- The three navigators `app/variable/navigator/{List,Dictionary,Snapshot}.cs` — they read `data.Materialize()` today; route through the awaited door.
- **`As<T>` goes async** (it calls `Variable.Get` to resolve `%var%`), and every `As<T>` caller awaits. This is the wide part of the ripple — and the reason B forces C (a sync property getter can't call async `As<T>`).
- **The ripple must terminate — no `GetAwaiter().GetResult()`.** Async climbing the chain must reach an `await` at every caller. The one known sync wall is the source-gen property getter, handled by C's lazy getter. If the ripple hits *another* sync wall (a sync interface member, a framework override that can't be async), **surface it** — that wall is exactly where Design-2's laziness would have hidden, and under Design-1 we want it explicit, never papered over by blocking on async.
- **The await-once gate** — stand up the analyzer/grep gate Stage 2 called for (`ValueTask` awaited once per call site, no store-and-await-twice).
- **The sync framework surfaces use `Peek()`, not an exemption:** `ToString`/`Equals`/`GetHashCode` `Peek()` the in-memory value (never navigate/parse); template render (Fluid) materialises params up-front (async, at `SetValue`) then `Peek()`s over in-memory views.

`count.cs:13` (`data.GetChild("Count")` — sync, no await, then `countData.Materialize()`) is the combined A+B fix in one handler: the navigation becomes awaited and the value read becomes `await Value()`.

---

## Part C — the getter rewrite: lazy param resolution + the optional-param null model

One rewrite of the source-gen Data-property getter (`Emission/Property/Data/this.cs`, the four cases at `:40,44,54,58`) does two things at once — lazy resolution and the non-null null model (folded in from the former Stage 8, because it's the same emission and the same handler sites).

**Verify first** (one compile check, before anything leans on it): `[System.Diagnostics.CodeAnalysis.NotNull]` on the *implementing* part of a partial property is honored by Roslyn for read-site flow analysis. Attributes union across partial parts, so it should be — confirm, then build on it.

**Lazy resolution.** The getter resolves eagerly today — `__ResolveData(name).As<T>(Context)` / `.AsCanonical(Context)` runs the instant a handler touches the property, before any `await`. So `await Param.Value()` (Part A) returns an already-resolved value; the door's laziness (no I/O at `read X`, I/O on first navigation) isn't real for params until this lands.
- Add `GetParameter<T>(name)` returning a **lazy, door-backed `Data<T>`** — wraps the param, sets context, does **not** call `As<T>`/navigate. (`GetParameter(string, context)` non-generic already exists at `app/goal/steps/step/actions/action/this.cs:220`.)
- Rewrite the four getter emissions to bind that lazy `Data<T>`; resolution + the content read move into the handler's `await Param.Value()`.

**The optional-param null model.** Two nulls live in `(Mime == null ? null : await Mime.Value()) ?? "text/plain"`: `Mime == null` (slot not supplied) and `?? default` (resolved to nothing). The first is redundant — Data already models "absent" via `IsInitialized` (`Uninitialized`/`NotFound` vs the present-null sentinel); a nullable C# reference is a second encoding (OBP smell #6). Collapse both, in this same getter rewrite:
- **Optional param → non-null Data.** The nullable case (`:44`, returns C# `null` on `__d.IsEmpty`) instead binds `Data.Uninitialized(name)`. The reference is never null; only the resolved *value* is. (The required, `[Default]`, and plain-Data cases already return non-null.)
- **`Data.Uninitialized`, not `Data.Null`, for absent** — keeps absent (`IsInitialized==false`) distinct from supplied-null (`null.@this` sentinel, `IsInitialized==true`).
- **`?` stays the optional signal; no `[Optional]`.** Optional = `data.@this<T>?`, required = no `?`. Forgetting `?` → required → missing-param guard fires → loud, not silent.
- **Generator stamps `[System.Diagnostics.CodeAnalysis.NotNull]` on every `?` Data property.** Under `<Nullable>enable</Nullable>` a `?`-typed deref (`await Mime.Value()`) trips CS8602 even though the getter now guarantees non-null; `[NotNull]` (read-direction: "not null even if the type allows it") tells flow analysis the truth. Keep `?`, no `!`, no project-wide warning suppression.
- **`[Default(...)]` fires on a null resolved value too**, not only an absent slot — so `mime: %unsetVar%` falls to the default, matching the old `?? "text/plain"`. Static literals only (attribute args are compile-time constants); the generator lifts the literal into the typed `Data`.
- **`.Value(fallback)` door overload** for runtime/computed defaults `[Default]` can't express (`Context.Actor`, `TimeSpan.FromSeconds(30)`): `ValueTask<object?> Value(object? fallback)` on base, `ValueTask<T?> Value(T fallback)` on `Data<T>` — returns the resolved value, or `fallback` when null (absent or present-null). Sync-completing in memory.

**Consumer side (joins Part A).** With the null model in place, optional-param handler sites migrate straight to the clean shape (`await Mime.Value()` + `[Default]`, or `await Actor.Value(Context.Actor)`) — no verbose intermediate. `== null` survives only where a handler genuinely distinguishes absent from supplied-null, as `!X.IsInitialized`. The resolution-error guard reorder (`var v = await x.Value(); if (!x.Success) return …; … v …`) is the same consumer change.

**Coordinate with Stage 7.** Stage 7 retypes some consumer fields (e.g. `channel.Mime` `string`→`text`); 2.1c's `[Default("text/plain")]` seed stays a string literal regardless (attribute constant) and the generator lifts it — the two don't collide.

---

## The gate — no exemptions, enforced by the type system

The gate isn't a grep with an allowlist (those rot). It's the access modifier: **`Materialize()` is `internal` to `Data`.** Then `app/module`, `app/variable`, and every other consumer literally *cannot* call it — the compiler is the gate, there's no carve-out to grant and no regression to catch. The public read vocabulary is exactly two verbs:

- **`await Value()`** — give me the value, load it if needed.
- **`Peek()`** — what's in memory now / the raw rung; forces nothing, does no I/O.

Every former "exempt" site resolves to one of these — content → `await Value()`, in-memory → `Peek()` — never a third path. Concretely: serializer `Write` / `GetHashCode` / `Equals` / Fluid / `--debug` diagnostics → `Peek()`; build-meta (`builder/code/Default`, literal LLM build output) → `Peek()` (no `IBuilder` async cascade needed); any `--debug` site currently using `GetAwaiter().GetResult()` to force a load → make that method async (it's the sync-over-async smell, not a diagnostic exemption). Content reads that could reach a `file`/`url` reference (`llm` content, `mock` param values) → `await Value()`.

`Peek()` stays a rare, intentional verb ("I want the in-memory rung, not a load"), but it needs no hard gate of its own: it does no I/O, so it cannot cause the Stage-3-bypass bug — a misuse returns the unloaded reference form (visibly wrong), never a silent sync load.

## You own this (coder)

You audited this — trust your read of each site. Non-negotiable outcome: the door is async in *substance*, not just signature — handler value-reads, `%x.field%` navigation (Design-1: `ValueTask GetChild`), and param getters all route through `await Value()`/the `ValueTask` chain; `Materialize()` is `internal` to `Data` (the compiler is the gate); in-memory reads use `Peek()`; **no `GetAwaiter().GetResult()` introduced anywhere** — if the async ripple hits a sync wall, surface it. B + C + the ~200 method-group migration land as one unit (the door-cutover shape). Where this overlaps Stage 6 (two-phase sort), do the shared piece once.
