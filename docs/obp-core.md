# OBP Core — Shared Rules for All Bots

The PLang Object-Based Pattern in one document. This is the source of truth referenced by per-bot lens files (`coder.md`, `architect.md`, `codeanalyzer.md`, `tester.md`).

OBP is **the design discipline that says every type owns its data and the rules for that data**. When you see a piece of choreography about a collection in three different files, those three files have one missing type. When you see a `Manager` or `Helper`, the role belongs *to* the noun, not on a separate class. When you see a static field, the data has no owner.

This document covers:

- **The `@this` convention** — how OBP types are named and located in folders.
- **Naming principles** — what makes a name OBP-correct.
- **The four foundational shape rules** — patterns to spot in code.
- **The five sharpened detection rules** — narrower rules with grep-friendly screens.

Bots: read your lens file (`<bot-name>.md` in this folder) for workflow-specific application. The foundational definitions live here.

---

## The `@this` convention

Every folder's primary class is named `@this` in `this.cs`:

```
PLang/App/this.cs              → class @this in namespace App         (the root)
PLang/App/Goals/this.cs        → class @this in namespace App.Goals
PLang/App/Goals/Goal/this.cs   → class @this in namespace App.Goals.Goal
PLang/App/Channels/this.cs     → class @this in namespace App.Channels
```

Rationale: the *namespace path* names the entity (`App.Goals.Goal`); the class name `@this` defers to it. You navigate `app.Goals.Get(name)` returning a `Goal.@this` — the type's identity is its location.

Consumers reference children as `ChildNamespace.@this` from the parent namespace, or via global aliases (`global using Goal = App.Goals.Goal.@this;`) in test code. See `Documentation/v0.2/good_to_know.md` "@this Class Convention" for full details.

---

## Naming principles

**The name IS the contract.** Each property on the object graph should tell you what the object *is*, not what it *does*. You navigate the tree by name and the object takes care of itself.

**Properties are nouns; methods are verbs.** A property describes what the thing IS — `lifecycle.Before` (the before bindings). If something needs to happen *to* it, that's a method on it: `Phase.Load()`. Never use a verb in a property name.

**Structures ARE things.** A `Lifecycle` with `.Before` and `.After` IS a lifecycle. `Bindings` with `.Add()` and `.Run()` IS a collection of bindings. Don't rename to `LifecycleManager` or `BindingDispatcher` — those suffixes describe behavior, not identity.

**Plural is the registry; singular is the entity.** `app.Channels` is the channel registry. `Channel.@this` is one channel. No `ChannelRegistry` class — the plural noun *is* the registry. No `ActorEntity` class — the singular noun *is* the entity.

**Suffixes that almost always indicate a wrong-shape name:** `Manager`, `Helper`, `Service`, `Handler`, `Loader`, `Holder`, `Wrapper`, `Container`, `Dispatcher`, `Builder`, `Coordinator`, `Controller`, `Mediator`. If you reach for one of these, the type's name is wrong — the role belongs *to* the noun.

---

## The four foundational shape rules

These describe shapes you spot reading code. Not greppable as quick screens (with one exception — Rule 2). Internalize them; you'll see them in code review without thinking.

### Rule 1 — Mutable collection with rules enforced from outside

A type exposes a private collection field and the `Add` / `Remove` / locking / iteration discipline lives on the same class — but the discipline could be on a *new type that wraps the collection*. The collection isn't the type; it has no identity of its own.

**Symptom:** private field `_xs`, public methods `Add(...)`, `Remove(...)`, `foreach (var x in _xs) ...` all on the holder class.

**Fix:** the collection becomes its own `@this` type with private lock and `Add(...)` / `IReadOnlyList<T>` surface. Holder keeps a reference, not the choreography.

**Worked example:** `App._keepAlive` (private List with KeepAlive(x) / RemoveKeepAlive(x) / DisposeAsync iteration on App) → `App/KeepAlive/this.cs` (own type, own lock, own DisposeAsync). App holds `KeepAlive.@this`, calls go from `app.KeepAlive(x)` to `app.KeepAlive.Add(x)`.

### Rule 2 — Cross-file lock target

`lock (other.X)` taken from outside `other`'s class. The data lives in `other`; the discipline lives in the caller. Wrong owner.

**Symptom:** any `lock (some.X)` outside the class that owns `X`.

**Fix:** the lock moves into the type that owns the data. Caller doesn't see the lock; it just calls a method.

**Quick screen:** `grep -rEn "lock\s*\(\s*\w+\." PLang/ --include='*.cs' | grep -v "lock\s*\(\s*this\."` — every cross-class lock target is a candidate. Today: zero hits in `PLang/App/` (the channels work cleared this).

### Rule 3 — Same logical thing stored twice across types

Two types each hold a collection that *means the same thing* — overlapping semantics, similar names, same element type, same role. Either there's a hidden parent that should own the canonical store, or one of the two is redundant.

**Symptom:** type-name appears as a member on multiple parent classes when the *element types* and *roles* match. `app.X.Y` and `app.A.B.Y` both expose Y of the same shape.

