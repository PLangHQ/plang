# OBP Smells — the named catalog

The **operational** OBP doc: every violation has a name, and the name is the finding — "this is a *raw hand-off*" is a complete review comment, no lookup needed. Part of a three-doc set, each with one job:

- **Quick list** — project `CLAUDE.md` `## OBP Shape Smells`: the names with one line each, loaded into every bot's context.
- **This doc** — owns the names and the worked examples: what each smell looks like, the tells, the fix.
- **The pattern** — [`object_pattern_formal.md`](object_pattern_formal.md): the 3 laws + the rules + why.

Smells are named, never numbered. Names carry the diagnosis and can't desync across docs; refer to them in italics by name.

---

## Naming — the name is the contract

The rule itself is in the pattern doc ("The name is the contract"); these are the operational tells.

- **Properties and types: one honest noun.** `app.Goal`, `app.Channel`, `app.Cache` — each says what it IS; you navigate there and call methods. Property-shaped knowledge is a property: `Count`, never `GetCount()`.
- **Methods: one verb naming the caller's intent** — `Open`, `Read`, `Write`, `Close`, `Get`. Never the mechanism: `cache.Get(key)`, not `cache.Resolve(key)` — the caller just wants the thing and doesn't care how it's made.
- **Boolean questions: `IsX` / `HasX`** — the only sanctioned compound.
- **Structures ARE things.** A `Lifecycle` with `.Before`/`.After` IS a lifecycle. Don't rename to `Manager`/`Dispatcher`/`Handler` — those describe behavior, not identity.

Everything else compound is the *verb+noun* smell — see the catalog.

---

## The catalog

### Shape — objects & collections

**naked collection** — a bare `List<T>`/`Dictionary<K,V>`/`HashSet<T>` exposed as public state while its discipline (add rules, locking, eviction) is enforced from other files.

```csharp
public List<IError> Audit { get; } = new();     // on type A
// ...elsewhere on type B...
lock (something) { stack.Audit.Add(error); }     // discipline lives outside the owner
```

Fix: the collection becomes its own type under the concept — `error/list/this.cs`, type `error.list.@this` — with the backing list and lock private and `Add(...)` as a method. The owner exposes it as a **singular** property (`callStack.Error`); implementing `IReadOnlyList<T>` keeps the PLang surface (`%log.Count%`, `%log[0].Module%`) working. Bare collections are fine as transient locals, private backing fields, and DTO fields at serialization boundaries.

**middleman** — a parent proxying what it owns: `callStack.AddError(e)` / `GetErrors()` / `ClearErrors()` wrapping a collection the callers should talk to directly. Expose the node (`callStack.Error.Add(e)`); domain operations belong on the collection type, never on the parent.

**cross-file lock** — `lock (other.X)` taken from outside `other`'s class, including an `internal readonly object XLock` exposed only so a sibling can take it. The type that owns the data isn't the type that owns the discipline. Fix: lock goes private inside the owning type; the mutating method encapsulates it.

**stored twice** — the same logical thing held in two types with overlapping semantics (similar names, same element type, same role). If `stack.Audit` and `app.Error.List` are both "run-wide error log," they're one concept in two places. Fix: one `X.list` type under the concept — the concept carries the domain meaning, the collection is `list`; never invent a domain word for the container (`trail`, `ErrorLog`, `Tracker`).

**split lifecycle** — allocate-here / mutate-there / clean-up-elsewhere: file A allocates, B does `.Add`, C does `.Remove` under A's lock. If you read three files to understand one collection's mutation, the collection wants to be a type.

**flat copy** — a class declares `Foo Foo { get; }` *and* scalar fields whose values are all reachable as `Foo.X`, `Foo.Y`, `Foo.Z`. The mirrors cost memory, and they silently drift when `Foo` is rebuilt; every construction site must remember to fill both.

*Worked example:* `app.tester.Test.@this` declared `Goal? Goal` *plus* `Path`, `PrPath`, `EntryGoalName`, `GoalHash`, `BuilderVersion` — every one reachable through `Goal`. Fix: delete the flat fields, route through `file.Goal?.Path`. Keep one *summary* field (e.g. `StatusReason`) only for the `Goal == null` case — a state the reference can't describe. A serialization DTO or deliberate thread-safe snapshot holding flat copies is fine — document the intent so the roles don't merge.

