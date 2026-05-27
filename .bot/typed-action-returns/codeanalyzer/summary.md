# codeanalyzer — typed-action-returns

## Version
v2

## What this is

Branch `typed-action-returns` introduces typed action return plumbing in five stages
(Stage 0 reviewed in v1; v2 covers Stages 1-4 + two bonus refactors).

v2 ships these architectural moves:

- **Stage 1:** `tester/File.cs` → `tester/Test/this.cs` (rename + folder move).
- **Stage 2:** 10 action handlers typed to concrete `Task<Data<T>>` shapes
  (test.discover, test.run, output.ask, mock.intercept, builder.actions, builder.goals,
  builder.types, goal.getTypes, channel.set, test.tag). `output.ask` gains an `Ask`
  record with virtual `IExitsGoal.ShouldExit()`; `MockHandle` renames to
  `app.mock.Mock.@this`; `Schema` renames to `app.builder.Types.@this`.
- **Stage 3:** new `app/http/Response/this.cs` record; `http.request`/`http.upload`
  return `Data<Response>`; `ParseResponseAsync` dispatches body by Content-Type
  through the Serializers registry with a TextFallback.
- **Stage 4:** `file.read.Build()` (extension → type), `llm.query.Build()` (schema/format
  → type), `http.request.Build()` / `http.upload.Build()` (URL extension → type).
  `Serializers.GetByExtension` walks multi-segment extensions; `(type)` hint
  precedence pins user intent over Build() inference.
- **Bonus 1:** `Serializers/ISerializer` returns `Data` everywhere (no more bare `T?`
  with caller try/catch).
- **Bonus 2:** HTTP body dispatch flows through the registry.
- **Bonus 3:** `PathHelper.GetExtension` returns no-dot (`"csv"` not `".csv"`).

## What was done

Read the production diff `68319f649..9513a6fe7` (~770 insertions, ~51 files). Ran the
five-pass analysis + the new leaf-returns-Data check + the Data<T> implicit-op footgun
audit (every typed forwarder verified).

### Findings (11 total — all LOW)

| # | Where | Pass | Note |
|---|---|---|---|
| F1 | `tester/Run.cs:16,40,42` | Readability | `Run.File` property name vs `Test.@this` type — coder-deferred rename |
| F2 | `modules/mock/action.cs` | Readability | Triple-mismatch: filename `action.cs` / class `MockAction` / catalog `mock.intercept` |
| F3 | `http/code/Default.cs:270-340,1060-1108` | Leaf-Data | `ReadLimited{String,Bytes}Async` + `Create{File,Form}ContentAsync` throw instead of returning Data |
| F4 | `http/code/Default.cs:270-340` | Simplification | `ReadLimited{String,Bytes}` are 90% duplicate |
| F5 | `modules/file/read.cs:72` | Behavioral | Bare `catch (System.Exception)` swallows OOM/SOE |
| F6 | `http/code/Default.cs:512-523,593-602,912-924` | Simplification | Application/plang transport bypasses Serializers registry — needs comment or helper |
| F7 | `output/ask.cs:34` (Ask.ToString) | Behavioral | Now renders user answer — caution for diagnostics |
| F8 | `Default.cs:575-578` | Behavioral | `(type)` hint precedence: literal `Type="object"` silently disables Build() inference |
| F9 | `Default.cs:543` | Polish | `err` from `GetCodeGenerated` still discarded (carried from v1) |
| F10 | `modules/test/run.cs:77` | Readability | Stale comment "test.File.Path / PrPath" |
| F11 | `modules/test/discover.cs:81-82` | Readability | Stale docstring "File.Goal is never null" |

### Sound pieces (verified explicitly)

- **OBP shape smells:** no public mutable collection with rules elsewhere; no cross-file
  lock targets; no overlapping collections; no allocate-here/mutate-there splits.
  `Response`/`Ask`/`Mock.@this`/`Test.@this` records all own their own discipline.
- **Data<T> implicit-op footgun:** 10 typed action handlers audited — every forwarder
  uses `.Ok(value)` or `.From(source)` explicitly; no `return innerData;` shapes that
  would double-wrap.
- **Leaf-returns-Data:** the only failures are the four HTTP body/file-content helpers
  in F3 (which throw instead of returning Data). Everything else — Run(), Build(),
  Serializer impls, ParseResponseAsync, path verbs — terminates in `Data`.
- **Serializer try/catch lists:** every catch's exception list is the minimum that can
  actually fire from the wrapped call. No bare catches inside the Serializer family;
  no unreachable branches; no overly broad catches.

### Verdict

**FAIL — NEEDS WORK (low severity).** No blocker. The two deferrals (F1, F8) are
documented architectural follow-ups; F3 + F4 are the meatiest cleanup. The architecture
this branch lands (typed Run() returns, Build() compile-time inference, IExitsGoal
opt-out, Serializers-return-Data, HTTP body via registry) is sound and consistent with
the architect plan + coder handoff.

## Code example — the new leaf rule applied

Where the audit catches existing throws-as-control-flow inside an otherwise Data-clean
module:

```csharp
// http/code/Default.cs — what we have
private static async Task<string> ReadLimitedStringAsync(HttpContent c, long maxBytes, ...)
{
    ...
    if (totalRead > maxBytes)
        throw new InvalidOperationException($"Response body exceeds maximum size of {FormatBytes(maxBytes)}");
    ...
}
// caller relies on ExecuteHttpAsync's catch list to translate to Data.FromError(("ResponseTooLarge", 413)).

// what the leaf rule wants
private static async Task<data.@this<string>> ReadLimitedStringAsync(HttpContent c, long maxBytes, ...)
{
    ...
    if (totalRead > maxBytes)
        return data.@this<string>.FromError(new ServiceError(
            $"Response body exceeds maximum size of {FormatBytes(maxBytes)}", "ResponseTooLarge", 413));
    ...
    return data.@this<string>.Ok(text);
}
// outer ExecuteHttpAsync catch shrinks to network-level only (no InvalidOperationException case).
```

## For v2 after review

n/a — v2 is its own first pass over Stages 1-4. v1 review was for Stage 0.
