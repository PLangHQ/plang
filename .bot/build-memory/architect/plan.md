# Build infers types through its own memory — and one reader for disk + LLM

**Author:** architect · **Branch:** context-never-null
**Origin:** refines `.bot/context-never-null/coder/builder-read-unification-plan.md` (coder). The coder framed it as "one deserializer." Working it through with Ingi, the deeper model is: the builder gets a variable memory, and a variable there can be `Uninitialized` — Type known, value unknown. One reader then falls out for free.

## Why

Build-time type inference today works by sniffing strings. To know that `%rows%` is a `table`, the builder walks backwards to the terminal `variable.set`, string-matches `%...%`, and stamps a `Type` param onto it (`StampTerminalType`). The type an action infers reaches the next action through a bespoke stamp, not through anything the rest of the system understands.

The runtime already has the right shape for this: a variable memory. At runtime `%rows%` is a variable that carries a value. At build it should be a variable that carries a **Type** and no value yet — because we haven't run anything, we just know its shape. Give the builder its own memory and the sniffing, the backwards-walk, and the `%!build` stamp all dissolve into ordinary variable reads: `build.Variable["rows"].Type`.

The same move unifies the two readers. A disk `.pr` and the LLM's build response deserialize into the *same* `action.@this` today, but through different options — disk borns `%rows%` as a live variable reference, the LLM borns it as the literal string `"%rows%"`. Once build has a memory that resolves references, both sources read through the one goal reader and born the instance identically.

## The picture

Build is the runtime memory model with **Types where values would be**. Nothing executes — each action's `Build()` infers a Type; `Run()` is never called.

```
                RUNTIME (values)              BUILD (types)
file.read       Run() → <rows>               Build() → {Type=table, Kind=csv}   ← from the string "data.csv"; file never opened
  → %!data%     %!data% = <rows>             %!data% = Uninitialized(table/csv)
set %rows%      %rows%  = <rows>             %rows%  = Uninitialized(table/csv)
db.insert       Run() → inserts              Build() → reads %rows%.Type=table   ← db never touched
```

## Change 1 — `Uninitialized` is its own state, split from `NotFound`

Today they are the same thing — a value-less miss with no Type:

```csharp
// data/this.cs:433 — the alias is the lie
public static @this Uninitialized(string name) => NotFound(name);
```

Split them. The states are a **three-question cascade** — and **Type rides orthogonally**, it is *not* one of the questions:

```
Found?  (was it supplied / is it in memory)
  ├─ no  ──►  NotFound        ← may still carry a declared Type:  NotFound<T> | NotFound (plain)
  └─ yes ──►  Initialized?
              ├─ no  ──►  Uninitialized   (present, value pending)
              └─ yes ──►  HasValue?
                          ├─ no  ──►  Null
                          └─ yes ──►  real value
```

`Found` is the primary axis and the new predicate — false for exactly one branch, `NotFound`. So `NotFound` reads as literally "`Found == false`."

The subtle line, and the one to get right: **`Uninitialized` vs `NotFound<T>` is not a Type question — both can carry a `T`. It is a `Found` question.** `Uninitialized` is *present, value pending*. `NotFound<T>` is *absent, but its declared type is known*. A `Data<T>` param that was never supplied is `NotFound<T>` — it knows its `T`, but it was never there.

| state | `Found` | `IsInitialized` | `HasValue` | Type |
|---|---|---|---|---|
| real value | ✓ | ✓ | ✓ | the value's |
| `Null` | ✓ | ✓ | ✗ | `T` / `@null` |
| **`Uninitialized`** | **✓** | ✗ | ✗ | `T` (or unknown) |
| `NotFound` / `NotFound<T>` | **✗** | ✗ | ✗ | none / `T` |

**Build inference reads `Type` off whatever carries one** — a build local (`Uninitialized`, Type known) or even a `NotFound<T>` slot; `Null` and plain `NotFound` carry nothing to check.

### Where `Uninitialized` is actually born — only two places