**raw hand-off** — the producer hands back raw; consumers transform identically. `obj.Path.TrimStart('/')`, `obj.Name.ToLowerInvariant()`, `obj.Url.Trim().TrimEnd('/')` at three or more call sites: every fix to the transform now has N sites, and one forgetful consumer is a divergence bug. The discipline (separator, case, trimming, parent-derivation) belongs on the owner.

*Worked example:* `step.Goal?.Path?.ToString().TrimStart('/')` paired with `test.Path.TrimStart('/')` across `modules/test/run.cs` and `modules/cache/wrap.cs`. The leading slash comes from `.pr` deserialization; fix it once at the producer (`Goal.RelativePath`) and both sites collapse. Grep: `\.{PropertyName}\.(TrimStart|TrimEnd|ToLower|ToUpper|Replace|GetDirectoryName|Substring|Split)` — three or more hits means the property is shaped wrong. When the raw form IS the point, keep both (`Goal.Path` raw + `Goal.RelativePath` trimmed) — no transform repeats at call sites.

**stray helper** — a helper that takes a domain object and returns a derived answer: `ComputeAbsolute(path)`, `CheckPermission(absolute, verb)`, `RenderName(user)`. The domain object owns its own questions; `Helper.X(thing)` almost always wants to be `thing.X()`. The helper is the missing method on the type.

```csharp
// Smelly — transaction script dressed as OBP: body wires helper outputs into helper inputs
var absolute = ResolveAbsolute(path);
var check = CheckOrRequest(absolute, Verb.Read);

// Self-owning — the path owns its own questions
var check = path.CheckPermission(Verb.Read);
```

Litmus: count private static helpers in the calling class. Each one is a suspect — a method that didn't make it onto the right type. (Note the boundary with "a method holds its own logic": inlining beats extracting when there's no second caller; when there IS shared logic, it goes onto the owning type, not into a static helper.)

### Value layer

**broken seal** — a courier reads `Data.Value` mid-flight. A relay layer (a handler that forwards Data, variable memory, callstack, channel routing, signing, the wire envelope) does `data.Value as X` or `if (data.Value is X)` to branch on the contained value — opening a package that should stay closed in transit. Only leaves open it: handlers that declared the typed slot (`Data<image> A`), and the value's own per-format serializer. Grep: `\.Value (is|as|switch)` outside files that declare `Data<T>` parameters.

**opened box** — a leaf cracks carriers into primitives to feed a helper. A leaf may read its own typed value; it must not chop it (or its operands) into raw fields for a static op.

```csharp
// WRONG — cracks both carriers open, hands raw values to a static op
var n = number.FromObject(await Value.Value());
return number.Round(n, (await Decimals.Value())!);

// RIGHT — the number rounds itself; the other operand rides whole
return await Value.Round(Decimals);
```

The tell: you `await X.Value()` an operand only to pass the raw inside to something else. If you opened the box to pass what was inside, pass the box.

**clr leak** — lowering to CLR (`.Clr`) anywhere except a real boundary (.NET/3rd-party API, sqlite, STJ). Work in plang types end to end; high `.Clr` density means the design is CLR-centric and wrong at the root.

**late stamp** — construct-then-stamp: `new X(...) { Context = ... }` or `Context ??=` instead of born-with-context. A context-less instance exists for a window, and in that window it mis-types, can't navigate, and forces null checks on everyone downstream. Context is a private non-nullable field set at construction.

### Design alarms

**fork** — two execution paths for one operation. Four shapes: a behavioral `if`/`switch` (choosing *how* to do the same thing — reading distinct fields of a structured object is not a fork); a generic/fallback/"default" path beside per-type handlers; a type-switch inside a registry (`is X.subtype` → behave differently — push it onto the element as a virtual member); an optional-override branch (`is INamedThing ? declared : derived` — two ways to get one thing). Forks are where code diverges over time; the pattern-doc rule behind this is "never diverge."

