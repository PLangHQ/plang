# Coder — compare-redesign

## Version: v6 — implementing Stage 2.1 (make the door actually async). Production stays GREEN.

Architect's `stage-2.1` (3 parts: A handler reads / B nav→ValueTask / C getter rewrite+null model).
My audit caught the 3-way gap; architect folded it in, then folded Stage 8 into Part C.

### Part A — handler reads → `await Value()` — substantially DONE, build green
`app/module/` `.Materialize()`: **272 → 112**, all increments committed/pushed, build 0 errors throughout.
- **Routing handlers flipped:** math/* , list/* (sync→async), crypto (ICrypto interface cascade),
  builder/validateResponse (contained cascade), variable/{get,exists,remove}, event/remove, error/throw,
  test/run lambda, + the async-method swaps on the big files (builder/llm/assert/http/debug/Fluid/identity).
- **The 112 left split cleanly:**
  - **54 optional `?.Materialize()` → DEFER to Part C.** The verbose `(X==null?null:await X.Value()) ?? d`
    is the exact intermediate C's null model eliminates; doing them now = churn. (Of these, ~33 I already
    did verbose in earlier increments — C retrofits those too.)
  - **~44 gate exemptions** (documented in `v6/gate-exemptions.md`): serializer `JsonConverter.Write`
    (signing), `IFileInfo`/`IFluidIndexable` (Fluid), diagnostic/display (debug, like ToString), build-meta
    handlers (builder/code/Default — process LLM build output, never runtime refs). The architect should
    formalize these as gate exemptions (like `System.IO`'s `app.type.path.**`).
  - **~4 Stage-6-owned** (list/sort, condition/Operator — two-phase sort / old mediator).
  - **~10 scattered sync helper/predicate/service-resolution** sites — per-site flip-vs-exempt (noted).

### Part C — KEY FINDING: `.Value(fallback)` cannot land additively
I added the `.Value(fallback)` overloads (base + `Data<T>`) expecting them additive — they produced
**208 errors**. A second `Value` overload makes `data.Value` (method group) **ambiguous**, breaking every
remaining silent `data.Value` method-group site in production (the CS8974-equivalents — passed to
`object?`/delegate, compiled before via single-overload method-group conversion). So the overload — and
thus Part C — must land **with** the consumer migration of those method-group sites, not before. Reverted;
production green.

### B+C are one all-at-once interlocked change — NOT attempted AFK
- **B forces C:** `GetChild`/`Variable.Get` → `ValueTask` ⟹ `Data.As<T>` async (As<T> calls `Variable.Get`)
  ⟹ a sync source-gen getter can't call async `As<T>` ⟹ the lazy getter (C). And C's `.Value(fallback)`
  overload surfaces ~200 method-group sites (above). All one change.
- **Scope:** `GetChild`/`GetChildValue` (`app/data/this.Navigation.cs`), `Variable.Get`/`Resolve`
  (`app/variable/list/this.cs`, ~29 `Get` callers + 6 `Resolve`), the 3 navigators, `As<T>` async, the
  source-gen getter rewrite (`Emission/Property/Data/this.cs:40,44,54,58`) for lazy + non-null `Data.Uninitialized`
  + `[NotNull]` stamp + `[Default]`-on-null, the `.Value(fallback)` overload, AND the ~200 method-group
  consumer sites + the 54+33 optional-param retrofits.
- **Design fork to settle (architect):** design-1 = `ValueTask GetChild` (architect's; forces the
  Variable.Get ripple + As<T> async + C). design-2 = a **lazy-child `GetChild`** that stays sync and defers
  the read to `child.Value()` (the existing async door) — avoids the Variable.Get/As<T> ripple and the
  forced coupling, but changes nav-error timing to touch-time and is a bigger GetChild restructure. I lean
  design-2 (smaller blast radius) but it diverges from the plan — flag for the architect.

This is the change that must land all-at-once or the build is deeply red. Doing it half-way while AFK risks
an unrecoverable break with no course-correction. So: Part A landed safely + documented; B+C is the next
focused session's work (with the design-fork decision).

### B+C grind STARTED (Design-1) — build RED at the As<T>→C boundary (door-cutover mode)
Decisions settled: Design-1 nav async; **no exemptions** (three verbs — `await Value()`/`Peek()`/internal
`Materialize()`); Stage 8 folded into C. Part A finished accordingly (in-memory reads → `Peek()`, not Materialize).

**B done (committed red):** `INavigator.Navigate` + the 4 navigators → `ValueTask` (CanNavigate stays sync via
`Peek()`); `GetChild`/`GetChildValue` (`app/data/this.Navigation.cs`) → `ValueTask`; `Variable.Get` → `ValueTask`
(dotted branch awaits GetChild); `Variable.Resolve` → `ValueTask` (rewritten: pre-resolve all `%var%` through the
door, then the sync `Regex.Replace` lambda just looks up — the sync-wall fix, **no `GetAwaiter().GetResult()`**);
`Variable.Set` → `ValueTask` (ctor seeds system vars directly); the indexer removed; **39 Get/Set caller sites
await-wrapped**. Build 170→108.

**Remaining = C (the hard source-gen part), the 108 red errors are the worklist — all in `data/this.cs`:**
- `As<T>` / `AsCanonical` / `AsT_Impl` / `AsT_Convert` / `WrapAs` / `SubstitutePrimitive` / `WalkContainerVars` /
  `WalkDict`/`WalkList` / `ResolveParameter` → **async** (they call the now-async `Variable.Get`/`Resolve` at
  `this.cs:729,739,936,957,1192,1197,1226`). This is the chain that forces the getter.
- **Source-gen getter rewrite** (`Emission/Property/Data/this.cs:40,44,54,58`): lazy `GetParameter<T>` (no eager
  `As<T>`; resolution moves into `await Param.Value()`) **+** the null model (optional `?` → non-null
  `Data.Uninitialized`, `[NotNull]` stamp, `[Default]`-on-null) **+** `.Value(fallback)` overloads.
- **`.Value(fallback)` + the ~200 method-group `data.Value` migration land together** (the overload makes
  `data.Value` ambiguous — proven, 208 errors — so the 200 sites migrate in the same pass).
- Then the 47 optional-param retrofits to the clean shape (`await Mime.Value()` + `[Default]`).

This is one green-or-red unit; it's red mid-flight as designed. The 108-error list + the getter emission are the
exact continuation worklist.

### Next
1. (settled) Design-1 + no exemptions — done.
2. Coder (focused session): B+C as one unit — nav chain async, getter rewrite + null model, `.Value(fallback)`,
   then migrate the ~200 method-group sites + optional-param retrofits in the same pass; land green.
3. Then Stages 3–6.


## v6 continued — dispatch-resolution model landed; suite green except stage stubs

**Final model (Ingi's design, settled after the Param/Seal/HoldsReference smells):**
%var% decode happens ONCE per execution at the dispatch boundary — the generated
`__ResolveParameters()` (awaited by `ExecuteAsync`/`SetAction` before Run/Build) resolves
each .pr parameter into the handler's backing field. The handler instance is the
per-execution home: no per-execution Data copies, no caching on the shared .pr param,
no store sealing. The async value door (`await Value()`) remains for CONTENT — a
path/file value loads on its own first read (Stage 3); an unused param costs one cheap
decode, never I/O. Getters are plain backing reads (sync Uninitialized/[Default]
fallback for direct C# composition). Deleted: Param factories, Seal(), HoldsReference(),
the _resolved latch, decode-at-Set, dead ResolveParameter. `data/` has no GoalCall
calls left (one IsSelfResolvingParams walk-skip remains; deletes with born-native-at-load).

**Suite: 4165/4290.** The 125 failing = 124 CompareRedesign stage stubs
(`Assert.Fail("Not implemented")` bodies, Stages 2-7) + 1 known port-collision flake
(HttpTestServer). All real regressions fixed, including: null-model consumer gates
(foreach/Ed25519/channel.set), literal typed-conversion, boxed-ValueTask sites,
SubstitutePrimitive door-read (DynamicData factories), Authorize EOF=deny (infinite
reprompt hang), Fluid null guard, Reconstruct/Normalize materialize (sqlite rows).

### Next
1. Fill the 34 Stage-2 stub bodies (they test the door/dispatch model just built).
2. Born-native composite reconstruction at load (FromWireShape reconstructs GoalCall
   from its type tag) -> delete GoalCall.Convert + the IsSelfResolvingParams carve-out.
3. Stages 3-7 (reference narrowing, per-type Compare, data.Compare entry, demolition).
4. plang --test (clean rebuild first - stale-binary trap; read the docs per CLAUDE.md).
