# Error: one type, variables whole, shown by plang and templates

With Ingi, 2026-09-24. Approved ("ok, like this. you can do both").

> **Coder, you own this.** The code below is a suggestion. The rulings are fixed; the final shape, names of locals and the order inside a step are yours. Where the plan says "trace", the answer is yours to bring back.

## Why

- **`IError` is a second name for `Error`.** `Error` is its only implementer; the 10 subclasses derive from `Error`. Code casts `IError` back to `Error` for what the interface lacks (`Error.cs:282, :309, :356, :403`, `call/this.cs:250, :282`). Nothing named two ways.
- **An error's variables are broken apart.** `variable.Snapshot()` (`variable/list/this.cs:642`) keeps `name → .Peek()` and loses each variable's type. The base `Error.Variables` (`Dictionary<string,string>`) is never filled by anything; `AssertionError` declares its own `Variables` (`Dictionary<string, object?>`) that hides it. A variable is a Data (name, type, value), so the snapshot keeps the Data whole (Ingi).
- **Errors are shown by C#.** The only live display is `PlangConsole/Program.cs:24` → `Error.Format()`, a C# text builder (`Error.cs:232-449`, plus `FormatExtra` in `AssertionError.cs:44` and `SettingsError.cs:56`, plus `CallChainRenderer`). The rule settled 2026-07-13: presentation belongs to os templates (`os/system/error/<code>.<ext>`), and `Error.Format`/`FormatExtra` die. Today `os/system/error/error.html` and `500.html` are loaded by nothing, and `ConsoleError.goal` is reached only from `os/system/events/Runtime/On{App,Goal,Step}Error.goal`, which nothing wires (and it prints `%!error.Format%`, the C# formatter again).

## Rulings (Ingi)

1. `IError` is removed. `Error` is the one type.
2. `Error.Variables` is a list of Data: each variable whole (name, type, value). A plang `list` (its rows are Data; `list.@this(IEnumerable<Data>)`, `type/item/list/this.cs:63`), so it navigates and writes itself.
3. Variables are captured when the error happens, not when it's shown. Assert already does this (`AssertSnapshot.cs:18-19`). Under `--debug`, every error gets them at the frame's `Record` (`call/this.cs:280`), the one door every error passes. Not by default: variables can hold secrets.
4. A plang goal shows the error: `os/system/error/Show.goal`. The goal works on `%!error%`.
5. Templates can't see `!` variables (`Fluid.cs:139` loads `GetAll()`, which skips them, and a Liquid name can't start with `!`), so the goal hands the error in by name: `error=%!error%`. The template says `error`.
6. The template folds repeated (recursive) call frames itself. `CallChainRenderer` goes.

## Step 1 — remove `IError`