**verb+noun** — the flashing sign. `BuildTypeEntries`, `GetParameters`, `CoerceToKind`, `ErrorCategory` — any compound name where one half is a verb (only `IsX`/`HasX` booleans are exempt). Never allowed, and always diagnostic of the design underneath: `CoerceToKind` sat on the type object doing kind's job — the name was flagged before a line of the body was read, and poking at it uncovered the misplaced responsibility. `BuildTypeEntries` was a verb proxying a collection the registry should simply BE. Needing to proxy a collection is wrong design; a name not matching the work is wrong design; the compound is how it surfaces in the API.

---

## The meta-test

**If removing one line of choreography requires editing three files, those three files are one missing type.** And coincidental duplication that vanishes when the shape is right — don't extract it; fix the shape first.

---

## Variant design — folder per concept, file per variant

When one concept takes several mutually-distinct shapes, each carrying its own configuration (a filesystem `Verb` that is Read *or* Write *or* Delete, each with its own knobs), the temptation from other languages is a flags enum plus an option bag:

```csharp
// ANTI-PATTERN
[Flags] public enum Verb { None=0, Read=1, Write=2, Delete=4 }
public record Permission(string AppId, string Path, Verb Verbs,
    VerbRead? Read, VerbWrite? Write, VerbDelete? Delete);
```

Three things wrong: the variants have **no owner of their own**; the enum and payloads can **disagree** (`Read|Write` with `Write == null` is representable nonsense); serialization is **two-pass** (an LLM sees `"Verbs": 3` and has nothing to chew on).

**Correct shape: a folder per concept (singular), a file per variant, always-present records with sub-option booleans defaulting to true, each owner answering its own coverage question.**

```
permission/
  this.cs              -- @this manager + the Permission record
  verb/
    this.cs            -- Verb @this: composes Read/Write/Delete
    Read.cs            -- record Read(bool Recursive = true, bool Metadata = true)
    Write.cs           -- record Write(bool Create = true, bool Overwrite = true, ...)
    Delete.cs          -- record Delete(bool Recursive = true, bool Permanent = true)
```

```csharp
public class @this   // Verb
{
    public Read   Read   { get; init; } = new Read();
    public Write  Write  { get; init; } = new Write();
    public Delete Delete { get; init; } = new Delete();
    public bool Allows(@this r) => Read.Allows(r.Read) && Write.Allows(r.Write) && Delete.Allows(r.Delete);
}

public record Read(bool Recursive = true, bool Metadata = true)
{
    public bool Allows(Read r) => (!r.Recursive || Recursive) && (!r.Metadata || Metadata);
}
```

The rule reads as *"if the request needs feature X, the grant must have X."* The manager **composes** (`permission.HasAccess(...)`, `variant.Allows(...)`); it never reaches into a record to apply matching from outside.

**The rules, tightly:**

1. **Folders are singular** — `permission/`, not `permissions/`.
2. **A concept with N configurable variants is one folder.** Each variant is one file owning its record (sensible defaults) *and* its own `Allows(other)`.
3. **Variants are always-present, non-nullable properties on the parent `@this`.** Narrowing is a record copy with explicit `false`; never a flag enum with parallel option records, never nullable variants as granted/not-granted signaling.
4. **Managers compose, they don't implement.**
5. **Methods take whole domain objects, not pre-decomposed primitives** — `HasAccess(Path, …)` not `HasAccess(string absolutePath, …)`. The receiver decides which field it needs.

> **Current home:** this pattern is live at **`app.type.path.permission`** — `permission/this.cs` plus `permission/verb/{Read,Write,Delete,Execute}.cs` (a fourth verb, `Execute`, alongside the three shown). The sub-option booleans shown are illustrative of the shape, not the exact current fields, and the coverage methods in code still carry the earlier name `Covers` — the rename to `Allows` (caller-intent verb) is on the cleanup backlog.

---

## Drift tells

You're drifting out of the pattern when: you reach for a `*Helper`/`*Manager`/`*Service` class; you write `lock (other.X)` from outside `other`; you copy a property and apply the same transform at several call sites; you decompose a domain object into primitive parameters at the call site. Any of these → stop and re-anchor: behavior lives on the owner, the collection is the API, names are single and honest.
