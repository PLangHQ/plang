# Good to Know — App Architecture Notes

Collected architectural insights from building and debugging PLang App.

---

## Folder Structure & Namespaces

### `@this` Class Convention
Every folder's primary class is named `@this` in `this.cs`. Consumers use global using aliases:
- `app/this.cs` → `class @this` (no global alias — namespace shadows it)
- `app/goals/this.cs` → `class @this` (alias: `EngineGoals`)
- `app/goals/goal/this.cs` → `class @this` (alias: `Goal` in tests, per-file in PLang)
- `app/goals/goal/steps/step/actions/action/this.cs` → `class @this` (per-file alias only — `System.Action` conflict)

### Namespace Per Folder
Each folder gets its **own namespace** matching its path exactly:
- `Goals/Goal/this.cs` → namespace `app.goals.goal`
- `Goals/Goal/Steps/Step/this.cs` → namespace `app.goals.goal.steps.step`
- `Events/Lifecycle/Bindings/this.cs` → namespace `app.events.lifecycle.bindings`

This works because the class is `@this` — it never collides with its namespace segment.

### `ChildNamespace.@this` Pattern
From within a parent namespace, reference a child's primary class as `ChildNamespace.@this`:
- From `app.goals`: `Goal.@this` (the Goal entity class)
- From `app.channels`: `Channel.@this`, `Serializers.@this`
- From `app.*`: `app.@this` (the app root class)

This works because C# resolves child namespace segments before using aliases.

### Global Using Aliases
`PLang/app/GlobalUsings.cs` provides aliases for types without naming conflicts.

**Can't be global** (shadowed or conflicting):
- `App` — namespace `app.app` shadows it from all `app.*` files
- `CallStack` — v1 `PLang.Runtime.CallStack` conflict
- `Goal`, `Visibility` — v1 `Building.Model` conflict
- `Action` — `System.Action` conflict
- `EventType`, `EventBinding` — v1 `PLang.Events` conflict

### PLang.Tests Has Extra Aliases
`PLang.Tests/GlobalUsings.cs` includes additional aliases (App, Goal, ErrorOrder, CallStack, etc.)
because there are no Building.Model or v1 Runtime references in the test project.

---

## Goal Resolution & Relative Paths

### App Root
The app's file system root is the top-level directory (e.g., `Tests/App/` or the app folder). The PLang app is only aware of its own file system — `/` means app root, not OS root.

### Goal.FolderPath
Every goal has a `FolderPath` derived from its `Path` property:
- `\Cache\Start.goal` → `/Cache/`
- `\Variables\Variables.test.goal` → `/Variables/`
- `\Start.goal` → `/`

FolderPath always starts with `/` (relative to app root) and ends with `/`.

### Relative vs Absolute Goal Calls
When a goal calls another goal by name:
- **Relative** (`call ReadCached`) — resolves relative to the calling goal's `FolderPath`. A goal in `/Cache/` calling `ReadCached` looks for `/Cache/.build/readcached.pr` first, then falls back to root `/.build/readcached.pr`.
- **Absolute** (`call /ReadCached`) — the leading `/` means resolve from app root: `/.build/readcached.pr`.

### Lazy Loading
Goals are loaded on demand. `Goals.GetAsync` only loads a `.pr` file when a goal is first requested and not already cached. Never preload all `.pr` files in a directory — load them when needed.

### Multi-Goal Files
A `.goal` file can define multiple goals (Start + sub-goals). The builder creates a separate `.pr` file per goal, named after the goal (e.g., `start.pr`, `innertest.pr`). If two `.goal` files in the same directory both define a goal named `Start`, their `.pr` files collide. Keep sub-goals in separate `.goal` files to avoid this.

---

## Event Override (skipAction)

`event.skipAction` sets `context.EventOverride` to override an action's result. This override is only consumed by action-level event bindings (`BeforeAction`/`AfterAction`). Step-level and goal-level events must NOT consume it, or the override gets eaten before the action handler can see it.

---

## Test Architecture

### Test Isolation
Each `*.test.goal` gets a fresh app instance. This prevents events, variables, and goal caches from leaking between tests. The fresh app shares the same root directory as the original app.

### Builder Caching
The builder uses a content hash to skip rebuilding unchanged `.goal` files. If a `.pr` file has incorrect data but the `.goal` hash matches, the builder will approve the existing (broken) `.pr`. To force regeneration, delete the `.pr` file and rebuild.

### Test Goal Names
Test goals (`*.test.goal`) must have their goal named `Start` — the test runner looks for a goal called "Start" in each `.test.pr` file. If the goal has a different name, the test runner reports "Goal 'Start' not found".

---

## Mock Module Architecture

The mock module (`mock.intercept`, `mock.verify`, `mock.reset`) provides test isolation by intercepting module action calls at the event level.

### How It Works
`mock.intercept` registers a `BeforeAction` event binding for the specified action pattern. The binding's handler:
1. Captures call parameters into a `Mock.@this.Calls` list
2. If `ReturnValue` is set: sets `context.EventOverride` to skip the real action
3. If `GoalToCall` is set: runs the goal (which can use `event.skipAction`)
4. If neither: spy mode — tracks calls but lets the real action run

### `Mock.@this` (the returned handle)
`mock.intercept` returns `Data<app.mock.Mock.@this>`. The handle's properties are reachable via PLang variable resolution:
- `%mock.CallCount%` — number of times the mock was called
- `%mock.Calls[0].Parameters.path%` — first call's path parameter
- `%mock.Pattern%` — the action pattern being mocked
- `%mock.IsSpy%` — true if no ReturnValue or GoalToCall was set

(Previously named `MockHandle`; renamed to `app.mock.Mock.@this` on the `typed-action-returns` branch. The PLang catalog name still derives to `"mock"` via the @this convention — no PLang-side rename.)

### Builder Naming Gotcha
The handler is named `intercept` (not `action`) because the LLM builder confuses `mock.action` with `mock.mock` — it treats "mock" as both module and action name. Using `mock.intercept` avoids this ambiguity.

### Parameter Matching
Uses regex-based matching: standalone `*` becomes `.*`, regex-like patterns are used as-is, plain strings are exact-matched. Matching is case-insensitive.

---

## OBP Naming Principle

In OBP, **the name IS the contract**. Each property on the object graph should tell you what the object *is*, not what it *does*. You navigate the tree by name and the object takes care of itself.

Good names describe the thing: `app.Goals`, `app.Libraries`, `app.FileSystem`, `app.Channels`, `app.Channels.Serializers`. Each tells you what it manages — you navigate there and call methods.

Bad names describe a verb or are too broad: `IO` is a verb disguised as a noun. It doesn't tell you what the object *is* (a channel manager), only what it vaguely *does* (input/output). Broad names cause confusion — "filesystem is I/O too, shouldn't it be here?" The fix: name it what it is (`Channels`), and the responsibilities become obvious.

**Structures ARE things.** A `Lifecycle` with `.Before` and `.After` IS a lifecycle. `Bindings` with `.Add()` and `.Run()` IS a collection of bindings. Name structures after what they are, not what they do. Don't rename to "Manager", "Dispatcher", or "Handler" — those describe behavior, not identity.

**Properties are nouns, methods are verbs.** Never use a verb (sagnorð) in a property name. A property describes what the thing IS — it's just a structure sitting there. If something needs to happen to it, that's a method on it. Example: `lifecycle.Before` (noun — the before bindings), not `lifecycle.Load` (verb — loading is an action, not a thing). If it needs loading, call a method: `Phase.Load()`.

**Agreed target naming for events:**
- `GoalStepEvents` / `ActionEvents` → `Lifecycle` (same type for all entities)
- `EventList` → `Bindings`
- Navigation: `goal.Lifecycle.Before.Run(context)`, `step.Lifecycle.After.Run(context)`

---

## OBP Smell Checklist — When a Collection Should Be Its Own Type

Code reviewers (codeanalyzer, security, manual) should run this scan on any folder that owns state. A "yes" on any of these means an OBP type is missing.

**1. Primitive collection (`List<T>`, `Dictionary<K,V>`, `HashSet<T>`) exposed publicly while its mutation discipline (lock, eviction, lazy-alloc, snapshot-iteration) lives elsewhere.** The collection has rules; the type that owns the rules should own the collection. Smell looks like:
```csharp
public List<IError> Audit { get; } = new();   // on type A
// ...elsewhere on type B...
lock (something) { stack.Audit.Add(error); }  // discipline lives outside the owner
```
Fix: the collection becomes its own type with the lock private and `Add(...)` as a method. PLang surface (`%log.Count%`, `%log[0].Module%`) keeps working when the type implements `IReadOnlyList<T>` (the navigator was extended to recognize it).

**2. `internal readonly object XLock` exposed only so a sibling type can take the lock from outside.** Telltale that X should be its own type — `lock (caller.ChildrenLock)` followed by `caller.Children.Add(call)` is two cooperating pieces of one responsibility, split across files. Fix: move the lock private inside the X type and let `Children.Add(call)` encapsulate it.

**3. Cross-file mutation choreography.** File A allocates, file B does `.Add`, file C does `.Remove` under a lock from file A. If you have to read three files to understand how one collection is mutated, the collection wants to be a type. Fix: same as #2 — collapse the choreography into the collection's type.

**4. Two collections with overlapping semantics in different parent types.** If `stack.Audit` and `app.Errors.All` are both "run-wide IError log", they are one concept used in two places — even if the SCOPES differ (run-wide observed vs run-wide pushed-via-handler). Either one type used twice (with domain-specific wrappers) or — preferred — each gets its own *domain-named* type that happens to share a small implementation pattern. Avoid generic shared utility names like `ErrorLog`, `Tracker`, `Manager` — those are structural names, not domain identities. The folder name is the type name; pick a domain word.

**5. Helper that takes a domain object and returns a derived answer.** A free function (private, static, or external) takes `Thing` and returns some piece of its logic — `ComputeAbsolute(path)`, `CheckPermission(absolute, verb)`, `RenderName(user)`. The domain object owns its own questions; if you find yourself writing `Helper.X(thing)`, ask whether it should be `thing.X()`. Almost always yes. The helper is the missing method on the type.

**6. Producer hands back raw; consumers transform identically.** Same property, same suffix/prefix/case-fold/slice repeated at three or more call sites — the discipline belongs on the owner.

*Worked example (this branch):* `test/run.cs` had `step.Goal?.Path?.ToString().TrimStart('/')` paired with `test.Path.TrimStart('/')`. The leading slash comes from `.pr` deserialization — fixing it at the producer (`Goal.RelativePath` returning the trimmed form, computed once) would collapse both call sites and prevent the next consumer from forgetting the trim. The grep pattern `\.Path\.TrimStart\(` lights up across `modules/test/run.cs`, `modules/cache/wrap.cs`, etc. when this is wrong.

*When the property IS the raw form on purpose:* keep both. `Goal.Path` (raw, source of truth) plus `Goal.RelativePath` (trimmed) is fine — consumers pick the one that matches intent and no transform is repeated at call sites.

**7. Holds a reference AND a flat copy of properties reachable through it.** A class with `Foo Foo` and N scalar fields all reachable through `Foo` is paying double — once in memory, once in drift risk.

*Worked example (`tester-cleanup` branch):* `app.tester.File` (since renamed to `app.tester.Test.@this`) declared `Goal? Goal` *plus* `Path`, `PrPath`, `EntryGoalName`, `GoalHash`, `BuilderVersion`. Every one reachable through `Goal` when `Goal != null`. The flat copy was paid *for every discovered test file* — and silently staled if anyone rebuilt the Goal in place. Fix: delete the flat fields, route consumers through `file.Goal?.Path` etc. Keep one summary field (e.g. `StatusReason`) for the case where `Goal` is null (.pr missing / corrupt) — that's the legitimate carve-out, because it describes a state the reference can't.

*When the class IS a value-snapshot on purpose:* a serialization DTO or a thread-safe-snapshot record holding flat copies is fine — the point of the type is to be detached from the live graph. Document the intent in the class XML doc ("snapshot of Foo at time T; not refreshed when Foo changes") so future readers don't merge the two roles.

### Worked example — Helper-soup vs. self-owning methods

Smelly:

    public async Task<Data<string>> ReadText(Path path) {
        var absolute = ResolveAbsolute(path);
        var check = CheckOrRequest(absolute, Verb.Read);
        if (check is { } request) return request;
        return Data.Ok(await File.ReadAllTextAsync(absolute));
    }

`ResolveAbsolute` and `CheckOrRequest` are helpers taking Path's data and producing answers. The method body is wiring outputs from one helper into the next — a transaction script dressed as OBP.

