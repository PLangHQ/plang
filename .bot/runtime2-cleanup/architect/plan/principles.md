# OBP Cleanup — Principles & Stage Anatomy

This is the discipline reference for the cleanup plan. It is the tightest statement of OBP rules the architect now considers settled, plus the checklist every stage runs against.

## OBP smell tests (the eight)

Reproduced from `/CLAUDE.md` so the cleanup work is anchored against the canonical list, with the architect's notes inline. Each item is a yes/no question; any "yes" means the shape is wrong and the fix is structural, not a line edit.

1. **Public mutable collection with rules enforced from outside.** A type exposes `public List<T>` / `Dictionary<K,V>` / `HashSet<T>` and the `Add` / `Remove` / locking / eviction lives in another file. Fix: the collection becomes its own `@this` type with private lock and `Add(...)` / `IReadOnlyList<T>` surface. *Examples in this cleanup: `App._keepAlive`, `App._modules.All` walked by `App.DisposeAsync`.*
2. **Cross-file lock target.** `lock (other.X)` taken from outside `other`'s class. *Example: `Channels.WriteAsync` reaching into `channel.Stream` directly was this; partly fixed in Stage 6.*
3. **Same logical thing stored twice across types.** Overlapping semantics, similar names, same element type, same role. *Examples: the four console channels living in both App.Channels and actor.Channels (fixed); `Serializers` on both App and per-actor Channels (Stage 1 of this plan).*
4. **Allocate-here / mutate-there / clean-up-elsewhere.** One collection's lifecycle split across three files. *Example: `App._keepAlive` allocated in App, mutated by `KeepAlive(x)` / `RemoveKeepAlive(x)`, iterated in `DisposeAsync`. Stage 3.*

If removing one line of choreography requires editing three files, those three files are one missing type.

## The Context principle (foundational — added 2026-05-08)

Every object that *does something* holds a Context. Context exposes App; App exposes everything App holds. Through Context, any object reaches what it needs without holding direct refs to App, sub-systems, or sibling actors.

This is the rule that lets every other OBP rule work. The eight smell tests above (mutable collections leaking out, cross-file locks, allocate-here / mutate-there) all stem from objects holding wrong references — direct refs to siblings, parents, specific subsystems. Context replaces those direct refs with one navigation point. Without Context, "each object responsible for itself" doesn't compose: an object that needs `app.Settings` and `app.Modules` and `app.Channels.Serializers` ends up with three direct refs (or three constructor parameters) — exactly the smell. With Context, it has one (`_context`) and navigates.

### Where Context lives

- **As a field on long-lived `@this` classes** — `Channels`, `Modules`, `Settings`, `Errors`, etc. Internal code uses `_context.App.X` to reach what it needs.
- **As a method parameter on PLang-boundary calls** — `RunAction`, `Run`, `Start`. The boundary is "called from outside the runtime." The parameter is how the language hands control to the runtime.

### What needs a Context

Anything that needs to reach App or its sub-systems. The criterion is functional: **add a Context if the class needs it; don't add it if not.** The class type doesn't determine this — records, exceptions, and value-types can all hold a Context if it makes sense for them. `Data` is the case in point: it's a value-shaped type that holds Context because navigation/resolution needs it.

### Choosing what back-ref(s) a class holds

A class doesn't automatically take Context (or App) just because it's per-actor (or per-app). It takes **whatever back-ref(s) it needs to navigate to what it actually touches** — no more, no less.

Possible choices:

- **Context** — when the class touches per-actor state (Variables, Trace, the actor's specific identity).
- **App** — when the class needs app-level state only, and the actor is either irrelevant or reachable through some other property (e.g., `Channels.@this` has an `Actor` property set after construction; `Channels` itself doesn't need Context).
- **A specific parent-ref** (e.g., `Channel.Channels`, `Variable.parent`) — when navigating up the immediate parent chain is what the class actually does. Used when "go to my parent" is a natural operation, not a workaround for a missing direct ref.
- **No back-ref at all** — for pure values, leaves, IDs, exception types.

**Method-parameter Context** — when a class is per-app but a particular method needs per-actor state (e.g., `Goals.LoadFromFileAsync(... Context context)`), Context flows as a method parameter, not a field. The instance doesn't hold a Context; the call provides one per invocation.

### The smells

- **Allocating direct refs to everything you might want to navigate to.** A class with `_app`, `_actor`, `_context`, `_variables`, `_settings` all stored separately is a god-bag. One navigation point that gets you everywhere is enough.
- **Implicit per-actor dependencies dressed up as app-level reaches.** A class that takes `App` and internally calls `_app.CurrentActor.Variables` is hiding a per-actor dependency. Make it explicit — take Context where it's needed.
- **Holding a back-ref you don't actually use.** A class that takes `App` because "everyone takes App" but never reads from it. The back-ref ought to earn its keep.

### Trade-off worth naming

This is the minimalist position: each class declares only the back-refs it actually needs. The cost is judgment — readers need to look at what a class does to know what it holds. The benefit is each class is honest about its dependencies; there's no Context-as-god-bag hiding what's actually used. PLang chooses minimalism over uniform "everyone holds Context."

### What doesn't need a Context

App itself. App is the bootstrap root — `Context.App` *is* App, so App can't hold a Context (chicken/egg). App's own internal code reaches its sub-systems directly through field references. `app.Context` exists as a shortcut to the current actor's Context.

### How Context gets to objects

Construction flow:
```
new App(...)                           — bootstrap root
  ↓ creates
System actor, User actor               — Actor.@this(name, app, ...)
  ↓ each Actor creates
its Context                            — new Context.@this(app, ...)
  ↓ Actor passes Context to its sub-systems
new Channels.@this(context),
new Modules.@this(context), …
```

The actor receives App in its ctor (it has to — App is the parent). The actor creates one Context for itself with the App reference. The actor then constructs its sub-systems passing the Context. From there, no object below the Actor layer needs to know about App except through Context.

### Cross-actor navigation goes through App

When code in User's actor needs to reach System's state, the navigation is `Context.App.System.Context.X`. Shared things sit on App as a single instance and are surfaced on each actor's Context — e.g., `app.SettingsVariable` is one shared instance that gets registered on every actor's `Context.Variables`. Object lives on App; access path is per-Context.

### Module handlers see the calling actor's Context

When User runs a goal, the module handlers receive User's Context. Same goal run by System gives handlers System's Context. `Context.App` is the same App in both cases; `Context.Variables` differs. The handler doesn't construct its own Context — it consumes the one handed to it.

## The architect-sharpened rules (added 2026-05-07)

These are detection rules that feed the eight smell tests with concrete red flags every reader can spot in seconds.

### Rule A — Compound class names are a red flag

A class named `{Noun}{RolePattern}` is wrong because the role-pattern suffix is behaviour described as a class. The single-noun fix:

- The plural noun *is* the registry (`Channels` IS the channel registry — no `ChannelRegistry`).
- The singular noun *is* the entity (`Actor`, not `ActorEntity`).
- If you find yourself reaching for `Manager`, `Helper`, `Service`, `Handler`, `Loader`, `Holder`, `Wrapper`, `Container`, `Dispatcher`, `Builder`, `Coordinator`, `Controller`, `Mediator` — the type's name is wrong, and the role belongs *to* the noun.

**Sub-rule: role suffix duplicates the parent folder.** If the class name's role-pattern suffix names the folder it lives in, drop the suffix — the folder already says it. `SensitivePropertyFilter.cs` in `PropertyFilters/` becomes `Sensitive.cs` in `Filters/`. `DefaultGrepProvider.cs` in `Providers/` becomes `Grep.cs` in `Code/`. `OpenAiProvider.cs` becomes `OpenAi.cs` in `code/`. `SqliteSettingsStore.cs` in `Settings/` becomes `Sqlite.cs`. The fully-qualified type read as `App.{Folder}.{File}` reads naturally — `App.Filters.Sensitive`, `App.Data.Code.Grep` — without the redundant decoration. Same logic for `Default*` prefixes when there's only one impl variant in the folder.

**Quick screen**: `grep -E "class [A-Z][a-z]+[A-Z]"`. Two capital letters in a class name is the red flag. Every hit needs human judgment (typed exceptions are conventionally compound — `FileNotFoundException`, `UnregisteredMimeType` — and stay; some implementation-variant compounds may be best handled by per-impl folders) but the screen surfaces the candidates.

### Rule B — `Get<Plural>()` is a missing collection type

A method named `GetBindings()` returning a list of Bindings tells the architect there should be a `Bindings` `@this` that *is* the list. The method shape is hiding what should be a navigable property.

Refinement: `Get(uniqueKey)` returning **one item** is fine (`Variables.Get(name)`, `app.Goals.Get(name)`). The smell is `Get*()` returning a **list** — that's the collection that should exist as its own type. Every hit is a redesign hint: the list as data, the filter/query verbs as methods on the collection.

**Quick screen**: `grep -E "Get[A-Z][a-z]+s\("`.

### Rule C — Static fields are a missing `@this`

A `static` field — including `static readonly` — has no owner. The data is process-global, with no `@this` it belongs to. Fix: hand the field to the owning `@this` (App, or one of its children).

This rule covers **fields, not methods.** Static factory methods, conversion operators, and helpers (`static @this Ok(...)`, `static implicit operator string(Variable v)`) are behavior, not state, and stay.

Three exceptions for state:

1. **`const`** — compile-time constant, no allocation, no instance.
2. **`AsyncLocal<T>`** — flow-scoped, not process-global. Different mechanism, different problem.
3. **Lock objects whose guarded data is itself irreducibly static** — and on this codebase, that set is empty. If you reach for a static lock, the data it guards should move first; the lock follows. (The data lives lazily and globally *only because* the field is static; once the data has an `@this` owner with a deterministic construction point, the racing-thread problem disappears and the lock with it.)

**Quick screen**: `grep -rE "^\s+(public|private|internal|protected)\s+static\s+" PLang/ | grep -v 'static class\|static partial class'`. Filter out methods (`(`), `=>`-bodied factories, and `const`/`AsyncLocal` by hand. ~10 real hits today.

### Rule D — Gerund-named app-graph properties are a wrong-shape name

A property on `app.X` should name an **object you hold and navigate**, not a state the system is in. Gerunds (`-ing` endings) describe activity; nouns name the thing performing the activity. `app.Building` reads "the system is currently building" — that's a state. `app.Builder` reads "the thing that builds" — that's an object. The latter is OBP-shaped; the former is not.

CLI follows: the flag form is the only form, and it lives on the noun (`--builder`, `--tester`). Folder names follow the property: `App/Builder/`, not `App/Build/`.

Three forms must all agree:

| Form | Today (wrong) | After (correct) |
|------|---------------|-----------------|
| Folder | `App/Build/` | `App/Builder/` |
| App property | `app.Building` | `app.Builder` |
| CLI | `plang build` | `plang --builder` |

**Quick screen**: `grep -rE "(public|internal)\s+\w+ing\b" PLang/App/this.cs` — matches gerund property names on the App spine. Then read each: a state is the rare case; rename otherwise.

### Rule E — Decomposed parameters that should navigate

A method `B.X(spec, modules)` where `modules` is reachable from `B` (or from `B.Modules`, or from `spec.Owner.Modules`, or any other navigation chain rooted at the receiver) is a decomposition smell. The caller is being made to chop its own children off and pass them in; the OBP form is **the callee navigates the receiver for what it needs**.

Worked example from the codebase:
- `Catalog.@this.Build(modules)` called as `Catalog.Build(action.Context.App.Modules)` → caller decomposes its App graph
- After: `app.Modules.Schema.Build()` — instance method on Modules.Schema, navigates `this.Modules` (the parent reference) internally

Two side wins of the navigation form:

1. **Owner is forced explicit** — to navigate, the method has to live where its data lives. Decomposed-parameter methods can live anywhere; navigated methods can only live on a node that *has* the data they need. This makes the OBP smell #4 ("allocate-here / mutate-there") harder to introduce.
2. **API surface stops leaking caller structure** — `Render(spec, modules)` is two parameters wide; `Render(spec)` is one. Renames and refactors of the navigation chain don't change the public method signature.

**Quick screen**: `grep -rnE "\.\w+\(.+\.App\.\w+" PLang/App/ --include='*.cs' | grep -v 'this\.\.\.'`. Surfaces every call site where the caller is reaching into `App.X` to pass it as a parameter. Each is a candidate — verify by reading whether the receiving method could navigate to the same data instead of receiving it.

Refinement: not every parameter is decomposed. A method that takes data the receiver *cannot* navigate to (a fresh value computed by the caller, an opaque token, an unrelated entity) is correctly parameterized. The smell is specifically "parameter is a child of the receiver" or "parameter is reachable through the receiver's parent chain."

## Stage anatomy

Every stage in this plan has the same shape:

1. **The smell it closes.** One sentence — quoting the smell test number from above. If a stage closes more than one smell, it's probably two stages.
2. **The ownership realignment.** "X moves from `A.@this` to `B.@this`," stated plainly. New types named, deletions named.
3. **The new shape.** What `B.@this` looks like after the move. Public surface, private state, lifecycle. Code shape sketches, not full implementation.
4. **Files touched + caller propagation.** The list of files that need to change, with a count of caller sites for any rename or move.
5. **Risk + dependencies.** What could break. Whether earlier stages need to land first. (Most stages have no dependency.)
6. **Tests.** What new tests are needed. What existing tests cover the change. Whether PLang tests are involved or only C#.
7. **Out of scope.** "While I'm here" temptations the stage explicitly refuses.

## Definition of done — per stage

A stage is done when **all** of the following hold:

- `dotnet build PlangConsole` clean.
- `dotnet run --project PLang.Tests` green (no new failures vs trunk).
- `cd Tests && ../PlangConsole/bin/Debug/net10.0/plang --test` green (no new failures or stale entries vs trunk).
- The stage's design doc lists what changed; the doc was honest about out-of-scope items that *didn't* get done.
- The architect updates `summary.md` with a chronological entry.
- The architect updates the stage's row in `plan.md` to `complete`.
- Branch merges to trunk; no leftover commits.

## Definition of done — for the whole plan

The plan is "done" when:

- Every stage marked `complete` or explicitly dropped (with a reason).
- `App.this.cs` is under ~300 lines (currently 681).
- `Modules.this.cs` is under ~200 lines (currently 464).
- `Channels.this.cs` is under ~150 lines (currently 277).
- The two-capital screen, the `Get<Plural>()` screen, the static-field screen, the gerund-property screen, and the decomposed-parameter screen on `PLang/App/` return zero must-fix hits (some unavoidable hits stay; they're documented in `claude-md-proposals.md`).
- `Documentation/v0.2/architecture.md` directory tree matches reality.
- `/shared/app-tree/` is regenerated against the post-cleanup App surface (one-shot, not a stage in itself).

## What this plan refuses to do

- Bundle multiple ownership realignments into one stage.
- Change behaviour while refactoring shape. Behaviour changes get their own branch.
- Edit `CLAUDE.md` files mid-stage. Proposals go in `claude-md-proposals.md` and the docs bot decides.
- Open more than one cleanup stage at a time without merging the previous.
- Refactor inside `App.modules.*` (action handlers) at this layer. Handler-level cleanup is a separate plan if and when needed.