**Fix:** pick the canonical home. If both have a real reason to exist, one is a domain-specific subtype of the other (`stack.Audit` vs `app.Errors.Trail` — both `IError` lists, but different scopes; promote each to its own *domain-named* type, don't share a generic `ErrorLog` utility).

**Worked example:** `Serializers` on both App and per-actor Channels (same shape, same role). Stage 1 of cleanup makes `app.Serializers` canonical; per-actor `Channels.Serializers` removed.

### Rule 4 — Allocate / mutate / cleanup split across files

One collection's lifecycle is split across multiple files. Allocation in file A, mutation through methods in file B (or A and B), iteration/teardown in file C. The collection has no real owner.

**This is the structural generalization of Rules 1 and 2.** If you can't name the file that owns the lifecycle, it doesn't have one.

**Detection rule of thumb:** *If removing one line of choreography requires editing three files, those three files are one missing type.*

**Fix:** collapse the choreography into a single `@this` that owns allocation, mutation, and teardown.

---

## The five sharpened detection rules

These complement the foundational 4 with grep-friendly quick screens. Each finds specific instances of OBP smells at scale. Audit recipes (the screens, the filters, today's counts) live in `Documentation/v0.2/audit/obp-rules.md`. The principle is here; the recipes are there.

### Rule A — Compound class names are a red flag

`{Noun}{RolePattern}` class names hide a wrong-shape design. The role belongs on the noun. Plural noun = registry; singular noun = entity. See "Naming principles" above.

### Rule B — `Get<Plural>()` is a missing collection type

`GetXs()` returning a list of X tells you there should be an `Xs` `@this` that *is* the list. Refinement: `Get(uniqueKey)` returning one item is fine; the smell is `Get*()` returning a list.

### Rule C — Static fields are a missing `@this`

A `static` field — including `static readonly` to a mutable collection — has no `@this` owner. Hand it to the owning instance. Three exceptions: `const`, `AsyncLocal<T>`, lock objects whose guarded data is itself irreducibly static (empty set today; if you reach for a static lock, the data should move first).

This rule covers **fields, not methods.** Static factory methods, conversion operators, and helpers stay.

### Rule D — Gerund/verb-named app-graph properties and folders

Properties on `app.X` should be nouns naming objects, not gerunds (`-ing`) or verb roots (`Build`, `Run`). Folder names follow. CLI flags follow. Verb commands (`plang p build`) stay verbs.

### Rule E — Decomposed parameters that should navigate

A method that takes a parameter reachable from the receiver is decomposed wrong. The callee should navigate the receiver for what it needs, not require the caller to chop its children off and pass them in. Side wins: forces the method to live where its data lives; stops the API surface from leaking caller structure.

**The value-layer face — leaf actions that decompose their operands.** A leaf action may read its own typed `Data<T>` value, but it must not extract `.Value()` and feed the raw inside to a static/free helper. `number.Round(await Value.Value(), await Decimals.Value())` and `Resize((await A.Value()).Bytes, w, h)` crack the carriers open and decompose the value. The value owns its operations: call the op on the carrier and pass other operands as **whole carriers** — `await Value.Round(Decimals)`, `await A.Add(B)`, `await A.Resize(Width, Height)` — never `Op(a.Value, b.Value)`. The tell: you `await X.Value()` an operand only to hand the raw inside to something else; if you opened the box to pass what was inside, pass the box. Quick screen: `grep -rEn "\.Value\(\)\)?[!]?[,)]" PLang/app/module/ --include='*.cs'` then look for the result feeding a static `Type.Op(...)` call. Full rule: `Documentation/v0.2/object_pattern_formal.md` Rule #9; CLAUDE.md smell #8.

---

## How the rules relate

The foundational 4 are *patterns* (read code, see the shape). The sharpened 5 are *finders* (run a grep, walk the candidates).

**Rule A** finds Rule 1 / Rule 4 patterns at the class-name level (compound names often signal misplaced collections or split lifecycles).

**Rule B** finds Rule 1 patterns at the method-name level (`GetXs()` is the method that should be the collection).

**Rule C** is its own thing — static state has no foundational analog because OBP assumes everything has an owner; static is the absence of one.

**Rule D** finds wrong-shape *naming* directly; doesn't strictly map to a foundational rule, though `app.Building` instead of `app.Builder` often coincides with allocate-in-Foo / mutate-in-Bar / cleanup-in-Baz spread (Rule 4) because verb-named subsystems tend to fragment behavior.

**Rule E** finds Rule 4 patterns at the method-call level (the parameter list reveals split ownership: caller knows about callee's children).

---

## Where to find more

- **`Documentation/v0.2/good_to_know.md`** — original OBP Naming Principle and OBP Smell Checklist with worked examples from the channels work.
- **`Documentation/v0.2/audit/obp-rules.md`** — full grep recipes, filter pipelines, current signal/noise counts on `PLang/App/`.
- **`Documentation/v0.2/architecture.md`** — App architecture overview.
- **`/CLAUDE.md`** — project root, has the OBP Shape Smells in summary form.

---

## Bot-specific lenses

Each bot has a workflow-specific application of these rules. Read your lens file in this folder:

- `coder.md` — when you write a class, here's the shape
- `architect.md` — when you design, here's what to design for
- `codeanalyzer.md` — when you review, here are the smells
- `tester.md` — when you test, here's how OBP shape exposes testability