`Uninitialized` means *present in memory but no value yet*. Nothing that "wasn't supplied" is ever `Uninitialized`. It has exactly two births:

1. **Build memory** — `set %rows% = …` registers `%rows%` as `Uninitialized(table)` (the type-only dry run).
2. **An event bound to a not-yet-existing variable** — `on %user% create, call UserCreated` registers a `%user%` placeholder so the create-event can fire. It is `Found` (in memory, carrying the handler) with no value yet. (This is the `debug:162` site — today `NotFound` only because the two were aliased; post-split it becomes the real `Uninitialized`.)

Suggested births (born through the context, matching the branch's `context.Null` / `context.NotFound` factories at `actor/context/this.cs:205,217`):

```csharp
// context factory — sibling of context.NotFound. Type optional (event placeholder may not know it yet).
public data.@this Uninitialized(string name, type? type = null) =>
    new(name, context: this) { Type = type, IsInitialized = false /* Found = true */ };
```

Why the split matters: build must tell a **pending local** (`%rows%`, assigned earlier in this goal → Type known) from a **typo or an outside variable** (not in memory). It reads that difference as `Found`. `HasValue` cannot carry it — `HasValue` is false for both.

### Build is lenient on `NotFound` — cross-goal variables are normal

Goals build independently. Building `SaveUser`, `%user%` may be an input from a caller goal, never assigned in `SaveUser` itself. So at build a `NotFound` is **not an error** — it means "not declared here, may come from elsewhere."

```
build SaveUser:
  %user%   ─► build.Variable["user"]  ─► NotFound   →  tolerated, Type unknown (from a caller)
  %usr%    ─► (typo)                  ─► NotFound   →  also tolerated — indistinguishable per-goal
```

Consequences, stated plainly:
- A build type-check that wants a `NotFound` variable's Type **skips** — no info, no complaint.
- A real typo cannot be caught building one goal alone; it looks identical to a legit cross-goal variable. Only a whole-app build (all goals in view) could catch it. Out of scope here.
- Runtime keeps `NotFound`-as-error. This leniency is a **build policy**, not a change to what `NotFound` is.

### The source generator: an unbound param slot is always `NotFound`, never `Uninitialized`

The `Uninitialized` factory is called in exactly one place today — the source generator, seeding an unsupplied optional param (`PLang.Generators/Emission/Property/Data/this.cs:40,43,85`). The null model requires the slot be **non-null before `Resolve()` binds it** (that is why it is seeded at all; `[NotNull]` is the annotation that makes it compile, not the reason). But an unbound slot was *never supplied* — it is `Found == false`. So the field initializer is always the not-found state, typed or not:

```
Data<T>  unbound  →  NotFound<T>   (declared type T known, but not supplied)
Data     unbound  →  NotFound      (no type, not supplied)
```

The generator never emits `Uninitialized` — "unbound" is always `NotFound`, so there is no absence-citizen for it to *choose*. It only stamps the type `T` it already has from the signature. `Resolve()` then overwrites the slot with the real value when the arg exists.

### `Uninitialized` call-site audit — resolved

Every current `Uninitialized` mention means "not supplied / not in memory," except the one placeholder case:

| site | what it does | disposition |
|---|---|---|
| generator `:40,43,85` | seeds unbound param slot | → `NotFound` / `NotFound<T>` (was `Uninitialized`) |
| `http:96` | guards `Body.IsEmpty()` | unchanged — `IsEmpty` true either way; comment → `NotFound` |
| `mock:24` | reads `Return.IsInitialized` | unchanged — comment → `NotFound` |
| `foreach:49` | reads `KeyName.IsInitialized` | unchanged — comment → `NotFound` |
| `variable Get<T>:636,645` | already returns `NotFound` on a miss | fix stale "Uninitialized" doc |
| `debug:162` | event placeholder for a not-yet-existing variable | → **`Uninitialized`** (it *is* `Found`; today `NotFound` only by aliasing) |

No runtime read site breaks — they all read `IsInitialized`/`IsEmpty`, false for both. The only behavioral flips are the generator (→ `NotFound`) and `debug` (→ `Uninitialized`).

## Change 2 — the builder gets its own memory; `Build()` reads and writes it

The "build pass" is a plain loop. It builds a goal by making each action's instance and calling `Build()`, threading the inferred Type through a build-scoped memory so the next action can read it.

> **You own this code.** The snippets are the intent, not the final shape. Names, seams, and signatures are the coder's call.

```csharp
// Build a goal = loop its actions, thread each inferred Type through build memory.
// `build` is a context with its OWN Variables — isolated from any runtime memory.
async Task Build(Actions actions, context build)
{
    foreach (var action in actions)
    {
        var handler = await action.Resolve(build);   // instance, params bound against build memory
        var type    = await handler.Build();          // infer Type — pure, no Run, no I/O
        await build.Variable.Set("!data", type);      // last result → next action reads it
    }
}
```

The only reason it is a loop and not N isolated calls: action N+1 reads the Type action N produced, through `build` memory. That shared memory is the whole point.

`variable.set` writes its **own** target — no external stamp:

```csharp
// variable.set.Build() (suggested)
build.Variable.Set(Target.Name, context.Uninitialized(Target.Name, incomingType));
```

`incomingType` is read from build memory (`%!data%` or a named `%var%`), exactly as `variable.set` at runtime reads its source value. `%!build` disappears — it was `%!data%` all along.

### The build context is a plain `context` — there is no `BuildContext` type

`app.Builder` (`app/this.cs:203`) owns the build and already carries build mode (`IsEnabled` → in-memory datasources). Per goal, it borns a child context with its own memory, parented to the running one:

```csharp
var build = new context(app, owner: /* system actor */,
                        variables: new Variables(),   // isolated build memory
                        parent: running);             // links cancellation + hierarchy
```

That context threads down through `Resolve` + `Build()` unchanged. A `Build()` author sees a normal `context` and writes normal code — the build-ness is invisible, because it lives in the *memory the context carries*, not in its type:

```csharp
// variable.set.Build() — no BuildContext, no `if (building)`. Just context.
context.Variable.Set(Target.Name, context.Uninitialized(Target.Name, incomingType));
```

The same line runs at runtime against runtime memory. What differs is the threaded context, not its type. The no-side-effects discipline lives in *which method is called* (`Build` vs `Run`), not a context flag.

**Per goal, fresh memory.** Each goal builds with its own memory so goal A's `%x%` never leaks into goal B. Cross-goal inputs stay `NotFound` (the lenient policy). It is a child context per goal build, not one global.

### Later: feed values in

Because `Uninitialized` is a real slot keyed by name, a later pass can fill it from a prior test run — `build.Variable.Set("user", realValueFromTest)` flips `IsInitialized` true on the same slot. Not in scope now; the state model is chosen so it stays open.

## Change 3 — one reader for disk `.pr` and the LLM build response

Both sources deserialize into the same `action.@this` today. The only difference is which read options run, and it is one gate:

```csharp
// Conversion.cs:298 — only a goal engages Wire template mode
targetType == typeof(goal.@this) ? GoalReadOptions(context)   // disk .pr: born as a variable reference
                                  : ContextualReadOptions(...)  // LLM build response: born as literal "%rows%"
```

Route the LLM build response through the **goal reader** too, so `%rows%` borns as a variable reference identically. The reader is then the same; build vs runtime differ only in the memory the context carries (Change 2).

### The §5.4 security line still holds

Template mode "rides the goal type" on purpose — it is the trust boundary (`Conversion.cs:46-53`). This change keeps it: the goal reader covers the disk `.pr` and the LLM build response — **both are goal source** (the LLM output *is* the goal being authored). Runtime data (a message body, an HTTP response) stays on its own literal read, so a forged `%secret%` in a message never borns a reference. Do **not** implement this as a "template mode" flag on the `BuildResponse` read — that reintroduces the read-path flag §5.4 rejects.

### `BuildResponse` is a goal mirror — it dies

`BuildResponse` is `{ Description, Errors, Warnings, Steps }` — every field is already a field of `goal.@this` (`Description` `this.cs:42`, `Errors` `:188`, `Warnings` `:191`, `Steps` already `Step`). It is a thinner copy of the goal (OBP smell #3), existing only to mirror the LLM's JSON shape — and that shape *is* a partial goal.

So the LLM builds a `goal.@this` directly:

```
LLM JSON  ──► goal reader ──►  goal.@this { Description, Errors, Warnings, Steps }
                                 ▲ no BuildResponse, no envelope, no smear onto Step
```

Every field lands in its existing slot. The LLM's build errors fold into `goal.Errors` (it already carries goal-level `Info` diagnostics — same list, decided). Per-step (`Keep`/`PriorText`) and per-action diagnostics keep their existing homes on `Step` / `action.@this`. **`BuildResponse` is deleted** — the "where does build metadata live" question was the goal all along.

**The `Validate` logic gets one home — the builder.** `BuildResponse` was never the real owner; it was a stand-in for "the build's result." That result is now a `goal.@this`, and validating it is a build operation, so the logic moves into the `validateResponse` handler / `IBuilder` (which already delegates there), taking the read `goal` vs the prior `goal`:

```
was:   response.Validate(priorGoal)     // on the mirror type
now:   IBuilder.Validate(readGoal, priorGoal)   // build behavior over the read goal
```

It does **not** go on runtime `goal.@this` — the checks (`keep:true`, count-vs-prior, index-gaps) are build-time-diff concerns a loaded `.pr` never asks. One home, in the builder. `FromGoalState` disappears — it only existed to mirror a goal *into* a `BuildResponse`; the safety net now validates the goal it already holds.

### What actually collapses (and what does not)

The coder plan says "most of the transform layer has nothing left to convert." Split that claim in two:

- **Bridge — dies.** The parts of `validateResponse` / `NormalizeParameterTypes` that exist only to turn the LLM's literal `%...%` strings and untyped params into typed goal params. Once the reader borns references and build memory carries Types, these lose their job.
- **Validation / repair — stays.** Module-name-separator repair, `goal.call` CLR-type-name rejection, required-parameter checks, catalog-description skips. These validate LLM output; they are unrelated to which reader ran. They survive the reader change unchanged.

The demolition worklist below draws that line member by member — that boundary is the riskiest part of the change and must be explicit before cutting.

### The bridge-vs-validation ledger (member by member)

**`NormalizeParameterTypes` (`Default.cs:884–998`) — nearly all bridge → dies wholesale.**

| block | verdict |
|---|---|
| schema type-stamp (~897) | bridge — the typed slot already knows `T`; `Resolve` + `Output` carry it |
| kind-stamp (~925), skips `%var%` via `StartsWith("%")` | bridge — kind is inferred by `Build()`; the `%` sniff → structural |
| convert either-direction (~977) | bridge — typed resolution converts |
| **template flag (987–998)** | **the centerpiece bridge** — detects `%var%`, stamps `type.template="plang"`. The goal reader borns the template directly, so the block vanishes |

Its one non-bridge concern — "does this literal convert to the declared type" (~983) — is already done by `Validate` (176–186). Nothing is orphaned.

**`validateResponse` / `BuildResponse.Validate` — nearly all validation → stays.** Auto-fill missing indexes, step-count, keep-invariant, no-actions, index-gaps, empty-`""`→null repair, choices vocabulary, convertibility, `IsActionRecord`/scalar/choices skips — all stay (move onto the read goal). The `p.Peek() is variable` skip (125) **stays and simplifies** — a born reference *is* a `variable.@this`; drop the `HasVariableReference` string-fallback. `FromGoalState` (18) dies with `BuildResponse`.

**`enrichResponse` (`Default.cs:667`) — stays.** `keep`→copy-prior, source tagging, `RenderFormal` are build semantics; only change is reading `goal.Steps` instead of `response.Steps`.

```
NormalizeParameterTypes   →  gone    (bridge; the template flag was the born-source hack)
validateResponse/Validate →  stays   (minus FromGoalState + the ref string-fallback)
enrichResponse            →  stays   (reads goal, not BuildResponse)
```

## Sequencing

1. **Change 1 first** (`Uninitialized` split + `Found`). Pure state model, no build/reader dependency, unblocks the rest. Ship it green on its own.
2. **Change 2** (build memory + the `Build()` loop). Depends on Change 1. Delete `StampTerminalType` and the `%!build` stamp here.
3. **Change 3** (one reader). Depends on Change 2 (references only resolve once build has a memory). Draw the bridge-vs-validation line here.

## Leaf trace — incumbents and dispositions

- **`Data.Uninitialized`** (`data/this.cs:433` alias; `:795` the `<T>` form) — stops aliasing `NotFound`. Becomes *present, value pending* (`Found`, `IsInitialized=false`); born via `context.Uninitialized(name, type?)`. Only two births: build memory and the event-placeholder (`debug:162`). The generator no longer calls it (see audit).
- **`Data.NotFound`** (`data/this.cs:426`, `context/this.cs:217`) — stays the not-supplied / not-in-memory state; loses the `Uninitialized` alias. **Gains an optional Type** — a `NotFound<T>` slot (unbound typed param) knows its `T`. Type does not make it `Found`.
- **`Found`** — new predicate on `data.@this`, false only for the `NotFound` branch (typed or not). Contract only; impl is the coder's.
- **Generator field initializer** (`PLang.Generators/Emission/Property/Data/this.cs:40,43,85`) — was `Uninitialized`. Disposition: unbound slot → `NotFound<T>` (typed) / `NotFound` (plain). Never `Uninitialized`.
- **`StampTerminalType`** (`goal/steps/step/actions/this.cs:45`) — called at `builder/code/Default.cs:637,642`. Disposition: **deleted**. The `variable.set` handler writes its own target into build memory.
- **`RunBuildPass`** (`builder/code/Default.cs:609`) — verb-noun name, flat loop with no memory. Disposition: **replaced** by the `Build()` loop backed by build memory. Rename to `Build`.
- **The goal-type read gate** (`Conversion.cs:298`) — Disposition: the LLM build response reaches the goal reader; gate stays, the LLM side now qualifies as goal source.
- **`data.@this<BuildResponse> StepResults`** (`validateResponse.cs:16`) — the LLM read seam. Disposition: reads a `goal.@this` through the goal reader. `BuildResponse` is **deleted** — a goal mirror; `Description`/`Errors`/`Warnings`/`Steps` land in their existing `goal` slots. The `Validate` logic relocates into this handler / `IBuilder`, over `goal` vs prior `goal`.

## Demolition worklist

**Dies with Change 1**
- The `Uninitialized => NotFound` alias (`data/this.cs:433`). `Uninitialized` becomes its own state; `NotFound` gains an optional Type.
- The generator emitting `Uninitialized` for unbound slots (`Generators/.../Data/this.cs:40,43,85`) → `NotFound` / `NotFound<T>`.
- Stale "Uninitialized" comments at `http:96`, `mock:24`, `foreach:49`, `variable Get<T>:636,645` → `NotFound`. `debug:162` flips the other way → real `Uninitialized`.

**Dies with Change 2**
- `StampTerminalType` (`actions/this.cs:45`) and both call sites (`Default.cs:637,642`).
- `RunBuildPass` as a flat loop (`Default.cs:609`) → becomes `Build` over build memory.
- Any `%!build` stamp / infra-scoped build variable — folded into `%!data%` in build memory.

**Dies with Change 3 (bridge only — see the ledger above)**
- **`NormalizeParameterTypes` wholesale** (`Default.cs:884–998`) — type-stamp, kind-stamp, either-direction convert, and the `template` flag (987–998, the born-source hack) are all bridge. Its one validation concern (literal convertibility) already lives in `Validate`, so nothing is orphaned.
- **`BuildResponse` itself** (`BuildResponse.cs`) — a goal mirror; fields land on `goal.@this`.
- **`FromGoalState`** (`BuildResponse.Validate.cs:18`) — only mirrored a goal into a `BuildResponse`; the safety net validates the goal directly now.
- The `Validate` **method** does not die — its logic **relocates** into the `validateResponse` handler / `IBuilder`, taking the read `goal` vs prior `goal` (one home in the builder, not a mirror, not on runtime `goal.@this`).
- The `HasVariableReference` string-fallback in `Validate` (125) — a born reference *is* a `variable.@this`; the structural `p.Peek() is variable` check stays.

**Stays through Change 3 (validation / repair, not bridge)**
- `validateResponse`/`Validate` checks: step-count, keep-invariant, no-actions, index-gaps, choices vocabulary, convertibility, empty-`""`→null repair, auto-fill missing indexes.
- `enrichResponse` (`Default.cs:667`): `keep`→copy-prior, source tagging, `RenderFormal` — reads `goal.Steps`.

**Stays (do not cut)**
- Module-name-separator repair, `goal.call` CLR-type-name rejection (`Default.cs:~487-525`), required-parameter checks, catalog-description skips. These are LLM-output validation, not deserialization bridging.
- `NotFound`-as-error at runtime. Only the *build* policy is lenient.
- The §5.4 goal-type template boundary.

## What stays nullable / unknown on purpose

- A `NotFound` variable's Type at build is genuinely unknown (cross-goal input). Not a state to fill with a guess — type-checks skip it.

## Open questions

1. **Build context — resolved.** It is a plain `context` (no `BuildContext` type), born per goal on `app.Builder` with its own `Variables`, parented to the running context, **owned by the system actor** (no dedicated build actor — the set is system/user/service and build runs as system). See "The build context is a plain `context`" above.
2. **`Uninitialized` call-site audit — resolved.** See the audit table under Change 1. Generator → `NotFound`/`NotFound<T>`; `debug:162` → real `Uninitialized`; the rest read `IsInitialized`/`IsEmpty` and are unaffected (comments only).
3. **Build-metadata home — resolved.** `BuildResponse` is a goal mirror and is **deleted**; the LLM builds a `goal.@this` directly, and `Description`/`Errors`/`Warnings`/`Steps` land in their existing `goal` slots (build errors → `goal.Errors`). Folded into Change 3.
4. **Value pinning / writing effective defaults into the `.pr`** (coder Q4) — separate axis (deterministic `.pr`), own branch. Excluded here.
5. **`read-path-unification` overlap** (coder Q5) — shares the one-reader goal; sequence so they don't fight the same seam.

## OBP validation pass

| surface | verb+noun? | decomposition | verdict |
|---|---|---|---|
| `Uninitialized(name, type?)` | no | whole `type` carried, not `type.Name` | ok |
| `Found` (predicate) | no — one honest word | none | ok |
| `NotFound<T>` (typed not-found) | no — state name | Type rides orthogonally, not decomposed | ok |
| `Build` (was `RunBuildPass`) | no — was verb+noun+noun, fixed | none | ok |
| `build.Variable.Set("!data", type)` | no | Type flows whole through memory | ok |
| `variable.set.Build()` writing its own target | no | action owns its write, no external stamp (removes a Rule-1 smell) | ok |

`StampTerminalType` (the removed method) was itself the smell: build choreography reaching across files to mutate another action's params. Its deletion is the OBP win, not a side effect.

## You own the final shape

Every name and signature here — `context.Uninitialized`, `Found`, the `Build` loop — is intent, not spelling. If a cleaner seam appears while implementing, take it. The contract is: `Uninitialized` is a real, `Found`, typed-but-valueless state; build reads types through its own memory; one reader borns disk and LLM identically.