Self-owning:

    public async Task<Data<string>> ReadText(Path path) {
        var check = path.CheckPermission(Verb.Read);
        if (!check.Success) return check;
        return Data.Ok(await File.ReadAllTextAsync(path.Absolute));
    }

`Path.Absolute` and `Path.CheckPermission(Verb)` are methods on Path — it owns those questions. The FS method only does what only it can do: the actual IO via the BCL. Two delegations, no helper wiring.

Litmus test: count private static helpers in the calling class. Each one is suspicious — it's a method that didn't make it onto the right type.

**Worked example (collections — this branch):** `stack.Audit`, `app.Errors.All`, `call.Errors`, `call.Children`, `call.Diffs`, `call.Tags` were all `List<T>` / `Dictionary<K,V>` exposed publicly with locks scattered around CallStack.Push, Call.DisposeAsync, Tag(), and the OnSet handler. All six were promoted to their own types: `Audit`, `Trail` (was `All`), `Errors` (per-call), `Children`, `Diffs`, `Tags` — each `@this` in its own namespace folder. Each owns its lock, its eviction policy (Children FIFO), and its snapshot-iteration semantics. The Diffs and Tags promotions specifically closed the *reader race* that survived the writer-lock fix — a `lock (_diffsLock) { Diffs!.Add(...) }` writer pattern was safe for the writer but a debug observer iterating the public `List<Diff>?` could still throw `InvalidOperationException` mid-Add. Promoting both moved iteration under the same lock as Add (snapshot-then-yield), closing the race symmetrically.

**Tags / Dictionary case** is the same shape as List, with two extra notes: (a) the type implements `IDictionary<string,V>` (not just `IReadOnlyDictionary<string,V>`) because PLang's DictionaryNavigator probes for the writable interface; mutation methods other than the type's own `Set` throw to enforce the lock-internal discipline. (b) Always-allocate the wrapper at construction — lazy alloc would re-introduce the race on the property setter, defeating the point. Cost is one dict alloc per Call (small).

---

## OBP Variant Design — When a Concept Has Multiple Configurable Modes

When you have a single concept that can take several mutually-distinct shapes, each carrying its own configuration (e.g. a filesystem `Verb` that is Read *or* Write *or* Delete, each with its own boolean knobs), the temptation from other languages is a flags enum plus an option bag:

```csharp
// ANTI-PATTERN
[Flags] public enum Verb { None=0, Read=1, Write=2, Delete=4 }

public record Permission(
    string Subject,
    string Resource,
    Verb Verbs,                            // bitfield says which
    VerbRead? Read,                        // anonymous payloads alongside
    VerbWrite? Write,
    VerbDelete? Delete);

public record VerbRead(bool Recursive, bool Metadata);
public record VerbWrite(bool Create, bool Overwrite, bool Append, bool Mkdir);
public record VerbDelete(bool Recursive, bool Permanent);
```

Three things are wrong with this shape:

1. **The variants have no owner of their own.** `VerbRead` lives wherever it's declared, but no folder *owns* the question "what is Read?". The flag enum implies one thing, the payload records imply another, and the truth is reconstructed by the consumer every time.
2. **The enum and payloads can disagree.** `Verbs = Read | Write` with `Write == null` is a representable nonsense state. The compiler can't catch it.
3. **Serialization is two-pass.** A reader has to interpret the bitfield, then go fetch the matching payload. An LLM looking at the JSON sees `"Verbs": 3` and has nothing useful to chew on.

**The correct shape: a folder per concept (singular name), a file per variant, always-present records with sub-option booleans defaulting to true, and each owner does its own coverage check.**

```
Permission/
  this.cs              -- @this manager + the Permission record (both live here)
  Verb/
    this.cs            -- Verb @this: composes Read/Write/Delete coverage
    Read.cs            -- record Read(bool Recursive = true, bool Metadata = true)
    Write.cs           -- record Write(bool Create = true, bool Overwrite = true, bool Append = true, bool Mkdir = true)
    Delete.cs          -- record Delete(bool Recursive = true, bool Permanent = true)
```

Singular namespace: `App.FileSystem.Permission`. The full type name `App.FileSystem.Permission.Permission` is the doubled-name cost of singular OBP — C# wasn't designed for this pattern, and the consistency of every folder being singular is worth the small awkwardness.

**`Verb/this.cs`** — variants are always-present, defaulted to full capability:

```csharp
namespace App.FileSystem.Permission.Verb;

public class @this
{
    public Read   Read   { get; init; } = new Read();
    public Write  Write  { get; init; } = new Write();
    public Delete Delete { get; init; } = new Delete();

    public bool Covers(@this requested) =>
        Read.Covers(requested.Read) &&
        Write.Covers(requested.Write) &&
        Delete.Covers(requested.Delete);
}
```

**Each variant owns its own coverage rule** — Read knows what "this Read covers that Read" means; Verb only composes:

```csharp
public record Read(bool Recursive = true, bool Metadata = true)
{
    public bool Covers(Read r) => (!r.Recursive || Recursive) && (!r.Metadata || Metadata);
}

public record Write(bool Create = true, bool Overwrite = true, bool Append = true, bool Mkdir = true)
{
    public bool Covers(Write w) =>
        (!w.Create    || Create)    &&
        (!w.Overwrite || Overwrite) &&
        (!w.Append    || Append)    &&
        (!w.Mkdir     || Mkdir);
}

public record Delete(bool Recursive = true, bool Permanent = true)
{
    public bool Covers(Delete d) => (!d.Recursive || Recursive) && (!d.Permanent || Permanent);
}
```

The coverage rule reads naturally: *"if the request needs feature X, the grant must have X."*

**`Permission/this.cs` — the record and the manager together. Note: methods take whole domain objects (`Path`), not pre-decomposed primitives (`string`), and use verb-named implementation methods (`HasAccess`, `Covers`) which are real-work methods, not property-shaped getters:**

```csharp
namespace App.FileSystem.Permission;

public enum Match { Exact, Glob, Regex }

public record Permission(string AppId, string Path, Verb.@this Verb, Match Match)
{
    public bool HasAccess(Path path, Verb.@this requested)
    {
        if (!PathMatches(path)) return false;
        return Verb.Covers(requested);
    }

    private bool PathMatches(Path path) => Match switch
    {
        Match.Exact => string.Equals(Path, path.Absolute, StringComparison.OrdinalIgnoreCase),
        Match.Glob  => Glob.IsMatch(Path, path.Absolute),
        Match.Regex => System.Text.RegularExpressions.Regex.IsMatch(path.Absolute, Path),
        _           => false
    };
}

public class @this
{
    // State lives in the app's system variables, not in a private list here.
    // Permission/@this is a typed view over that variable.
    public IEnumerable<Permission> List() => /* read system variable, unwrap Data<Permission> values */;

    public Data Check(Path path, Verb.@this requested) =>
        List().Any(p => p.HasAccess(path, requested))
            ? Data.Ok()
            : Data.Fail(new PermissionRequired(path, requested));
}
```

**Why `HasAccess(Path path, ...)` and not `HasAccess(string absolutePath, ...)`:** caller doesn't pre-decompose. The record decides which field of `Path` it needs (absolute, raw, the originating goal — whatever). Passing the whole object hides information; passing `path.Absolute` leaks the receiver's internal preference into the call site.

**Why `HasAccess` / `Covers` are fine method names:** they do real work. The OBP rule against verb-prefixed methods (`GetX`, `IsX`) is about property-shaped questions disguised as methods. When the method has actual logic, verbs are how English describes work and method names can read like prose.

Construction reads naturally — the default is full capability, narrowing is an explicit record copy:

```csharp
// Append-only write, no destructive delete
var loggerVerb = new Verb.@this
{
    Write  = new Write(Overwrite: false),
    Delete = new Delete(Recursive: false, Permanent: false),
};
```

What this fixes:

- **Each variant has a single owner** — `Read.cs` owns what "read" is *and* what "read covers read" means. `Write.cs` owns the same for write. There is no ambiguity about where to add a new field or change a default.
- **No representable nonsense.** No flag enum to disagree with payloads, no nullable records to null-check. The state space is exactly the legal one: every variant is always present, allowance is the booleans inside.
- **Coverage logic lives with the data.** `Permission/@this.Check` is four lines because every comparison is delegated to the type that owns the fields being compared. The manager iterates and composes; it doesn't implement matching itself.
- **Serialization is one-pass and LLM-legible.** JSON is `{"read":{"recursive":true,"metadata":true},"write":{"create":true,"overwrite":false,"append":true,"mkdir":true},"delete":{...}}` — every field is a domain word the LLM can read top-to-bottom.

**The rules, stated tightly:**

> 1. **Folders are singular.** `Permission/`, not `Permissions/`. The doubled type name (`App.FileSystem.Permission.Permission`) is the accepted cost.
> 2. **A concept with N variants, each carrying its own configuration, is one folder.** Each variant is one file owning its record with sensible defaults (default-allow when granted) *and* its own `Covers(other)` rule.
> 3. **Variants are always-present, non-nullable properties on the parent `@this`.** Narrowing is a record copy with explicit `false` on the sub-options you want to revoke. Never a flag enum with parallel option records; never nullable variants used as "granted/not-granted" signaling.
> 4. **Managers compose, they don't implement.** `Check` on a manager calls `record.HasAccess(...)` and `variant.Covers(...)`; it never reaches into a record to apply matching logic from outside.
> 5. **Methods take whole domain objects, not pre-decomposed primitives.** `HasAccess(Path, ...)` not `HasAccess(string absolutePath, ...)`. The receiver decides which field it needs.
> 6. **Verb-named methods are fine when they do real work.** `HasAccess`, `Covers`, `Resolve`, `Open` — all valid. The `GetX`/`IsX` smell is about property-shaped questions dressed up as methods, not about verbs in general.

This is the same OBP move as the collection-promotion smell: choreography that requires reading three files collapses into one folder where each piece has a named owner. The smell checklist (above) catches it for collections; this rule catches it for variant configurations.

---

## Libraries Replaces ActionRegistry

`ActionRegistry` was replaced by `app.Modules` (flat action registry). The key changes:

- **`app.Modules`** — flat registry of all action handlers (module → action → type)
- **Resolution**: `Modules.GetCodeGenerated(module, action, context)` — case-insensitive lookup
- **External DLL loading**: `module.add` action lets PLang code load external DLLs at runtime (`add module mymodule.dll`). `module.remove` unregisters a module.
- **Two registration modes**: `Register(instance)` for shared/stateful handlers, `RegisterType(type)` for per-call instantiation (thread-safe)
- Handler discovery via `Modules.Discover(assembly, namespace)` scans for `[Action]`-attributed types (source generator adds `ICodeGenerated` — handlers don't implement it directly)

---

## GoalFirst Retry Behavior

When `ErrorOrder` is `GoalFirst`, the error goal runs first. If the error goal **succeeds**, the runtime considers the error handled and returns immediately — **retries are skipped entirely**. This is by design: the error goal resolved the problem, so there's nothing to retry.

Only if the error goal fails (or is absent) does the runtime proceed to retries. This means `GoalFirst` with both a goal and retries configured will only use the retries as a fallback when the error goal can't handle the problem.

`RetryFirst` (the default) is the opposite order: retries run first, the error goal only runs if every retry still fails. `IgnoreError` is the final fallback in both orderings — applied after retry and goal are both exhausted.

See `PLang/app/modules/error/handle.cs` for the implementation.

---

## Error Reporting — When to use what

**Rule: match the error mechanism to the return type.**

| Return type | Error mechanism | Example |
|-------------|----------------|---------|
| `Data` or `Data?` | `Data.FromError(new ServiceError(...))` | `GetChild` depth exceeded → `FromError("NavigationDepthExceeded", 400)` |
| `Task<Data>` | Same — return `Data.FromError(...)` | Handler `Run()` methods |
| Constructor / `void` | `throw` — caller must catch | `Data` constructor, `UnwrapJsonElement` |
| `string`, `Type?`, etc. | Return type's natural "not found" (`null`, unchanged value) | `Clr()` → `null`, `ResolveVariablesInPath` → leave unresolved |

**Why this matters:** `Data` has `Error`, `Success`, `Error.Key`, `Error.StatusCode` built in. Returning `null` from a `Data?` method loses information — the caller can't distinguish "not found" from "depth exceeded" or "permission denied." Use `Data.FromError` so the error travels through the normal pipeline with a clear key and status code.

**When a throw converts to Data.FromError:** A method deep inside a Data-returning boundary may throw freely as long as the boundary's try/catch converts to `Data.FromError`. `Decompress()` is the canonical example — it routes through the `application/plang` serializer (which itself returns `Data`) and wraps `InvalidDataException` / `JsonException` into `Data.FromError`. The throw propagates up to the nearest Data-returning boundary. This is fine — just make sure that boundary exists. (Historical note: an earlier `RehydrateNestedData` walk illustrated the same pattern; it was deleted on `data-serialize-cleanup` when Compress/Decompress flattened — the discipline is unchanged.)

---

## Sub-Step Execution — Condition-Gated Skipping

Indented steps (sub-steps) default to NOT executing. They must be "proven true" by a parent condition step. The mechanism:

1. `condition.if` evaluates its condition.
2. It walks the goal's step list from its own index forward, setting `step.Disabled = !conditionResult` on all steps with deeper indent.
3. `Step.Disabled` is a context-backed property — the value is stored on `Context._data` using a key like `step:{prPath}:{index}:disabled`. This keeps the disabled state per-execution, not on the shared Step object.
4. The step runner skips any step where `Disabled == true`.

**Thread safety:** The disabled state lives on the actor's Context data store, not on the Step object itself. Each execution context has its own copy.

**Nesting:** Works at arbitrary depth. When an inner `if` evaluates false, only its immediate indented children are disabled. The outer condition's children at the parent indent level continue normally.

## Condition Orchestration — if/elseif/else in One Step

When a step contains multiple actions and the first is `condition.if`, the condition module orchestrates all actions in the step as branches:

```
Step: "if %x% > 5 set %b% = 4, else set %b% = 0"
Actions: [condition.if, variable.set, condition.if, variable.set]
         ├─ branch 1: condition.if → variable.set (then)
         └─ branch 2: condition.if → variable.set (else)
```

The `Orchestrate()` method:
1. Groups actions into branches: each branch starts with a `condition.if` action, followed by body actions.
2. The last branch with no condition action is the else branch.
3. Evaluates branches in order. The first branch whose condition is true runs its body actions.
4. Returns the result of the matching branch, or `Data(false)` if no branch matched.

**Guard against recursion:** A step-scoped guard key (`__condition_orchestrating_{hashCode}__`) is stored on `Context._data` (not Variables) to prevent the elseif condition evaluations from re-entering orchestration. Inner goal calls from branches get their own guard keys.

---

## Data.Compare — Structural JSON Diff

`Data.Compare(other)` compares two Data objects by serializing both to JSON and walking the tree. Returns a Data whose Value is a dictionary with:
- `match` (bool) — whether the two objects are structurally equal
- `fields` — per-field comparison results (for objects)
- `items` — per-element comparison results (for arrays)
- `missingFields` / `extraFields` — fields present in one but not the other

Comparison rules:
- Numbers compared as `decimal` to avoid int/long/double boxing mismatches
- Keys are case-insensitive
- Null and missing (Undefined) are treated as equivalent
- Strings compared with `StringComparison.Ordinal`

Used by the builder eval runner to compare `.pr` output against `.golden` files.

---

## Security Hardening — Defense-in-Depth Limits

Several subsystems have resource limits to prevent abuse:

| Subsystem | Guard | Limit |
|-----------|-------|-------|
| **HTTP downloads** | `MaxDownloadSize` | 100MB (configurable) |
| **HTTP in-memory reads** | `ReadLimitedStringAsync` / `ReadLimitedBytesAsync` | 100MB |
| **HTTP SSE** | Consecutive overflow counter | Disconnect after 3 |
| **HTTP all streams** | Throughput floor | 1KB/sec over 30s (slow-loris protection) |
| **HTTP URL scheme** | `ResolveUrl` | Only `http://` and `https://` |
| **JSON navigation** | `MaxElementCount` | 100,000 elements |
| **JSON navigation** | `MaxDepth` | 64 levels |
| **JSON string parse** | `MaxJsonStringSize` | 10MB |
| **Variable resolution** | `ResolveDeep` breadth | 100,000 items |
| **Variable resolution** | `ResolveDeep` depth | 100 levels |
| **Ed25519 verification** | Header comparison | Constant-time via `CryptographicOperations.FixedTimeEquals` |
| **File errors** | Error messages | No absolute paths exposed |

---

## [Sensitive] Attribute — Two-Mode Serialization

The `[Sensitive]` attribute (defined in `app/View.cs`) marks properties that contain secret data (e.g., `IdentityData.PrivateKey`). It controls a two-mode serialization split:

- **Output serialization** (the `application/plang` wire serializer + `Data.Transport.Compress`): `Sensitive.Strip` (composed onto the merged serializer's options chain) drops `[Sensitive]` properties. Private keys never leak through channels, API responses, or compressed payloads.
- **Storage serialization** (raw JsonSerializer via DataSource): Filter is NOT applied. Private keys persist in SQLite.
- **Code-level access**: Unaffected. `%MyIdentity.PrivateKey%` in PLang code resolves normally — the attribute only controls serialization.

The filter is always-on — `application/plang` composes it directly onto its STJ options alongside `Transport.ForOutbound` (and `Compress` routes through the same registered serializer). No opt-in required. Any new type with `[Sensitive]` properties is automatically filtered.

---

## IdentityData — Data Subclass

`IdentityData` extends `Data` directly — a pure data record with typed properties (`PublicKey`, `PrivateKey`, `IsDefault`, `IsArchived`, `Created`). It lives on `Actor.Identity` as a property. No lazy resolution, no sync-over-async.

Handlers update `Actor.Identity` directly after mutations (e.g., `setDefault`, `rename`). The `DefaultIdentityProvider.Get()` refreshes `app.System.Identity` when resolving the default identity. `IdentityData.ToString()` returns the public key, so `%MyIdentity%` in a string context gives the public key.

See `PLang/app/modules/identity/types.cs` for the class definition.

---

## %MyIdentity% — DynamicData Registration

`%MyIdentity%` is registered on every actor's Variables as a `DynamicData`:

```csharp
Context.Variables.Set("MyIdentity", new Data.DynamicData("MyIdentity", () =>
{
    var provider = app.Code.Get<IIdentity>();
    if (!provider.Success) return null;
    var result = provider.Value!.GetOrCreateDefaultAsync(new Get { Context = app.System.Context }).GetAwaiter().GetResult();
    return result.Success ? result.Value as Identity : null;
}));
```

This means:
- It always points to the **System** actor's default identity (not the current actor's)
- It re-evaluates on every access (DynamicData calls the lambda each time)
- Changes via `setDefault`, `rename`, or auto-create are reflected immediately
- `%MyIdentity%` in string context gives the public key (`IdentityData.ToString()`)
- `%MyIdentity.PrivateKey%` navigates via dot-notation to the private key
- `%MyIdentity.Name%`, `%MyIdentity.IsDefault%`, etc. all work via standard Variables navigation

---

## app.modules.code — Pluggable Module Implementations

`app.modules.code` (`app.modules.code.@this`) is a named code-implementation registry — `ConcurrentDictionary<Type, ConcurrentDictionary<string, ICode>>`. Each interface type can have multiple named implementations. First registered becomes default.

Each module:
1. Defines a code interface (e.g., `ICrypto`, `ISigning`) under `app/modules/<m>/code/`
2. Ships a default implementation under the same `code/` folder (e.g., `Default.cs`, `Ed25519.cs`)
3. Resolves at runtime via `app.Code.Get<T>(name?)` or `GetOrDefault<T>(fallback)`

PLang developers override by loading a DLL that implements the interface:
```
load code 'my-crypto.dll'
```
→ `code.load` discovers all `ICode` implementations, registers each for its derived interfaces.

**Design decisions:**
- **Type-keyed + name-keyed** — each interface can have multiple named implementations (e.g., "ed25519" and "rsa" both implementing `ISigning`).
- **First-registered-is-default** — no explicit default-setting needed for the common case.
- **Thread-safe** — `ConcurrentDictionary` for both levels. `SetDefault` sets the new default first, then clears old — avoids a window where `Get<T>()` finds no default.
- **No audit trail for replacement** — by design. Implementation swapping is a user-sovereign operation. The security review accepted this.
- **Generic methods delegate to non-generic** — single source of truth for all logic. Non-generic methods use `System.Type` for runtime-resolved types (needed by `code.load` which discovers types via reflection).

**API:**
- `Register<T>(T code)` — registers by name. First for a type becomes default. Returns error if name already taken.
- `Get<T>(name?)` — by name, or returns default if name is null/empty. Returns `Data<T>` with error if not found.
- `GetOrDefault<T>(T fallback)` — returns default implementation or the provided fallback instance.
- `SetDefault<T>(name)` — changes which implementation is the default for type T.
- `Remove<T>(name)` — unregisters by name. Cannot remove the default.
- `List<T>()` / `List()` — lists implementations for a type or all implementations.
- `Has<T>()` — checks if any implementation is registered for type T.
- `ResolveType(typeName)` — maps PLang type names ("signing", "crypto", "identity", "key") to CLR interfaces.

**Code interfaces:**
- `ICode` — base: `Name`, `IsDefault`, `IsBuiltIn`, `Source`
- `IKey : ICode` — `GenerateKeyPair()` → `Data<KeyPair>`
- `ISigning : IKey` — `Sign(bytes, privateKey)`, `Verify(bytes, signature, publicKey)`
- `ICrypto : ICode` — `Hash(bytes, algorithm)`, `VerifyHash(bytes, hash, algorithm)`
- `IIdentity : ICode` — full CRUD for identity management

---

## Condition Evaluation — Type Normalization

`DefaultEvaluator.NormalizeTypes` handles the JSON numeric boxing problem for conditions:

1. **Both numeric** → convert to the wider type (`byte → short → int → long → float → double → decimal`)
2. **One string, one numeric** → try parsing the string as a number, then normalize
3. **Unknown numeric type** → falls back to `decimal` (the widest), not `byte`

This prevents `InvalidCastException` when comparing `int` vs `long` (a common JSON deserialization mismatch). The `ContainsElement` helper applies the same normalization per-element for collection `contains`/`in` checks.

---

## Signing Module — Architecture

The signing module (`signing.sign`, `signing.verify`) creates and verifies cryptographic signatures attached to `Data`. Key design decisions:

**SignedData owns everything.** `SignedData.CreateAsync(sign action)` orchestrates signing, `SignedData.VerifyAsync(verify action)` orchestrates verification. Handlers are one-line delegates — all logic lives on the `SignedData` record itself (OBP: behavior on the owner).

**Deterministic serialization.** `JsonPropertyOrder` on every field ensures identical byte output for signing and verification. `ToSigningBytes()` nulls the Signature field before serializing (save-mutate-restore pattern) — safe because PLang executes steps sequentially per context.

**9-step verification.** Type → provider → timeout → expiry → nonce replay → contracts → headers → data hash → cryptographic signature. Each step returns a specific error key (e.g., `TimedOut`, `NonceReplay`, `ContractMismatch`) so PLang developers can handle specific failures.

**Nonce replay protection.** Uses `ICache.TryAddAsync` with a TTL matching the signature timeout. Atomic — first use succeeds, replays fail. Single-process only; distributed deployments need a shared ICache implementation (Redis).

**Implementation resolution.** The `sign` and `verify` actions both declare `[Code] ISigning Signer` — the source generator emits eager `app.Code.Get<ISigning>()` (registry default). To swap algorithms, register a different `ISigning` and promote it via `code.setDefault`. Verification reads the algorithm from the `SignedData.Algorithm` field — the wire signature carries its own identity, not the caller's.

**Contracts.** Lightweight agreement mechanism. Signer attaches contract identifiers (e.g., `["C0"]`), verifier checks they match. Both null/empty = match. Both present = case-insensitive set equality.

**Integration with Data.** `Data.Signature` holds the `SignedData` record (`[JsonIgnore]`, `[Out]`). Signing attaches it; verification reads it. The property is on Data itself, so any Data flowing through channels can carry a signature. As of `data-serialize-cleanup`, `WireJsonConverter.Write` calls `EnsureSigned()` sign-if-missing on every Data it walks, so egress through any channel auto-seals — the explicit `signing.sign` step remains useful when the developer wants to set contracts, headers, or expiry.

---

## Signing — Lazy Verification on Property Access

Accessing `%data.Signature.Verified%` should trigger verification lazily — the PLang developer should NOT need to call an explicit `verify` step first. The `verify` action exists for when you need to pass contracts or headers, but bare property access to `.Verified` must do the verification automatically on first access.

This means `SignedData.Verified` needs a lazy resolution pattern (similar to `IdentityData`): first access triggers the full verification flow, caches the result, and returns it. Both the lazy path and the explicit `verify` action should run the same underlying verification logic.

**Implication for coder:** The verify handler's core logic should be extracted into a shared method that both the `verify` action and the lazy `.Verified` getter can call. The lazy path uses default contracts (e.g., `["C0"]`) and no expected headers. The explicit `verify` action passes the developer-specified contracts and headers.

---

## ILlm — LLM Implementation in app.modules.code

`ILlm` follows the same `ICode` pattern as other module interfaces. Single method: `Task<Data> Query(query action)`. The implementation owns the full lifecycle: config resolution, message formatting, HTTP calls (via the http module), tool execution loop, caching, streaming, validation, and conversation continuity.

**Default implementation:** `OpenAi` (`app/modules/llm/code/OpenAi.cs`) — works with any OpenAI-compatible API (configurable endpoint). Registered on `app.Code` during construction. Switchable via `code.setDefault`.

**PLang type name mapping:** `"llm"` / `"illm"` → `ILlm`.

**Config resolution:** `llm.endpoint` / `llm.apiKey` / `llm.model` read from SettingsStore → environment variables (`OPENAI_API_KEY`, `OPENAI_API_ENDPOINT`) → hard defaults (`gpt-4.1-mini`).

**Tool execution loop:** The implementation calls `app.RunGoalAsync(GoalCall)` for each tool the LLM requests. Tool errors are sent back to the LLM as tool result text ("Error: ..."), letting the LLM decide how to proceed. `MaxToolCalls` is a hard budget — tool calls are sliced to the remaining budget before execution.

**Conversation continuity:** Stores/restores message history in `PLangContext` (`__llm_conversation__`, `__llm_schema__`). Original messages (before format mutation) are stored so format instructions don't compound across turns.

**Cache:** Persistent via `SettingsStore` (SQLite). Hash of messages + model + temperature + schema + format. Skipped when tools are present. Cached results carry `Cached=true` property.

**GoalCall extensions for LLM tools:** `GoalCall.Description` tells the LLM what the goal does. `GoalCall.Parallel` (default false) marks the tool safe for concurrent execution. When all tools in a batch have `Parallel=true`, they run with `Task.WhenAll`.

---

## IHttp — HTTP Implementation in app.modules.code

`IHttp` follows the same `ICode` pattern as `ISigning`, `ICrypto`, etc. Registered on `app.Code` during app construction. `Default` (`app/modules/http/code/Default.cs`) is the built-in implementation that owns `HttpClient`, config resolution, signing integration, streaming, and response parsing.

Full code-interface roster:
- `ICode` — base: `Name`, `IsDefault`, `IsBuiltIn`, `Source`
- `IKey : ICode`
- `ISigning : IKey`
- `ICrypto : ICode`
- `IIdentity : ICode`
- `IHttp : ICode, IDisposable` — HTTP transport, disposable because it owns `HttpClient`
- `ITemplate : ICode` — template rendering (default: `Fluid` using Liquid syntax)
- `ILlm : ICode` — LLM queries (default: `OpenAi`)
- `IBuilder : ICode` — build-time goal parsing, validation, merge, persistence (default: `Default` under `app/modules/builder/code/`)

PLang type name mapping: `"http"` / `"ihttp"` → `IHttp`, `"template"` / `"itemplate"` → `ITemplate`, `"llm"` / `"illm"` → `ILlm`.

---

## IBuilder — Builder Implementation in app.modules.code

`IBuilder` follows the same `ICode` pattern as other module interfaces. Owns all build-time logic — action records are thin one-line delegates. The default implementation under `app/modules/builder/code/Default.cs` handles goal parsing, `.pr` file merging, action validation, and persistence.

**No per-action BuildingGuard.** Earlier revisions had a static `BuildingGuard(IContext)` called first in every method to gate builder actions on `App.Builder.IsEnabled`. That guard was deliberately removed (commit `4633674c`) — builder actions are callable at runtime as well as build time. The trust boundary is the goal signature: a signed `.pr` may legitimately invoke `builder.goals.save` and rewrite sibling `.pr` files, and the user is sovereign over which signatures to trust. `App.Builder.IsEnabled` is still consulted by the file module's default `IFile` on the read path for snapshot logic, but no per-action guard exists on the write path. If you are reasoning about the threat model, the docs file [`docs/modules/builder.md`](../../docs/modules/builder.md) summarises the same posture.

**Goal.Parse() + MergeFrom()**: The builder module adds two key methods to the Goal entity:
- `Goal.Parse(text, path)` — line-by-line parser for `.goal` text format. Produces `List<Goal>` with structural data (Name, Steps with Text/Index/Indent, Visibility, Comments). Supports multi-goal files, `/` and `/* */` comments, `\` escape, continuation lines.
- `Goal.MergeFrom(existing)` — matches steps by `Text`, delegates to `Step.Merge()` for LLM field transfer. Unmatched steps keep empty Actions.

**Step.Merge()**: Copies LLM-derived fields (Actions, Errors, Warnings) from source to target. Structural fields (Text, Index, Indent, LineNumber) are untouched. Only overwrites if source has data. Modifiers travel inside `Actions` — each action carries its own `Modifiers` collection.

**File I/O pattern**: All file operations go through `app.RunAction` with file module actions — consistent with how the LLM module uses `http.request`. No direct `System.IO`.

---

## TransportPropertyFilter — [In] / [Out] Attributes

`[In]` and `[Out]` are serialization view attributes (defined in `app/View.cs`) that control transport-layer property visibility. They work alongside `[JsonIgnore]` to create a three-mode serialization system:

- **Default JSON**: `[JsonIgnore]` properties are hidden (e.g., `Data.Signature`)
- **Inbound transport** (`[In]`): `TransportPropertyFilter.ForInbound` re-includes `[In]` properties during deserialization. Used when parsing `application/plang` responses — `Data.Signature` arrives on the wire and must be deserialized.
- **Outbound transport** (`[Out]`): `TransportPropertyFilter.ForOutbound` re-includes `[Out]` properties during serialization.

**Why this exists:** `Data.Signature` is `[JsonIgnore]` so it doesn't leak into normal JSON output. But for `application/plang` wire protocol, the signature must round-trip. The `[In]` attribute marks it for inbound deserialization; the filter overrides `[JsonIgnore]` selectively.

**Implementation note:** The filter removes any existing hidden entries before re-adding with fresh Get/Set delegates. Simply calling `CreateJsonPropertyInfo` + `Properties.Add` does NOT override `[JsonIgnore]` in System.Text.Json — the hidden entry must be removed first.

---

## ISettings → IConfig Rename

`ISettings` was renamed to `IConfig` across all modules. The rationale: "config" better describes what these classes are — configuration with defaults, not mutable settings. Files:

- `app/modules/settings/ISettings.cs` → `app/config/IConfig.cs`
- `app/modules/settings/ModuleView.cs` → `app/config/ModuleView.cs`
- `app/modules/settings/this.cs` → `app/config/this.cs`
- Module `Settings.cs` files → `Config.cs` (archive, signing, http)

`app.Settings` → `app.Config`. `Settings.Apply` writes action properties to the scope chain via reflection.

---

## IConfigure\<T\> — Build-Time Defaults Pattern

`IConfigure<TConfig>` (in `app.modules`) marks a configure action and links it to its `IConfig` class. The builder uses this to reflect on `TConfig` for filling defaults instead of reflecting on the action record itself.

```csharp
[Action("configure", Cacheable = false)]
public partial class configure : IContext, IConfigure<Config> { ... }
```

This separates the configure action's nullable properties (only non-null values are written to the scope chain) from the Config class's non-nullable defaults.

---

## PathData — Data Subclass in app/filesystem/

`PathData` extends `Data` — a path IS a Data. It was moved from `app/Memory/` to `app/filesystem/` because it's a file system concept, not a memory concept. `Value` holds file content when set by the file module (e.g., after `file.read`). Path properties (`Extension`, `FileName`, `FileNameWithoutExtension`, `Directory`, `Relative`) are on `PathData` directly, not on `Value`.

The class resolves raw path strings into absolute paths. Relative paths resolve against the goal's folder, not the app root. The source generator detects `Resolve(string, PLangContext)` and auto-wraps string parameters.

See `PLang/app/filesystem/PathData.cs` for the class definition.

---

## Action Modifiers — Fold + Grouping

Error handling, caching, and timeouts are **not step-level properties** — they're per-action modifiers. A modifier is a handler that implements `IModifier` and carries `[Modifier(Order = N)]`.

**Runtime.** `Action.RunAsync` hands its dispatch delegate to `Action.Modifiers.RunAsync(innermost, context)`, which walks the list right-to-left. Each action resolves its own handler via `Action.WrapAround` and wraps the running delegate. First in the list = outermost wrapper.

**Builder.** The default `IBuilder.GoalsSave` (`app/modules/builder/code/Default.cs`) calls `step.Actions.GroupModifiers(app.Modules)` before serialization. The LLM returns a flat list; grouping attaches every `[Modifier]` action to the nearest preceding executable action and sorts each cluster by `Order`. A leading modifier with no preceding executable is dropped and recorded as `DroppedLeadingModifier` in `step.Warnings` so the builder author notices.

**Ordering today:** `timeout=1` (outermost — caps everything including cache lookup), `cache=2` (skip the rest on a hit), `error=3` (innermost — closest to the action).

**Adding a modifier.** Write a handler with `[Modifier(Order = N)]` and implement `IModifier.Wrap`. Normal module discovery picks it up; the LLM sees it in the action registry like any other action.

See `PLang/app/modules/IModifier.cs`, `PLang/app/goals/goal/steps/step/actions/action/modifiers/this.cs`, and `PLang/app/goals/goal/steps/step/actions/this.cs` (`GroupModifiers`).

---

## GoalCall — Clone, Never Mutate

Deserialized `GoalCall` instances are **shared**. They come off the `.pr` file and back every invocation of the same step. If two invocations run concurrently (events, future async.fire, HTTP-driven requests), mutating shared `GoalCall` properties (`Parameters`, `Action`) races — one invocation reads the other's `%!error%`.

**Rule:** inside any handler that needs to modify a `GoalCall` before passing it to `RunGoalAsync`, **clone** rather than mutate. Example from `error/handle.cs:CallErrorGoal`:

```csharp
var call = new GoalCall
{
    Name = goalCall.Name,
    Description = goalCall.Description,
    Parallel = goalCall.Parallel,
    Parameters = parameters,
    PrPath = goalCall.PrPath,
    Action = context.Step?.Actions.FirstOrDefault() ?? goalCall.Action
};
return await context.App!.RunGoalAsync(call, context);
```

This pattern applies to any future modifier or handler that parameterises a goal call. Related Clone-family rule: when you add a property to `GoalCall`, update every constructor/clone path that copies it.

---

## Modifier Hardening Backlog

Three accepted-but-unresolved items from security v1 on the modifier feature. Not bugs today — tripwires once new capabilities land.

1. **Negative Ms.** `timeout.after.Ms` and `timer.sleep.Ms` are not validated. `CancelAfter(-2)` and `Task.Delay(-2)` throw `ArgumentOutOfRangeException`. If a developer binds `%ms%` from untrusted external input (HTTP query string, etc.) without sanitising, the modifier throws instead of returning a typed error.
2. **Unbounded RetryCount.** `error.handle.RetryCount` is applied as-is. A `%retryCount%` from untrusted input set to `int.MaxValue` makes the action effectively hang. The inner `Task.Delay` honours cancellation, but a retry with `delayMs == 0` does unbounded work per iteration.
3. **Non-thread-safe cancellation stack.** `Context._cancellationStack` is `Stack<CancellationTokenSource>`. Safe today because handlers execute serially per context, but the roadmap's `async.fire` / `parallel.set` modifiers would run on the same context concurrently. Swap to `ConcurrentStack<T>` or `AsyncLocal<ImmutableStack<T>>` before landing those.

---

## Test Module — Cross-Cutting Invariants

The test runner lives in `PLang/app/modules/test/` (`discover.cs`, `run.cs`, `tag.cs`, `report.cs`) and stores run state on `app.Tester` (`PLang/app/tester/this.cs`). Facts future devs won't see in any single file:

### App boundary = file boundary
Each `.test.goal` file gets its own child `App` rooted at that file's directory — not per-goal, not per-step. `test.run` spins up one App via `await using` per `TestFile`, runs the entry goal, then disposes. Multiple goals inside the same file share state within that test's run. Don't "optimise" this by pooling Apps across tests — isolation is the entire point of the module existing.

### Coverage merge is additive + idempotent
`Coverage.Merge(other)` unions module/action observations and branch indices/labels/chains into the parent. `ConcurrentDictionary.TryAdd` makes repeated calls with the same site/label a no-op. This is what makes `test.run` parallel-safe: each child App has its own `Coverage`, merge happens once on completion, no cross-talk.

### Site key = `goalPath:stepIndex`
The branch-coverage site identifier includes the source path, not just the goal name. A `Start` step in two files never collides. The format is fixed by `run.cs:99` and the same format is rendered in the console and `results.json`. Don't change it without updating both seed (`discover.cs:SeedBranchChains`) and observe (`run.cs` AfterAction binding) in lockstep.

### `test.discover` seeds declared branch chains
Before a single test runs, `test.discover` walks every `condition.if` site in every discovered test's goal tree — including statically-reachable `goal.call` targets — and records each site's declared chain on `Testing.Coverage`. Purpose: unreached sites (branches that exist in source but no test visits) still appear in the coverage report. Runtime observation unions in later without overwriting; seed-then-observe is safe by design (`Coverage.RecordBranchChain` stores only the first chain per site).

### `[RequiresCapability]` is class-level, single-instance
Per `PLang/app/Attributes/RequiresCapabilityAttribute.cs`, the attribute has `AllowMultiple = false`. Multi-capability handlers use `params string[]`: `[RequiresCapability("network", "llm")]`. Discovery reflects over the attribute on the resolved handler type for every action referenced in the test's `.pr` (recursing static `goal.call` chains, depth 50, cycle-safe via visited set) and unions the capabilities into the test's auto-tag set. If you add a new capability-hungry action, remember the attribute — otherwise `--test={"exclude":["your-capability"]}` won't filter it out.

### Staleness check uses goal hash, not mtime
`test.discover` re-parses the current `.goal` text into a Goal object and compares `Goal.Hash` (SHA-256 of Name + concatenated Step.Text) against the `.pr`'s stored hash. Touching the file, changing whitespace, or editing a comment doesn't trigger staleness — only changes that affect step text do. Missing `.pr` or unparseable `.pr` also marks Stale with a reason set on `TestFile.StatusReason`.

### `ChildAppCreated` is a test-only hook
`internal static event Action<App> ChildAppCreated` on `run.cs:29` fires once per child App after configuration (SystemDirectory inherited, `Testing.IsEnabled = true`, `CurrentTest` assigned) and before the entry goal runs. It exists so the runner's own meta-tests can install probes observing child-App state (SystemDirectory, parallel count, etc.) without faking. Do **not** depend on it from production handlers — it's an `internal static event` and subscribers must be thread-safe because parallel tests fire it concurrently.

### `test.tag` no-ops outside test mode
Shared goals often tag themselves so they carry auto-tags when reused in tests (`tag this test 'http'`). When that same goal runs in production (no `CurrentTest` on `App.Tester`), the action does nothing instead of throwing. This is why `test.tag` is callable from production goals — it's a one-way signal, never an error.

### `Variables.Snapshot()` honors exclusions, not sensitivity
The snapshot taken on assertion failure (`PLang/app/variables/this.cs:Snapshot`) excludes `!`-prefixed infrastructure vars, `DynamicData` (Now/GUID), and `SettingsVariable`. It does **not** honour `[Sensitive]` — that filter applies at JSON *serialization* via `Json.DiagnosticOutput` when the snapshot is rendered into the report. Result: ordinary user variables carrying secrets flow through the snapshot but are only masked if their carrier type has `[Sensitive]` on the relevant property. See security-report.json finding #3 on this branch.

### Teach LLM mappings via `ExamplesForLlm()`, never via runtime parsers
When a step like `set %count% = %count% + 1` produces the wrong action chain, the temptation is to add an arithmetic evaluator inside `Variables.Resolve` so the runtime "just handles" the `+`. Don't. The compile path already has a `math` module (`add` / `subtract` / `multiply` / `divide` / `power`); the LLM just doesn't know to translate the RHS-arithmetic shorthand. Adding `ExamplesForLlm()` to each math action with both forms (natural — `"add 5 and 3, write to %sum%"` — and RHS — `"set %count% = %count% + 1"`) mapping to `math.<op> | variable.set Value=%!data%` is enough; the LLM follows the example.

The pattern: `static ExampleSpec[] ExamplesForLlm() => new[] { Example("step text", Action("module.action", new() { ["Param"] = ... }), Action(...)) }` — multi-action chains pass multiple `Action(...)` args to one `Example`. Helpers live in `App.Catalog.ExampleHelpers`.

This keeps three things clean: (1) variables stay dumb (regex `%var%` substitution only, no hidden eval); (2) the action graph is explicit — math operations show up as `math.*` actions in the `.pr`, not as inline strings; (3) the catalog is the single source of truth for what the LLM should produce. Stamping the same intent in two places (catalog examples + runtime evaluator) creates drift and is rejected.

---

## Source Generator — OBP shape and incremental cache

`PLang.Generators/` mirrors the per-folder `@this` convention used by the runtime. Entry point is `PLang.Generators/this.cs` (`IIncrementalGenerator`); below it the work splits into Discovery (Roslyn boundary) and Emission (string output):

```
PLang.Generators/this.cs                — IIncrementalGenerator entry, source-output stage
  ├ Discovery/this.cs                   — IsActionPartialClass predicate, GetActionClassInfo, BuildProperty factory
  └ Emission/
      ├ Action/this.cs                  — per-handler emitter (shell + ExecuteAsync + __SnapshotParams)
      └ Property/
          ├ this.cs                     — abstract record (EmitProperty, EmitSnapshotEntry)
          ├ Data/this.cs                — Data<T> / plain Data
          ├ Code/this.cs                — [Code]
          └ Legacy/this.cs              — raw-scalar (transitional)
```

**Per-property polymorphism.** `Discovery.BuildProperty` picks one of the three Property leaves per declared property and packs primitive fields into the leaf's record. `Emission.Action.@this` consumes `ActionClassInfo` and dispatches via `ActionProperty.EmitProperty(sb)` / `EmitSnapshotEntry(sb)` — the leaves know their own emission shape.

**Incremental cache stability.** Roslyn's `IIncrementalGenerator` caches by **structural** equality on pipeline outputs. `List<T>` uses reference equality, so two lists with identical contents miss the cache on every recompile. `EquatableArray<T>` (in `PLang.Generators/EquatableArray.cs`) wraps `T[]` with element-wise `Equals`/`GetHashCode`. `ActionClassInfo` is a `record` with `EquatableArray<PropertyBase>`, `EquatableArray<string>`, `EquatableArray<RawScalarValidation>`, `EquatableArray<DiagnosticInfo>` — **no `IPropertySymbol` references leak in**, all fields are primitives. Result: if two compilations produce semantically identical class info, Roslyn reuses cached emission output.

Tracking-name constants (`ActionInfoTrackingName`, `ActionInfoFilteredTrackingName`) on `PLang.Generators.@this` exist so `IncrementalCacheTests` can drive `CSharpGeneratorDriver.WithTrackingName(...)` and assert pre-Where vs post-Where step reuse — a regression of "ActionClassInfo no longer value-equal" is caught by the test.

**Test alias clash with namespace generation.** `PLang.Tests/GlobalUsings.cs` declares heavily-used type aliases:

```csharp
global using Data = global::app.data.@this;
global using Variables = app.variables.@this;
```

Do NOT create test namespaces matching these alias names — `PLang.Tests.app.data` or `PLang.Tests.app.variables` namespaces shadow the type alias for all sibling test files (`CS0118: 'Data' is a namespace but is used like a type`). File-level `using Data = ...` cannot override (CS1537 against the global, and the namespace still wins at sibling scope). Convention: when a test folder mirrors `PLang/app/data/` or `PLang/app/variables/`, use the `*Tests` suffix on the folder/namespace (`PLang.Tests/app/DataTests/`, `PLang.Tests/app/VariablesTests/`). Same applies to any future global alias whose name is also a directory under `PLang/app/`.

---

## Action property kinds (PLNG001 build-time gate)

Action handler properties are constrained at build time. `Discovery.IsValidActionProperty` accepts only:

- **`Data<T>` / `Data`** — the standard form. Resolution flows through `Action.GetParameter(name, context).As<T>(Context)` lazily on first read.
- **`[Code] T`** — eagerly populated from `app.Code.Get<T>()` at the start of `ExecuteAsync`. Used for pluggable infrastructure (HTTP, signing, LLM).

Anything else fails the build with `PLNG001: Property '{0}' on action '{1}' must be Data<T> or [Code]. Raw scalars are not permitted.` The diagnostic carries the full identifier span so IDE squiggles underline the property name, not a one-character mark.

**Why the gate exists.** The pre-v4 generator handled raw `partial string` / `partial int` / etc. with bespoke logic per kind — 700 lines of conditionals, hard to extend, easy to break. PLNG001 narrows the surface so emission lives on two Property leaves with one shape each (`Emission/Property/Data` and `Emission/Property/Code`). The Legacy emitter and `[VariableName]` attribute that bridged this in v4–v6 are gone as of `runtime2-generator-obp` v7.

---

## `app.variables.Variable` — the variable-name carrier

`Variable` is a record (`Name`, `RawValue`, `WasPercentWrapped`) used as the wrapped type in `Data<Variable>` for action parameters that *name* a variable rather than carry its value (write targets, read-by-name lookups: `variable.set`, every `list.*`, `loop.foreach` ItemName/KeyName). It implements `IRawNameResolvable`, a marker that tells `Data.AsT_Impl` to skip its `%var%` substitution branch and call `Variable.Resolve(raw, ctx)` directly. Both `value="%x%"` and bare `value="x"` slot forms collapse to `Variable { Name = "x" }` — symmetric, and works even when the named variable doesn't yet exist (e.g., `set %x% = 5` creating x for the first time).

**Why it exists.** Before this change, `[VariableName] string` was a transitional carve-out for slots whose value the source generator strips `%` from rather than resolving. `Data<Variable>` is the typed form: same payload, but lives in the same OBP shape as every other handler property (`Data<T>`), and provenance attaches at the wrapper level (`Data<Variable>.Signature`) for future signing without a third API shape.

**Implicit string conversion gotcha.** `Variable` defines `static implicit operator string(Variable v) => v.Name`, so `string s = name.Value` works. But `var s = name.Value` infers `Variable`, not `string`. If you need a string-typed local, write `string s = name.Value;` (or extract `.Name` explicitly). The implicit conversion fires at method-call boundaries — `Variables.Get(name.Value)` and `Variables.Set(name.Value, …)` read naturally. For string interpolation, `ToString() => Name` covers it: `$"variable '{name.Value}' missing"` prints the canonical name, not the synthesized record format.

**Nullable variant.** `loop/foreach.cs` `ItemName` / `KeyName` are intentionally nullable. Use `name?.Value?.Name ?? default` because `?.Value` returns `Variable?` which doesn't chain through the implicit operator.

**`WasPercentWrapped`.** Records whether the slot was `%x%` or bare `x` on the wire. Not load-bearing today — surfaces the LLM-emission shape for a future build-time validator that warns on bare-name slot values.

**Missing-name guard (v8).** Non-nullable `Data<Variable>` slots (and any future `Data<T>` where `T : IRawNameResolvable`) get a generator-emitted pre-`Run()` validation that fires `MissingRequiredParameter` ServiceError when the parameter is absent or its `.Value == null`. Plumbed Discovery → `ActionClassInfo.RequiresRawNameResolvable` → `Emission/Action/this.cs` (mirrors `[IsNotNull]`). Closes the silent-NRE path the implicit `string` operator would otherwise take. The `foreach` ItemName/KeyName are skipped via the `!p.IsNullable` filter — those slots are intentionally permissive. Empty-string slot values (`Name = ""`) currently pass the guard (`?.Value == null` is the literal check); pre-v7 surfaced this via `string.IsNullOrEmpty(...)`. Tightening is a noted optional follow-up — not blocking, sits inside the signed-`.pr` trust boundary.

---

## `Data.As<T>` — cycle, depth, ServiceError contract

`Data.As<T>(context)` is the v4 resolution entry point. Three guards plus a `ServiceError` contract; both halves matter for handler correctness.

### The two ServiceError keys

| Key | Status | Trigger | Source |
|-----|--------|---------|--------|
| `VariableResolutionCycle` | 400 | A `%var%` references itself transitively (e.g. `%a%="%b%", %b%="%a%"`) | `[ThreadStatic] HashSet<string>` exact-match cycle detection in `AsT_Impl` |
| `ResolveDepthExceeded` | 400 | An *expanding* chain produces a new string at each level past `ResolveDepthLimit = 32` | Depth check inside the cycle's try/finally |

The HashSet alone misses expanding cycles — `%a%="X-%b%", %b%="Y-%a%"` produces a fresh string each level (`"X-Y-X-Y-..."`), so HashSet membership never trips. Real handler chains go 1–5 levels deep; matrix tests exercise 5 (see `AsT_DeepChain_5Levels_ResolvesCorrectly`). The cap is well above any legitimate use.

### The dual capture pattern (don't break either half)

Generated `Data<T>` getters resolve lazily on first read. When `As<T>` returns `FromError(ServiceError)` for a cycle/depth trip, the FromError-Data lives on the backing field with `.Value = default(T)`. A handler `Run()` body that reads `.Value` proceeds with a default, **masking the resolution error**.

The fix is two-part. Generated emission carries both:

```csharp
// (1) In each Data<T> getter — capture the error as the property is touched:
get {
    if (__Body_backing == null) {
        __Body_backing = __ResolveData("body").As<string>(Context);
        if (!__Body_backing.Success) __resolutionError = __Body_backing;
        __Body_set = true;
    }
    return __Body_backing!;
}

// (2) In ExecuteAsync — surface AFTER Run() completes:
if (__resolutionError != null) return __resolutionError;
var __runResult = await Run();
if (__resolutionError != null) return __resolutionError;
return __runResult;
```

The pre-Run check catches eager-validated raw scalars (Legacy emission writes `__resolutionError` before Run too). The post-Run check catches Data<T> getters that fired *during* Run — which is the common case. **Removing either half re-introduces the silent-default bug.** The auditor's first attempt at this fix proposed (1) only; that was dead code without (2).

### Action-destination carve-out

When `T` is `Action.@this` or `IEnumerable<Action.@this>`, `AsT_Impl` skips the variable walk entirely. Sub-actions hold raw `%var%` strings for *deferred* resolution at their own dispatch — resolving them at outer dispatch would prematurely substitute everything inside the action graph.

### `.Value` is raw

`Data.Value` returns the raw stored value (factory-resolved if any, but never `%var%`-substituted). Substitution happens only inside `As<T>(context)`. Each `As<T>` call resolves freshly against the current variable store — there is nothing to cache and nothing to invalidate. Caching, if any, lives on the caller (e.g. the generator's per-property backing field).

---

## `[Sensitive]` masking in ParamSnapshot

When a handler errors, `App.Run` stamps `ICodeGenerated.SnapshotParams()` onto `Error.Params`, which prints to logs/CI artefacts/debug output under "📥 Parameters at dispatch:". Each property contributes a `ParamSnapshot { Name, DeclaredType, PrValue, PrType, FinalValue, WasAccessed }`.

`[Sensitive]` on a `Data<T>` or legacy-scalar property (defined in `app/View.cs`, also used by `SensitivePropertyFilter` for JSON serialization) controls masking in two slots:

| Field | Non-sensitive | Sensitive |
|-------|---------------|-----------|
| `PrValue` | `__pr?.Value` (the raw `.pr` literal — often a `%var%` reference) | `"******"` when the literal is non-null, `null` when absent |
| `FinalValue` | `{set_flag} ? backing : null` | `{set_flag} ? (backing?.Value != null ? "******" : null) : null` |

The null-guard on `FinalValue` (added in v6 nit #3) distinguishes **accessed-and-null** from **accessed-and-redacted**. A sensitive property the handler read but resolved to null reports `FinalValue: null`, not `FinalValue: "******"`. There is no secret to redact in the null case; reporting `"******"` is misleading.

`[Code]` properties are not parameter-sourced — they emit no snapshot entry. Match the convention if you add a new property kind.

**Attribute matching is short-name only.** `Discovery` matches `[Sensitive]` by `AttributeClass.Name == "SensitiveAttribute"` — same convention as `[Code]` (`CodeAttribute`). A different `SensitiveAttribute` declared in another namespace would inadvertently trigger masking. Theoretical only; no current namespace collision in the codebase. If standardisation on fully-qualified attribute matching ever lands, do both at once or you create a different inconsistency.

---

## `Action.GetParameter` — pure parameter lookup

```csharp
public Data GetParameter(string name, Actor.Context context);
```

Walks `Parameters` first, falls back to `Defaults`, returns `Data.NotFound(name)` when missing. **Pure lookup — no resolution side effects.** Resolution lives in `Data.As<T>(context)`.

Why the `context` parameter even though the lookup is context-free today: contract symmetry with `As<T>(context)`. Both names "reach into the parameter graph" — a future variant that resolves on lookup (e.g. for handlers that want the resolved Data immediately) keeps the same signature. The hook is cheap; renaming the API later is not.

**Within the source generator**, handlers call `__ResolveData(name)` which delegates to `GetParameter` and stamps the Data's `Context`. From outside, callers (e.g. tests composing actions directly) call `GetParameter` themselves and pipe through `As<T>`.

---

## `ICodeGenerated.SnapshotParams` — default-impl interface method

`ICodeGenerated` declares `List<ParamSnapshot> SnapshotParams() => new();` with an interface default impl. The generator emits a per-handler override that walks each declared property and produces a `ParamSnapshot` (delegating to `EmitSnapshotEntry` on the corresponding `Property` leaf).

**Don't implement `SnapshotParams` by hand.** Same reason handlers don't write `: ICodeGenerated` — the generator owns this surface. The default-impl exists so handlers without parameter properties (e.g. simple infrastructure actions) compile cleanly without a generated override.

`App.Run` calls `handler.SnapshotParams()` from its catch block (and from the success-with-error path) and stamps the result onto `Error.Params` if not already populated. The generator no longer attaches snapshots inside generated `ExecuteAsync` — that responsibility moved to `App.Run` in v4 Phase 3 so all dispatch paths get consistent error context.

---

## Data identity preservation — `As<T>` four wrap rules

`Data.As<T>(context)` does not always allocate. It applies four rules (architect/v1/plan.md §Phase 2; `app/data/this.cs` `WrapAs<T>`):

1. **Same-type fast path** — `this is Data<T>` and `.Value is T` → return `this`. No allocation.
2. **Variance fast path** — `value is T` and `IsPlangAssignable(T, value.GetType())` → new `Data<T>` whose `.Value` is the same reference (cast-only). `Properties`, `OnCreate`, `OnChange`, `OnDelete` aliased from `this`.
3. **Cross-type with conversion** — converted `.Value`, state aliased. `T == IEnumerable` delegates to `Data.AsEnumerable()` so the string-not-iterable rule has one source of truth.
4. **Conversion failure** — `Data<T>.FromError(error)` sentinel; nothing aliased. The post-Run resolution check (see *Resolution semantics* in `data-generic-design.md`) surfaces it.

Aliasing means the four state slots are list refs shared between source and view: `wrapped.Properties.Set(...)` is visible through `source.Properties`; subscribers added to either side fire from either side. Removing any of the four alias assignments in `ConstructWrap<T>` is a silent regression — `--debug={"variables":[...]}` watches and `condition.if`'s `branchIndex` (attached to the result Data via `Properties`) both depend on this.

**Where it lives**: `app/data/this.cs` — `WrapAs<T>` is the dispatch; `ConstructWrap<T>` is the per-rule constructor. Plain `Data` slots bypass `As<T>` entirely via `AsCanonical` (next entry).

---

## `AsCanonical` — plain `Data` slots return the live variable

Handler properties typed as plain `Data` (not `Data<T>`) operate on the *live variable*. The generator emits `__ResolveData(name).AsCanonical(Context)` instead of `As<object>(Context)`. `AsCanonical`:

- **Full match `%var%`** → returns the LIVE variable Data from `Variables.Get(name)`. Mutations to `.Value` on the result IS the variable. `list.add` reads `List.Value as List<object?>`, calls `.Add(...)`, and the live variable sees the change without any explicit write-back.
- **Literal value (no `%`)** → returns `this` (the parameter Data). Same ref.
- **Partial interpolation** (`"hello %x%"`) → fresh `Data` over the interpolated string with the slot Name; state aliased from `this`.
- **Container with nested `%var%`** (list/dict) → walked via `WalkContainerVars`; fresh `Data` with substituted values, state aliased from `this`.
- **Unset `%var%`** → not-initialized `Data` carrying the variable name (so handler diagnostics see "missing %x%", not "missing slot").

The walker is shared with `AsT_Impl` so plain `Data` and `Data<T>` resolve nested vars by the same rule. Drift between the two paths bit `set ... type=json` over a list-of-dicts (coder/v2 fix) — the typed path walked containers, the plain path didn't, so handlers reading `Value.Value` saw literal `"%var%"` strings inside.

`Properties` and event lists are aliased on the partial/container paths so subscribers attached to the slot survive the wrap. The four alias lines on the container-walk branch (`transient.Properties = ...; transient.OnCreate = ...; ...`) are unpinned by tests as of this branch (auditor F2 carryover) — preserve them when refactoring.

---

## `Variables.Set` — events follow the name, Properties stay with the Data

When `Variables.Set(dv)` replaces an existing binding under the same name (`app/variables/this.cs:78-87`):

```csharp
if (_variables.TryGetValue(name, out var prev) && !ReferenceEquals(prev, dv))
{
    dv.OnCreate = prev.OnCreate;   // alias — same list refs
    dv.OnChange = prev.OnChange;
    dv.OnDelete = prev.OnDelete;
    prev.FireOnChange(dv);
}
```

**Events follow the name.** Each `Data` under a name shares the *same* event-list refs as the prev binding. Subscribers added at any point — to source, to any view, before or after replacement — are visible from every alias because they share the same list. This is what makes `--debug={"variables":[{"name":"x","event":"onchange"}]}` see every assignment to `%x%`, not just the first; pinned by `Set_Replace_AliasesPrevOnChangeOntoDv` and the regression test `DebugWatch_OnChange_FiresOnEveryReplacement` in `SubscriberSurvivalTests`.

**Properties stay with the `Data` instance.** They're metadata about the *value* (e.g. `condition.if`'s `branchIndex`, attached to a step's `!data` Data). A new binding starts with its own `Properties` so stale metadata doesn't bleed across re-bindings.

**Idempotent Set.** The `!ReferenceEquals(prev, dv)` guard means setting the same instance twice is a no-op (no double-fire of `OnChange`).

**Inconsistency on the non-Data path.** `Variables.Set(string, object?, Type?)` for a non-Data value mutates the existing Data in place via `existing.Value = value`; the `Value` setter fires `OnChange(this, this)` — same instance for both args. The replacement path fires `(prev, dv)` as two distinct Data; the in-place path fires `(this, this)`. `OnTypeChange` watches via the non-Data path therefore never fire (auditor v2 N1) — but user-visible `set %x% = ...` always goes through `variable.set` → `MintTyped` → Data path, so user variable watches work correctly. Engine paths (`!data` rebinding, `list.add` write-back, settings vars) hit the non-Data path; OnTypeChange on those is best-effort.

---

## `variable.set` is the sole binding-mint site

`app/modules/variable/set.cs` owns type inference for user-visible variables. `MintTyped(name, raw, ctx)` switches on the runtime type of the bound value and constructs the right `Data<T>` directly. Hot types (string, bool, int, long, double, decimal, float, DateTime, DateTimeOffset, Guid, byte[], `List<object?>`, `Dictionary<string, object?>`) take the if-chain; cold types fall through to a reflection construction (`typeof(Data<>).MakeGenericType`).

**Mutable refs are snapshot-cloned via JSON roundtrip.** `set %x% = %y%` where `y` is a list/dict mints a `Data<List<object?>>` (or `Data<Dict>`) over a *fresh* container — later mutations of the source do not bleed through. The clone runs through `Data.UnwrapJsonElement` to recursively normalize `List<JsonElement>` (which `JsonSerializer.Deserialize<List<object?>>` produces) into primitives.

**Forced type via `[Type]`** — `set %x% = "42", type=int` calls `TypeMapping.TryConvertTo(value, targetType, ctx)`; conversion failure surfaces as `Data.FromError`, `Variables.Set` is not called, and the binding stays whatever it was. For `type=json`, the value flows through `JsonNode` (`JsonObject` implements `IDictionary<string, JsonNode?>`, NOT `IDictionary<string, object?>`, so it has its own dispatch arm in `TypeConverter`); see *JsonNode in TypeConverter* below.

**Other `Variables.Set` callers exist but don't mint user-named bindings:** `Action.RunAsync` rebinds `!data` per step; `list.add` falls back to `Variables.Set(ListName, list)` on the convert-non-list-to-list path; `cache/wrap.cs` restores cached `!data`. None of these are slots a user would `set %x% = ...` on, so the "sole" framing holds at the user-visible layer.

---

## String-not-iterable — `IsPlangIterable` / `IsPlangAssignable`

C# treats `string` as `IEnumerable<char>`. Plang treats strings as atomic. Two helpers in `app/data/this.cs` enforce this:

```csharp
internal static bool IsPlangIterable(object? value) =>
    value is IEnumerable && value is not string;

internal static bool IsPlangAssignable(Type target, Type source) {
    if (typeof(IEnumerable).IsAssignableFrom(target) && source == typeof(string))
        return false;
    return target.IsAssignableFrom(source);
}
```

Three call sites: `As<T>` variance fast path (so `Data<string>` doesn't variance-cast to `Data<IEnumerable>`), `Data.AsEnumerable()` (single-element wrap), and `Data.EnumerateItems()` (foreach). All route through these helpers so the rule has one source of truth.

**User-visible behaviour**: `foreach %s%` where `s = "hello"` runs the body **once**, with `%item% = "hello"` — not five times with each char. Pinned by `ForeachStringNotIterableTests` (C#) and the deferred `Modules/Loop/Foreach/StringNotIterable.test.goal2` (PLang). `As<IEnumerable>()` on a `Data<string>` falls into Rule 3 and produces a single-element list `["hello"]`.

---

## JsonNode / JsonArray dispatch in `TypeConverter`

`set ... type=json` mints a `Data<JsonNode>` (TypeMapping maps `"json"` → `typeof(JsonNode)`). Downstream typed handlers want concrete types (`LlmMessage`, `List<LlmMessage>`, etc.) so the converter must roundtrip. Caveat: `JsonObject` implements `IDictionary<string, JsonNode?>` (NOT `IDictionary<string, object?>`) and `JsonArray` implements `IList<JsonNode?>` (NOT non-generic `IList`) — neither matches the existing `IDictionary<string, object?>` / `IList` arms in `app/Utils/TypeConverter.cs`'s complex-source dispatch.

Fix lives at `TypeConverter.cs` (~line 336): `JsonNode` joins `IDictionary<string, object?>`, `JsonElement`, `IList` in the complex-source check (so the JSON-roundtrip serialize→deserialize-to-target arm picks it up); `JsonArray` gets its own element-iteration arm parallel to `JsonElement`-array. Pinned by `TypeMappingDictConversionTests`.

**Why this matters cross-cuttingly**: the LLM builder pipeline does `set %messages% = [...], type=json` then passes `%messages%` to a typed handler expecting `List<LlmMessage>`. Without the JsonNode/JsonArray arms, `JsonObject` slipped past every dispatch arm and landed at `TypeMismatch`, which surfaced as an NRE further down in `OpenAi.Query`. Anyone touching the type-conversion dispatch should keep these four arms (`IDictionary`, `JsonElement`, `JsonNode`, `IList`) symmetric — adding a new complex source means adding a parallel arm.

---

## Lazy `Data.Signature` is ICallback-only — the carve-out

When you read `data.Signature`, the getter populates lazily **only if the wrapped value is an `ICallback`**. Plain `Data<T>` keeps `Signature == null` until something explicitly calls `EnsureSigned()` on it.

The carve-out is deliberate. A fully lazy populate would silently break every existing `if (data.Signature == null)` site across the verify path — they'd start succeeding (signature populated) where they previously needed an explicit sign. Restricting auto-populate to `ICallback` keeps the change behavioural-minimum: callbacks cross security boundaries, so they always seal; everything else keeps the explicit-`EnsureSigned` discipline.

`RawSignature` (an internal accessor on `Data.@this`) is the verify-path's hatch: it returns the underlying field without triggering populate, so verify can ask "is there already a signature here?" without changing state.

**If you're tempted to widen the carve-out, audit every `data.Signature == null` site first.** The trip-wires are subtle.

See `Documentation/v0.2/callbacks.md` for the seal-then-verify gate that depends on this discipline.

---

## `RestoredFrame` is a surrogate, not a `Call.@this`

`PLang/app/callstack/RestoredFrame.cs` is the position record callbacks use to identify their resume point. It carries the resolved live `Action` (linked to its Step → Goal in the live `app.goals` registry) plus the positional triple `(StepIndex, ActionIndex, Id)` captured at issue time.

**It is not a `Call.@this`.** It cannot be Pushed into `CallStack`'s AsyncLocal `Current`. It has no Stopwatch, no `OnSet`, no lifecycle. Restoring into a real `Call.@this` would tear up its invariants because Call's ctor is internal and lifecycle-coupled.

The dispatch path is: callbacks read `RestoredFrame` to identify the resume `Position`; `callback.Run` dispatches the bottom frame's Action through `App.Run`, which Pushes a *fresh* live `Call`. The surrogate exists so the snapshot wire shape stays a pure data record — restoring does not mean reconstructing the AsyncLocal frame in place.

---

## `Errors.Push` sets `error.App = this.App` for callback materialisation

`Error.Callback` is a property that materialises an `ErrorCallback` on demand by calling `app.Snapshot()`. For that call to land, the `Error` instance needs a path to the live App tree at the point the callback is asked for — which is later than the point the error was raised.

`PLang/app/errors/this.cs` solves this by setting `error.App = this.App` inside `Push`. Errors plumb through code that doesn't know about App; the back-ref means recovery callbacks can materialise without re-threading App through the throw site. If you reorganise error handling, preserve this assignment — `Error.Callback` reads `App` via this property and silently returning `null` would mask the recovery path.

---

## System.IO Is Banned in Production C# (use `path.@this`)

Action handlers and engine code under `PLang/app/**` must NOT call
`System.IO.*` directly. The only allowed filesystem surface is the
`app.types.path.@this` verb set (`ReadText`, `ReadBytes`, `WriteText`,
`WriteBytes`, `Append`, `Mkdir`, `Delete`, `List`, `Stat`, `MoveTo`,
`CopyTo`, `ExistsAsync`, `AsBooleanAsync`). Every one of those methods
passes through `FilePath.AuthGate(verb)` before touching the disk.

A handler reaching for `System.IO.File`, `System.IO.Directory`,
`System.IO.FileInfo`, or `System.IO.Path.*` (Combine/GetDirectoryName/
GetFullPath/...) is reaching **under** the auth gate. That means an
out-of-root path the actor never consented to gets read / written
silently. It's the filesystem analogue of the `Console.*` ban below.

**The rule.** A filesystem path in interior C# is `app.types.path.@this`
(the lowercase `path` alias). `string` only appears at the perimeter:
CLI args, JSON-on-disk shape (the wire format), scheme-resolved DLL
loads, and the App root anchors (`App.AbsolutePath`, `App.OsDirectory`,
`App.OsAbsolutePath` — they define the root, so they can't be lifted).
Crossing the perimeter into memory means calling
`path.Resolve(rawString, context)`.

**Build-time gate (PLNG002).** The PLang source generator emits a
`PLNG002` diagnostic — **at error severity** — on every `System.IO.*`
member-access reach that touches the disk, plus every `Data<string>`
action-handler property named `Path` / `PrPath` / `Source` /
`Destination` / `Directory` / `Folder` / `FilePath` under `PLang/app/**`.
A clean build is the bar — the codebase has zero PLNG002 warnings as of
the `purge-systemio-from-actions` branch landing.

Allowlist (pure name math, separator constants — none touch the
filesystem): `System.IO.Path.DirectorySeparatorChar` /
`AltDirectorySeparatorChar` / `PathSeparator` / `VolumeSeparatorChar`,
plus `Path.Combine` / `GetDirectoryName` / `GetFileName` /
`GetFileNameWithoutExtension` / `GetExtension` / `GetRelativePath` /
`ChangeExtension` / `GetInvalidFileNameChars` / `GetInvalidPathChars` /
`HasExtension` / `IsPathRooted` / `IsPathFullyQualified` /
`GetFullPath` / `Join` / `TrimEndingDirectorySeparator` / `GetPathRoot` /
`EndsInDirectorySeparator`. These are string transformations, not IO.

Exempt files / namespaces: `app.types.path.**` (the verb surface
legitimately uses `System.IO` post-AuthGate); the `PLang.Generators`
project; and `app.modules.MarkdownTeaching` (bootstrap-time discovery of
static repo-shipped teaching .md files — converting its sync utility
shape to async-everywhere buys no security and lots of churn).

**`.Absolute` discipline (D13).** `path.Absolute` is an easy-to-misuse
escape hatch. Any reach for `.Absolute` outside `app.types.path.**`
means a third-party API (sqlite, image library, `Assembly.LoadFrom`) is
about to touch the filesystem with no gate. Handlers MUST `await
path.Authorize(verb)` first and check `auth.Success` before reading
`.Absolute`. The verb surface (`ReadText`, `WriteText`, `List`, `Stat`,
`ReadBytes`, ...) does this automatically — reach for verbs first;
only fall through to `.Absolute` + manual `Authorize` when a
third-party API genuinely takes over the file (D9b — sqlite is the
canonical case).

**Migration status (purge-systemio-from-actions branch — landed).**

- Stage 1 — derivation verbs (`Parent`/`WithName`/`WithExtension`/
  `Combine`/`InFolder`) + PLNG002 analyzer.
- Stage 2 — `.goal` MIME → Goal deserialization (FilePath.ReadText
  parses `.goal` via Goal.Parse, stamps Path back-reference).
- Stage 3 — Goal.Path / PrPath / LoadedFromPrPath / GoalCall.PrPath
  flip to path?. JSON converter takes Context in its constructor;
  per-Actor `channels.serializers` instances bake a Context-bound
  converter into their options. `Conversion.TryConvertTo(value, type,
  context)` builds a one-shot Context-bound options bag per call so
  deserialised Path fields land Context-wired immediately.
- Stage 4 — AppGoals path-keyed dicts (separate `_byName` index for
  fuzzy lookups); App.Load/Save through path verbs.
- Stage 5 — full ring-2 handler sweep. `test/discover` (the brief's
  headline), `test/report`, `settings/Sqlite` (D9b take-over), `llm/OpenAi`
  (D9a content-shape: `path.ReadAsDataUri`), `module/add` + `code/load` +
  `code/Snapshot` (D8: new `Execute` verb + `path.LoadAssemblyAsync`),
  `ui/Fluid` + `http/Default` file providers, `debug` trace writes
  (`path.Append` + derivation chain), `modules.this.MarkdownTeachingRoot`,
  `goals.LoadFromFileAsync` / `LoadFromDirectoryAsync` / `TryLoadPr` /
  `GetByPrPathAsync`, `goals.goal.Methods.FormatForLlm`,
  `modules.builder.RunAsync` (app.pr existence probe),
  `modules.builder.goals` / `modules.builder.load` (action Path slots).
  New permission verb `Execute` distinct from Read (Unix r/w/x model).
- Stage 6 — PLNG002 flipped to `DiagnosticSeverity.Error`. PLang and
  PlangConsole build clean with zero PLNG002 warnings. The gate now
  fails compilation on regression.

## Console.* Is Banned in Production C#

Channels exist so that I/O is **redirectable** — a user can re-register the `output`/`error`/`debug` channel to a file, an in-memory buffer, an HTTP response, or a goal. Any `Console.WriteLine` / `Console.Write` / `Console.Error.WriteLine` in production C# silently bypasses that surface and breaks the contract.

The rule, with the three flavours of write:

- **Diagnostic / debug chatter** (lifecycle banners, `--debug` traces, internal warnings) → `await context.App.Debug.Write(...)`. This routes through the `debug` channel falling back to `error`, and is gated on `IsEnabled` so it goes silent without `--debug`. Sites that subscribe as `Action<...>` (sync event handlers) can use `_ = Debug.Write(...)` — `Console.Error` was non-awaitable already, so ordering guarantees don't change.
- **User-facing program output** (builder progress lines, LLM validation chatter — the user expects to see them with `--debug` off) → `await app.CurrentActor.Channels.WriteTextAsync(global::app.channels.@this.Output, ...)`. Do **not** route these through `Debug.Write` — the `IsEnabled` gate would silence them in the default case.
- **Interactive prompts** (the App build "create new app? (y/n)" prompt is the canonical example). The default console pair is direction-split: `output` is write-only, `input` is read-only. `Channel.Stream.AskCore` writes-then-reads on a single bidirectional stream and does not work across the split pair. Two-call pattern: write the prompt through `output`, then `using var reader = new StreamReader(inputChannel.Stream, leaveOpen: true)` and `await reader.ReadLineAsync()`. Don't extract a `Channels.AskAcrossAsync` primitive on speculation — there's only one caller.

The two `Console.*` references that **stay**:

- `Console.IsInputRedirected` / `Console.IsOutputRedirected` — these are **queries** ("is stdin a TTY?"), not writes. They gate *whether* to prompt, not *how*.
- `PlangConsole/Program.cs:26` — the process boundary. If `executor.Run` itself fails before channels are wired, this is the last-resort error sink. Single explicit exemption.

Test fixtures that capture stderr by `Console.SetError(...)` are broken under the channel model — the `error` channel was registered with `Console.OpenStandardError()` *at boot*, and re-pointing `Console.Error` later doesn't affect the captured Stream reference. Capture by registering a memory channel as `"error"` on the System actor instead — that's the redirection model channels exist to provide.

## Action `Run()` returns are typed — and the `Data<T>` implicit-operator footgun

Action handlers declare their return shape on the method signature, not via a separate attribute:

- `Task<Data<T>>` for a concrete T (`Data<string>`, `Data<bool>`, `Data<byte[]>`, `Data<path>`, `Data<List<path>>`, …). Renders in the action catalog as `→ returns T`.
- `Task<Data<object>>` for genuinely polymorphic actions (`goal.call`, `environment.run`, `llm.query`, `callback.run`, `mock.verify`, `loop.foreach`, the `list.*` cascade, `math.*`, `condition.*` operators returning bool/string/…). Renders as `→ returns data`.
- Bare `Task<Data>` for actions that produce no value (`output.write`, `error.throw`, side-effect-only writes). The catalog omits the `→ returns` line and the compile LLM rule treats `write to %x%` after such a step as invalid.

`Modules.Describe()` reads the signature by reflection; `action.@this.ReturnTypeName` carries T's PLang name; the per-step template (`stepActionDetails.template`) renders the return line; the compile LLM uses T as the type-annotation on the trailing `variable.set`'s `Value`. There is no separate `Type=` parameter — the `Data<T>` wrapper carries it.

**The footgun.** `data.@this<T>` defines an implicit operator `@this<T>(T value)` (`PLang/app/data/this.cs`). When `T = object` and the source value is itself a `Data` subtype, the operator silently wraps it — you get `Data<object>{ Value = Data<bool>{ Value = true } }` instead of the inner `Data<bool>` passing through. Bites every method declared `Task<Data<object>>` whose body returns a base `Data` or `Data<U>` it received from another call. Symptom: downstream `Value.As<bool>()` sees a `Data<bool>` where it expected a `bool` and either resolves to `default(bool)` or throws.

Mitigations:

- **For polymorphic forwarding actions** (the body genuinely returns a `Data` produced elsewhere — `goal.call`, `llm.query`, `condition.if` evaluators), stay on bare `Task<Data>` until a `Data.As<T>` passthrough or a `Data<T>.From(Data source)` helper lands. The convention is captured in `todos.md` under *Provider typing follow-ups*.
- **For owned-construction actions** (the body produces the value), explicit factory: `data.@this<object>.Ok(value)`. Never `return innerDataInstance;` from a `Task<Data<object>>` method.

Migration status as of branch `path-polymorphism`: ~60% of handlers typed (73 `Task<data.@this<…>>` vs 48 bare `Task<data.@this>`). The remaining bare handlers are either the polymorphic-forwarding carve-out above or genuinely void; both are correct shapes, not pending work. Audit before flipping a bare signature.

## Truthiness — `IBooleanResolvable` and async condition evaluation

A value's boolean meaning belongs to the value, not to `Data`. `Data.ToBoolean()` is the sync fallback (null/false/0/"" falsy, everything else truthy); **do not** add type-specific cases to it. A type that knows its own truthiness implements `app.data.IBooleanResolvable`:

```csharp
public interface IBooleanResolvable
{
    Task<bool> AsBooleanAsync();
}
```

`path` implements it — truthiness means "does the resource exist". For `FilePath` that's a stat; for `HttpPath` it's a HEAD request. Because the probe can be I/O, the entire condition-evaluation pipeline is **async**:

- `IEvaluator.Evaluate` returns `Task<data.@this>` (`PLang/app/modules/condition/code/IEvaluator.cs`).
- `Operator.Evaluate` is `Func<data.@this?, data.@this?, Task<bool>>` (`PLang/app/modules/condition/Operator.cs`).
- `assert.IsTrue` / `assert.IsFalse` are async (`PLang/app/modules/assert/code/Default.cs:138`).
- `Data.ToBooleanAsync()` dispatches to `IBooleanResolvable` when present and falls back to `ToBoolean()` otherwise (`PLang/app/data/this.cs:896`).

A new operator or evaluator must `await`. A new type that wants scheme-defined truthiness implements `IBooleanResolvable` — never edit `Data.ToBoolean()` to special-case it.

## Per-action LLM teaching lives in markdown, not attributes

Action **shape** (what parameters exist, what types, what defaults, is-it-a-modifier) is declared in C# attributes on the handler class — that has to be reflection-readable at compile time. Action **prose** (Description, Notes, Examples) is declared in markdown files at `os/system/modules/<module>/{module,<action>}.{description,notes,examples}.md` and read at catalog-build time by `app.modules.MarkdownTeaching.Load(...)`.

`[Description]`, `[ModuleDescription]`, and `[Example]` no longer exist on action handlers. Don't add them back; the rename from "attribute prose" to "markdown prose" was a deliberate move (branch `compile-llm-notes-per-action`) for three reasons:

- **Tuning teaching doesn't rebuild C#.** Edit the `.md`, run the next build, the LLM sees the new prose.
- **Per-action Notes ship scoped, not global.** Notes for one action render in the user message of the Compile call *only when the planner picked that action*. The system prompt keeps just the cross-cutting kernel (modifier-vs-peer classification, formal-mirroring rule, `%!data%`-never-as-fallback). Rules about one action belong with that action.
- **Two-layer merge kills the drift cycle.** `module.<file>.md` is module-wide; `<action>.<file>.md` is action-specific. Renderer concats module-first + blank + action — no override semantics, so a family rule lives **once** at the module layer.

`module` is a reserved stem inside a module folder; no action may be named `module`. Orphan markdown files (stem is neither `module` nor a registered action) are surfaced via `MarkdownTeaching.ScanOrphans` as warnings on the developer's `Output` channel — loud, not fatal.

Renamed attribute: **`[Provider]` → `[Code]`** across the source generator, the attribute definition, every call site, and the PLNG001 diagnostic text. Mechanical, no behaviour change.

Full guide: [`action-catalog.md`](action-catalog.md). Loader source: `PLang/app/modules/MarkdownTeaching.cs`. Architect plan: `.bot/compile-llm-notes-per-action/architect/plan.md`.

## Build()-time type stamping — `IClass.Build()`, `(type)` hints, and `BuildWarning`

The companion to *Action `Run()` returns are typed* (above) is the **build-time** side: how the type that rides on a step's terminal `variable.set` gets there in the first place.

Three sources, layered by precedence (highest wins):

1. **User `(type)` hint** — `write to %x%(json)` in the PLang source. The kernel rule lives in `os/system/builder/llm/Compile.llm` and tells the LLM to stamp `Type="json"` on the terminal `variable.set`. Any explicit `Type` on the variable.set (including literal `"object"`) is treated as a user hint and wins.
2. **`IClass.Build()` inference** — the optional compile-time hook on every action handler. Default impl returns `Data.Ok()` (no stamp). A handler that knows enough to infer overrides:
   - `file.read.Build()` — literal `Path` → infer from `path.Extension` via `Formats.Mime`.
   - `llm.query.Build()` — `Schema` set → `Ok("json")`; `Format` set → `Ok(Format)`.
   - `http.request.Build()` / `http.upload.Build()` — literal URL with a recognised extension → infer (query/fragment stripped first, registered-types gated).
3. **LLM-emitted `Type`** — what the planner wrote into the step's terminal `variable.set` based on the action's typed `Run()` return (see the *typed Run()* section).

The validate pass (`builder.code.Default.Validate`) iterates every step, calls `IClass.SetAction(action, context)` to prime the handler's lazy property getters, invokes `Build()`, and:

- `Data.Ok(typeName)` → stamps `typeName` onto the terminal `variable.set`'s `Type` parameter (only if the user didn't already set one — precedence #1 above wins).
- `Data.Ok()` (no value) → no terminal change; LLM-emitted Type stays.
- `Data.Fail(err)` → validate aggregates and fails the build.

`SetAction` is **source-generator-emitted** on each handler partial — it mirrors `ExecuteAsync`'s setup minus the runtime-only steps. Callers (validate) invoke through the `IClass` surface without reflection.

### `BuildWarning` — out-of-band advisory writes

In-band errors stay on `Data` (caller short-circuits, must be in the return path). **Advisory** warnings — "I inferred a type but the literal file you named doesn't exist" — travel through a channel-write instead of bending `Data`'s shape:

```csharp
var ch = Context.App.Channels.Channel("builder");
await ch.WriteAsync(new app.modules.builder.warning.@this(this, $"missing literal file: {path}"));
return data.@this.Ok(inferredType);
```

`Channels.Channel(name)` returns a registered channel or a **no-op fallback** (`channel.noop.@this`) — so the handler writes opportunistically without null-checking. (Distinct from `Channels.Resolve(name)`, which returns null on miss and surfaces `ChannelNotFound`.) `Build()` runs in two contexts: under the builder (channel registered, warning surfaces in trace + `--strict`) and outside it (channel absent, the no-op swallows the write).

The warning record `app/modules/builder/warning/this.cs` carries `(IClass Action, string Message)` — the writing handler puts `this` in `Action` so the consumer has source attribution without channel-side caller-tagging magic.

Full implementation: `PLang/app/modules/builder/code/Default.cs` (the validate-pass + `StampOnTerminalVariableSet` helper).

## `Serializers/ISerializer` returns `Data` — no throws

Every `ISerializer` method (`Deserialize<T>`, `DeserializeAsync<T>`, `SerializeAsync`, …) returns `Data` / `Data<T>` rather than throwing. Impls (Json, Text, plang) wrap each method body in try/catch over a **closed list**:

- `System.Text.Json.JsonException`
- `System.NotSupportedException`
- `System.IO.IOException`

…and convert the exception into `Data.FromError`. Anything else (OOM, cancellation) still propagates — by design. If a new serializer impl needs an additional "expected" exception caught, add it to the closed list and surface it as `Data.FromError`; don't introduce a bare `catch (Exception)` that swallows real bugs.

Call sites read `.Success` and `.Value` / `.Error` instead of try/catch around the call. The registry methods pass `Data` through (`Registry.Deserialize<T>` returns `Data<T>`, `Registry.DeserializeAsync<T>` returns `Task<Data<T>>`, `Registry.SerializeAsync` returns `Task<Data>`).

### http body dispatch through the registry

`http.request` / `http.upload` return `Task<Data<app.http.Response.@this>>`. The `Response` record is `(int Status, Dictionary<string,string> Headers, object? Body, TimeSpan Duration)`; `Body` is dispatched by Content-Type via `Serializers.GetByType` + a `TextFallback` for text-shaped misses (`text/*`, `application/xml`, `application/json`, `text/csv`). Binary content-types and missing Content-Type fall back to `byte[]`.

Legacy properties (`%response.StatusCode%`, `%response.Body%` as raw string) remain reachable via `Response.BuildProperties` so existing PLang code keeps working alongside the new `%response.Status%` / typed `%response.Body%`.

## Multi-segment serializer extension matching

`Serializers.GetByExtension` walks **multi-segment** extensions before falling back to the trailing segment. `report.junit.xml` first probes `junit.xml`; if no serializer is registered there, it falls back to `xml`. This lets a future `JunitSerializer` register against the multi-segment stem without colliding with the generic XML serializer.

`path.Extension` (`PathHelper.GetExtension`) returns the extension **without** the leading dot — `"csv"` not `".csv"`. Callers that need the dot prefix it themselves; `Formats.Mime` normalises it back on when needed.

## `IExitsGoal.ShouldExit()` — Value-side opt-out for resolved sentinels

`IExitsGoal` is the marker the engine queries via `result.ShouldExit()` to decide "stop here, capture a Snapshot, return through the channel". `ShouldExit()` is **virtual with default `true`** — the marker alone is enough for a type that always means "suspend".

A type that rides both states on one record (suspending **and** resolved) overrides:

```csharp
public sealed class Ask : IExitsGoal
{
    public string? Answer { get; init; }
    public bool ShouldExit() => Answer == null; // resolved Ask flows through
}
```

`output.ask` returns `Data<Ask>`. On the suspend path `Answer == null` → `ShouldExit()` returns true, the step loop short-circuits, the Snapshot rides as `Data.Snapshot`. On the resume path the channel has pre-bound the answer, `Answer != null` → `ShouldExit()` returns false, the step loop continues and the trailing `variable.set` binds the Ask. Callers read `%name.Answer%` for the structured form; `Ask.ToString() => Answer ?? ""` covers `%name% equals "Alice"` string-context comparisons.

The carve-out: `Data` with only `Type` set (no Value) still triggers the **Type-side** exit check. The Value-side `ShouldExit()` only fires when a Value is present.

Pattern to copy when adding another resolved-sentinel type: implement `IExitsGoal`, expose a nullable "answer-like" field, override `ShouldExit()` to return false when that field is bound. Don't reach for a separate "ResolvedAsk" subclass — one record, two states, the override carries the semantics.