- Delete `error/IError.cs`. Every `IError` becomes `Error` (about 85 production lines in about 35 files, 29 test lines).
- `public List<Error> list { get; init; } = new();`
- The casts go: `call/this.cs:250` (`result.Error is { } err`), `call/this.cs:282` (`if (error.CallFrames.Count == 0) error.CallFrames = SnapshotChain();`), and the four inside `FormatError` (it now takes an `Error`; it dies in step 3 anyway, so don't polish it).
- `ICreate.cs:64` keeps its `is Error`: it asks whether a VALUE is an error, a different question.
- Watch for classes with a method or property named `Error` (`actor.context`, `data`): the type may need `global::app.error.Error` there.
- Docs: `Documentation/v0.2/architecture.md:464` ("Errors implement `IError`") and any other mention state what is.

## Step 2 — `Variables` is a list of Data

```csharp
// variable/list/this.cs:642 — each variable rides whole
public global::app.type.item.list.@this Snapshot()
{
    var rows = new List<data.@this>();
    foreach (var kvp in _variables)
    {
        if (kvp.Key.StartsWith("!")) continue;
        if (kvp.Value is data.DynamicData) continue;
        rows.Add(kvp.Value);                       // was: dict[kvp.Key] = kvp.Value.Peek();
    }
    return new global::app.type.item.list.@this(rows);
}

// Error.cs — one member; the never-filled Dictionary<string,string> and AssertionError's own Variables go
public global::app.type.item.list.@this? Variables { get; set; }
```

- `AssertSnapshot.cs:18-19` keeps working, assigning the base `Variables`.
- `call/this.cs:280` `Record`: under `--debug`, `error.Variables ??= <the frame's context>.Variable.Snapshot()`. **Trace:** how `Record` reaches the running context and the debug flag.
- `report.cs:64, :135`: rows instead of pairs (`%{v.Name}% = …`).
- **Check:** `TestAssertFailureSnapshotsVariables.test.goal` (in `Tests/TestModule/Assert/` and `Tests/Modules/Test/Assert/`) uses `where Error.Variables.foo equals 42`. On a list that works only if list navigation finds a row by its name. Bring back what it does; don't add name lookup to lists without asking.
- `VariablesSnapshotTests` / `AssertionErrorVariablesTests` move to the list shape.

## Step 3 — plang and templates show the error

```
os/system/error/Show.goal (NEW, sketch)
Show
- render "/system/error/%!error.StatusCode%.txt", error=%!error%, write to %text%
- write %text% to error channel
```

- **Templates:** `400.txt` (short: key, message, file:line, step text, fix suggestion) and `500.txt` (full: what `Format` shows today: header, file:line, step text, message, data, fix suggestion, links, details, variables, call stack with repeated frames folded, parameters at dispatch, the exception for C# developers). 4xx/5xx is the old Application/Runtime split (`Category` is `StatusCode < 500`). `error.html` → `400.html`; `500.html` stays.
- **Fallback:** exact code (`404.txt`) → `400.txt` / `500.txt`. **Propose how** (the goal can't combine `if` + `set` in one step).
- **Who calls it:** a failed top-level run is shown exactly once. Show runs as the top-level error handler, so `%!error%` is the failed error exactly as in any `on error` handler. **Trace:** where the top result comes back (`App.Start`, `app/this.cs:490-523`; `Executor.Run`, `Executor.cs:15-20`) and how handlers get `%!error%`, then propose the call site. `Program.cs:24` stays only as the last resort: when Show itself fails, or the app never started (a `Configure` error), it prints `error.ToString()` (`[Key] Message`).
- **Navigation check:** every member the templates use must be reachable from `error` in a template (items go through the plang door, `Fluid.cs:206-220`; other types fall back to reflection): `Step` (`Text`, `LineNumber`, `Goal.Path`), `CallFrames`, `Params`, `Details`, `Data`, `Variables`, `Exception`. Bring back any that don't navigate.

## Demolition

| Dies | When |
|---|---|
| `error/IError.cs` and every `IError` reference | step 1 |
| the `is Error` casts in `call/this.cs:250, :282` and `FormatError` | step 1 |
| `Error.Variables` as `Dictionary<string,string>` | step 2 |
| `AssertionError.Variables` (its own, hiding the base) | step 2 |
| `variable.Snapshot()` returning `Dictionary<string, object?>` of `.Peek()` | step 2 |
| `Error.Format()`, static `FormatError`, static `FormatVerboseValue`, virtual `FormatExtra` | step 3 |
| `AssertionError.FormatExtra`, `SettingsError.FormatExtra` overrides | step 3 |
| `CallChainRenderer` (its only user is `FormatError`) | step 3 |
| `os/system/error/ConsoleError.goal` and its `.build` file | step 3 |
| `os/system/events/Runtime/On{App,Goal,Step}Error.goal` (unwired; only callers of ConsoleError) | step 3 |
| `Program.cs:24`'s `Format()` call (becomes `ToString()`, last resort only) | step 3 |

**Stays:** `Error.ToString()` (`[Key] Message`, the last resort); `AssertionError`'s message building (`FormatMessage`, `Expected`/`Actual`); `Error.Write` (the wire); `ICreate.cs:64`'s check; `AssertSnapshot` (a static helper: noted, not in this plan).

## OBP validation

| Surface | Check | Result |
|---|---|---|
| `Error` (one type) | nothing named two ways | `IError` gone |
| `Variables` | noun, one word; Data kept whole | replaces two members with one name; no `.Peek()` |
| `variable.Snapshot()` | existing name; returns the Data rows | no decomposition |
| `Show` (goal) | one verb | ok |
| `400.txt` / `500.txt` | presentation in os templates | settled 2026-07-13 |
| statics | none added | `FormatError`, `FormatVerboseValue`, `CallChainRenderer` die |

Working agreement: one step, commit when green, report to the architect, wait.
