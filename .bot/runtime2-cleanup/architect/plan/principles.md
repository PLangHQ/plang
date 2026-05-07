# OBP Cleanup — Principles & Stage Anatomy

This is the discipline reference for the cleanup plan. It is the tightest statement of OBP rules the architect now considers settled, plus the checklist every stage runs against.

## OBP smell tests (the eight)

Reproduced from `/CLAUDE.md` so the cleanup work is anchored against the canonical list, with the architect's notes inline. Each item is a yes/no question; any "yes" means the shape is wrong and the fix is structural, not a line edit.

1. **Public mutable collection with rules enforced from outside.** A type exposes `public List<T>` / `Dictionary<K,V>` / `HashSet<T>` and the `Add` / `Remove` / locking / eviction lives in another file. Fix: the collection becomes its own `@this` type with private lock and `Add(...)` / `IReadOnlyList<T>` surface. *Examples in this cleanup: `App._keepAlive`, `App._modules.All` walked by `App.DisposeAsync`.*
2. **Cross-file lock target.** `lock (other.X)` taken from outside `other`'s class. *Example: `Channels.WriteAsync` reaching into `channel.Stream` directly was this; partly fixed in Stage 6.*
3. **Same logical thing stored twice across types.** Overlapping semantics, similar names, same element type, same role. *Examples: the four console channels living in both App.Channels and actor.Channels (fixed); `Serializers` on both App and per-actor Channels (Stage 1 of this plan).*
4. **Allocate-here / mutate-there / clean-up-elsewhere.** One collection's lifecycle split across three files. *Example: `App._keepAlive` allocated in App, mutated by `KeepAlive(x)` / `RemoveKeepAlive(x)`, iterated in `DisposeAsync`. Stage 3.*

If removing one line of choreography requires editing three files, those three files are one missing type.

## The architect-sharpened rules (added 2026-05-07)

These are detection rules that feed the eight smell tests with concrete red flags every reader can spot in seconds.

### Rule A — Compound class names are a red flag

A class named `{Noun}{RolePattern}` is wrong because the role-pattern suffix is behaviour described as a class. The single-noun fix:

- The plural noun *is* the registry (`Channels` IS the channel registry — no `ChannelRegistry`).
- The singular noun *is* the entity (`Actor`, not `ActorEntity`).
- If you find yourself reaching for `Manager`, `Helper`, `Service`, `Handler`, `Loader`, `Holder`, `Wrapper`, `Container`, `Dispatcher`, `Builder`, `Coordinator`, `Controller`, `Mediator` — the type's name is wrong, and the role belongs *to* the noun.

**Quick screen**: `grep -E "class [A-Z][a-z]+[A-Z]"`. Two capital letters in a class name is the red flag. Every hit needs human judgment (some compounds are unavoidable: `MemoryStepCache`, `SqliteSettingsStore` seal implementation variants and may be best handled by per-impl folders) but the screen surfaces the candidates.

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
- The two-capital screen, the `Get<Plural>()` screen, and the static-field screen on `PLang/App/` return zero must-fix hits (some unavoidable hits stay; they're documented in `claude-md-proposals.md`).
- `Documentation/v0.2/architecture.md` directory tree matches reality.
- `/shared/app-tree/` is regenerated against the post-cleanup App surface (one-shot, not a stage in itself).

## What this plan refuses to do

- Bundle multiple ownership realignments into one stage.
- Change behaviour while refactoring shape. Behaviour changes get their own branch.
- Edit `CLAUDE.md` files mid-stage. Proposals go in `claude-md-proposals.md` and the docs bot decides.
- Open more than one cleanup stage at a time without merging the previous.
- Refactor inside `App.modules.*` (action handlers) at this layer. Handler-level cleanup is a separate plan if and when needed.

## What changes between this plan and the channels plan

The channels plan was a feature plan: nine stages of *new* functionality with a clear product goal. This plan is a refactor backlog: thirteen stages of *shape* improvements with no behavioural delta. The two formats differ:

- Channels plan had stage files written upfront because the design needed to settle before any code landed. This plan writes stage files as we approach each one; the upfront design content is in this `plan/principles.md` and in `plan.md`'s stage one-liners.
- Channels stages had inter-dependencies (Stage 4 depended on Stage 1's base abstraction). Cleanup stages are mostly independent — each closes its own smell.
- Channels lived on one branch (`runtime2-channels`). This plan spawns one cleanup branch per stage to keep blast radius small; `runtime2-cleanup` is only the planning home.
