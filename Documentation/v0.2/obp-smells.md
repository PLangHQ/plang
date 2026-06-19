# OBP Smells & Worked Examples — the audit reference

The **operational** OBP doc: the worked right/wrong examples and naming tells you check your own diff against. Part of a three-tier set, each with one job:

- **Quick checklist** — project `CLAUDE.md` `## OBP Shape Smells`. The terse numbered list, loaded into every bot's context. Run it yes/no on any diff.
- **This doc** — the examples behind that list: what each smell *looks* like, the naming tells, variant design. On demand.
- **The law** — `object_pattern_formal.md`. The philosophy + the 9 rules + why. On demand.

If the terse list and this doc ever disagree on wording, the CLAUDE.md list is the canonical statement; this doc owns the examples.

---

## Naming — the name is the contract

In OBP **the name IS the contract**. Each property tells you what the object *is*, not what it *does*; you navigate the tree by name and the object takes care of itself. (The rule itself is `object_pattern_formal.md` Rule #3 — this section is the operational tells.)

- **Good names describe the thing:** `app.Goal`, `app.Channel`, `app.FileSystem`, `app.Channel.Serializers` — each says what it manages; you navigate there and call methods. (Property access on `app.@this` stays PascalCase and singular; the *types* are lowercase singular.)
- **Bad names describe a verb or are too broad:** `IO` is a verb disguised as a noun — it says what it vaguely *does*, not what it *is* (a channel manager). Broad names breed confusion ("filesystem is I/O too, shouldn't it live here?"). Fix: name it what it is and the responsibilities become obvious.
- **Structures ARE things.** A `Lifecycle` with `.Before`/`.After` IS a lifecycle; `Bindings` with `.Add()`/`.Run()` IS a collection of bindings. Don't rename to `Manager`/`Dispatcher`/`Handler` — those describe behavior, not identity.
- **Properties are nouns, methods are verbs.** Never put a verb in a property name. `lifecycle.Before` (the before-bindings, a thing), not `lifecycle.Load` (an action). If it needs loading, that's `Phase.Load()`.

### The naming tell — verb+noun method names

A method named as a **verb+noun compound** — `BuildTypeEntries`, `GetValidValues`, `GetBuilderTypeNames` — is almost always a smell. Two failure modes:

- **The noun restates the class → you're proxying a collection.** `BuildTypeEntries` on the type list (`app.type.list.@this`) — which already *is* the list of type entries — is a verb handing back a collection the registry should simply *be*. OBP: "Collections are the API; expose the collection, don't proxy it with `Build`/`Get`/`Add`."

  ```csharp
  // WRONG — verb proxies a collection; "TypeEntries" restates the class
  public List<type.@this> BuildTypeEntries(module.@this? modules) { ... }
  var all = app.Type.BuildTypeEntries(null);
  ```

  The right shape: **the list owns its entries and returns itself; a derived view is its own named member that owns its shaping** — `app.type.entry.list.catalog`, a `catalog` property that knows what to give for "catalog"; an old module-scoped projection becomes *another* named view, not a parameter. A property returns exactly what its name says and does only that work — `Entries` returns entries, `Catalog` returns the catalog. **Name matches work.**

  > Cautionary note: a first-draft "fix" — `Entries => _catalog.Value` — is *also* wrong: it names the property for the raw collection while doing catalog-assembly behind it (work over modules/schemas that aren't the list's own). The smell isn't just the verb; it's the name not matching the work.

- **A `Get`-prefixed twin beside a noun** (`ValidValues` + `GetValidValues`, `BuilderNames` + `GetBuilderTypeNames`) is the same thing exposed twice — smell #4 below. Keep the noun, drop the `Get`-double.

**Rule of thumb: if you can't name it in one clean word, the shape is probably wrong.** Reaching for a verb+noun is the signal to stop and ask — does this behavior belong on the owner? Should the collection be the API? Am I building a middleman?

**Verbs are fine when they do real work.** `HasAccess`, `Covers`, `Resolve`, `Open`, `Read` — all valid; that's how English describes work and the names read like prose. The smell is `GetX`/`IsX` *property-shaped questions* dressed as methods, not verbs in general.

---

## The shape smells — with worked examples

Run this on any folder that owns state (it's the example-bearing version of CLAUDE.md's list). A "yes" on any item means an OBP type is missing and the fix is structural, not a line edit.

**1. Primitive collection exposed publicly while its mutation discipline lives elsewhere.** `public List<T>`/`Dictionary<K,V>`/`HashSet<T>` whose lock/eviction/snapshot-iteration discipline is enforced from another file.

```csharp
public List<IError> Audit { get; } = new();   // on type A
// ...elsewhere on type B...
lock (something) { stack.Audit.Add(error); }   // discipline lives outside the owner
```

Fix: the collection becomes its own type with the lock private and `Add(...)` as a method. The PLang surface (`%log.Count%`, `%log[0].Module%`) keeps working when the type implements `IReadOnlyList<T>`.

**2. `internal readonly object XLock` exposed only so a sibling can take the lock from outside.** `lock (caller.ChildrenLock)` followed by `caller.Children.Add(call)` is two halves of one responsibility split across files. Fix: lock goes private inside the X type; `Children.Add(call)` encapsulates it.

**3. Cross-file mutation choreography.** File A allocates, B does `.Add`, C does `.Remove` under A's lock. If you read three files to understand one collection's mutation, the collection wants to be a type.

**4. Two collections with overlapping semantics in different parents.** If `stack.Audit` and `app.Errors.All` are both "run-wide IError log", they're one concept in two places. Each gets its own *domain-named* type (avoid `ErrorLog`/`Tracker`/`Manager` — structural names, not domain identities; the folder name is the type name, pick a domain word).

**5. Helper that takes a domain object and returns a derived answer.** `ComputeAbsolute(path)`, `CheckPermission(absolute, verb)`, `RenderName(user)` — the domain object owns its own questions. `Helper.X(thing)` almost always wants to be `thing.X()`. The helper is the missing method on the type.

*Worked example — the leaf that decomposes its own operands.* A leaf action may read its own typed value, but it must not chop the value (or its operands) into primitives for a static helper. `math.round` did exactly that:

```csharp
// WRONG — cracks both carriers open, hands raw values to a static op
public async Task<data.@this<number>> Run() {
    var n = number.FromObject(await Value.Value());          // re-lift (the slot should be Data<number>)
    if (n == null) return ...ValidationError("requires a number");  // re-validate what the typed slot guarantees
    return number.Round(n, (await Decimals.Value())!);       // static Round(value, decimals); operand decomposed
}

// RIGHT — type the operand; the number rounds itself; the other operand rides whole
public async Task<data.@this<number>> Run() => await Value.Round(Decimals);
```

The whole `math/*` module had this shape (`number.Add(await A.Value(), await B.Value(), policy)` etc.) — every action decomposed both operand carriers and called a static `number.Op(a, b)`. The fix is `await A.Add(B, …)`: the value owns the verb, operands pass as whole `Data` carriers. The tell: you `await X.Value()` an operand only to feed the raw inside to something else. If you opened the box to pass what was inside, pass the box. (This is the value-layer face of `object_pattern_formal.md` Rule #9 and CLAUDE.md smell #8.)

**6. Producer hands back raw; consumers transform identically.** Same property, same suffix/prefix/case-fold/slice at three or more call sites — the discipline belongs on the owner.

*Worked example:* `step.Goal?.Path?.ToString().TrimStart('/')` paired with `test.Path.TrimStart('/')` across `modules/test/run.cs`, `modules/cache/wrap.cs`. The leading slash comes from `.pr` deserialization; fix it once at the producer (`Goal.RelativePath` returning the trimmed form) and both sites collapse. Grep `\.Path\.TrimStart\(`.

*When the raw form IS the point:* keep both — `Goal.Path` (raw, source of truth) + `Goal.RelativePath` (trimmed). No transform repeats at call sites.

**7. Holds a reference AND a flat copy of properties reachable through it.** A class with `Foo Foo` plus N scalar fields all reachable as `Foo.X` pays double — memory + drift risk.

*Worked example:* `app.tester.Test.@this` declared `Goal? Goal` *plus* `Path`, `PrPath`, `EntryGoalName`, `GoalHash`, `BuilderVersion` — every one reachable through `Goal`. Fix: delete the flat fields, route through `file.Goal?.Path`. Keep one *summary* field (e.g. `StatusReason`) only for the `Goal == null` case (.pr missing/corrupt) — a state the reference can't describe.

*When the class IS a value-snapshot on purpose:* a serialization DTO or thread-safe snapshot holding flat copies is fine — document the intent in XML doc so the two roles don't merge.

### Helper-soup vs. self-owning methods

```csharp
// Smelly — transaction script dressed as OBP: body wires helper outputs into helper inputs
public async Task<Data<string>> ReadText(Path path) {
    var absolute = ResolveAbsolute(path);
    var check = CheckOrRequest(absolute, Verb.Read);
    if (check is { } request) return request;
    return Data.Ok(await File.ReadAllTextAsync(absolute));
}

// Self-owning — Path owns its own questions; the method does only what only it can
public async Task<Data<string>> ReadText(Path path) {
    var check = path.CheckPermission(Verb.Read);
    if (!check.Success) return check;
    return Data.Ok(await File.ReadAllTextAsync(path.Absolute));
}
```

Litmus: count private static helpers in the calling class. Each one is suspicious — a method that didn't make it onto the right type.

---

## Variant design — folder per concept, file per variant

When one concept takes several mutually-distinct shapes, each carrying its own configuration (a filesystem `Verb` that is Read *or* Write *or* Delete, each with its own knobs), the temptation from other languages is a flags enum plus an option bag:

```csharp
// ANTI-PATTERN
[Flags] public enum Verb { None=0, Read=1, Write=2, Delete=4 }
public record Permission(string Subject, string Resource, Verb Verbs,
    VerbRead? Read, VerbWrite? Write, VerbDelete? Delete);
```

Three things wrong: the variants have **no owner of their own**; the enum and payloads can **disagree** (`Read|Write` with `Write == null` is representable nonsense); serialization is **two-pass** (an LLM sees `"Verbs": 3` and has nothing to chew on).

**Correct shape: a folder per concept (singular), a file per variant, always-present records with sub-option booleans defaulting to true, each owner doing its own coverage check.**

```
Permission/
  this.cs              -- @this manager + the Permission record
  Verb/
    this.cs            -- Verb @this: composes Read/Write/Delete coverage
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
    public bool Covers(@this r) => Read.Covers(r.Read) && Write.Covers(r.Write) && Delete.Covers(r.Delete);
}

public record Read(bool Recursive = true, bool Metadata = true)
{
    public bool Covers(Read r) => (!r.Recursive || Recursive) && (!r.Metadata || Metadata);
}
```

The coverage rule reads as *"if the request needs feature X, the grant must have X."* The manager **composes** (`record.HasAccess(...)`, `variant.Covers(...)`); it never reaches into a record to apply matching from outside.

**The rules, tightly:**

1. **Folders are singular** — `Permission/`, not `Permissions/`.
2. **A concept with N configurable variants is one folder.** Each variant is one file owning its record (sensible defaults) *and* its own `Covers(other)`.
3. **Variants are always-present, non-nullable properties on the parent `@this`.** Narrowing is a record copy with explicit `false`; never a flag enum with parallel option records, never nullable variants as granted/not-granted signaling.
4. **Managers compose, they don't implement.**
5. **Methods take whole domain objects, not pre-decomposed primitives** — `HasAccess(Path, …)` not `HasAccess(string absolutePath, …)`. The receiver decides which field it needs; pre-decomposing leaks its preference into the call site.
6. **Verb-named methods are fine when they do real work** — `HasAccess`, `Covers`, `Resolve`, `Open`. The `GetX`/`IsX` smell is property-shaped questions dressed as methods.

> **Current home:** this pattern is live at **`app.type.path.permission`** — `permission/this.cs` plus `permission/verb/{Read,Write,Delete,Execute}.cs` (note: a fourth verb, `Execute`, alongside the three shown). The folder shape above matches; the namespace root in the older `App.FileSystem.Permission` examples is pre-`filesystem→path`-merge and should read `app.type.path.permission`. The sub-option booleans shown (`Recursive`/`Metadata`/…) are illustrative of the shape, not the exact current fields.

---

## When shape #1 is the right answer

A bare `List<T>`/`Dictionary<K,V>` is fine when it's a **transient local**, a **DTO at a serialization boundary**, or a **value-snapshot** explicitly detached from the live graph (documented as such). The smells above are about *owned, long-lived state with discipline* — not every collection is a missing type.

**Tells you're drifting back into shape #1:** you reach for a `*Helper`/`*Manager`/`*Service` class; you write `lock (other.X)` from outside `other`; you copy a property and apply the same transform at several call sites; you decompose a domain object into primitive parameters at the call site. Any of these → stop and re-anchor: behavior lives on the owner, the collection is the API, names are single and honest.
