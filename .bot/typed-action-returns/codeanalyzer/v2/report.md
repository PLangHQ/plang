# codeanalyzer v2 — typed-action-returns (Stages 1-4 + bonus)

**Scope:** Production-code diff `68319f649..9513a6fe7`. 15 commits, ~770 production insertions
across ~51 files. Stage 0 was already reviewed in v1; v2 covers Stages 1-4 + the bonus
Serializers/HTTP-body-dispatch refactors.

**Coder handoff:** PLang.Tests 3123/3123 green; `plang --test` 208 pass / 0 fail / 12 stale.
Architectural decisions documented in `.bot/typed-action-returns/coder/handoff.md`.

**Verdict at a glance:** NEEDS WORK — LOW severity only. No correctness blockers. Several
follow-ups, two of which the coder explicitly deferred in the handoff. The big architectural
moves (typed Run() signatures, Response/Ask records, IExitsGoal opt-out, Serializers → Data,
HTTP body via registry, Build() inference) are sound and match the test contract.

---

## Findings

### F1 — `tester/Run.cs:16,40,42` — `Run.File` property naming inconsistency (READABILITY, LOW)

**Coder explicitly deferred** (handoff "What's intentionally deferred / Run.File → Run.Test").

```csharp
public Test.@this File { get; }
...
public Run(Test.@this file) { File = file; ... }
```

The property type renamed cleanly (`File` → `Test.@this`) but the property name didn't.
Reads as "a Test typed thing called File," which is confusing. Two callers feel it:

- `modules/test/run.cs:77` (stale comment): "test.File.Path / PrPath" — the symbol doesn't
  exist any more; should be `test.Goal.Path / PrPath`.
- `modules/test/run.cs:163-166`: code already reads `test.Goal.Path` correctly; only
  the comment is stale.

**Fix:** rename `Run.File` → `Run.Test`. Single-file rename; only call sites are inside
`tester/`. Coder limited blast radius for the v1 ship; v2 follow-up is the right time.

---

### F2 — `modules/mock/action.cs` — triple-mismatch in mock action naming (READABILITY, LOW, NEW THIS BRANCH)

`[Action("intercept")]` is declared on `public partial class MockAction` in a file named
`action.cs`. Three names disagree:

| Surface | Name |
|---|---|
| Filename | `action.cs` |
| Class | `MockAction` |
| Catalog | `mock.intercept` |

The handoff renamed `mock/types.cs MockHandle` → `mock/Mock/this.cs` (good), but didn't
finish the action's filename/class. Convention elsewhere (`file/read.cs Read`,
`http/request.cs request`, `output/ask.cs ask`, `test/discover.cs discover`) is filename
= class = catalog. `builder/actions.cs GetActions` is the one other precedent for a
mismatch — same root cause (avoiding a clash with the `actions` namespace type), but a
local readability bug there doesn't justify another one here.

**Fix:** rename file to `mock/intercept.cs` and class to `intercept`. Internal-only;
no PLang catalog change because `[Action("intercept")]` already pins the catalog name.

---

### F3 — `http/code/Default.cs:270-340` — leaf helpers return raw types and throw (LEAF-DATA, LOW)

Per the leaf-Data review rule: `ReadLimitedStringAsync` returns `Task<string>` and
`throw new InvalidOperationException` for body-too-large / slow-loris. Same shape on
`ReadLimitedBytesAsync` (line 309-340). These are the actual leaves of "size-capped
HTTP body read" — the discipline of "did this exceed budget" lives here.

```csharp
private static async Task<string> ReadLimitedStringAsync(HttpContent content, long maxBytes, ...)
{
    ...
    if (totalRead > maxBytes)
        throw new InvalidOperationException(
            $"Response body exceeds maximum size of {FormatBytes(maxBytes)}");
    ...
}
```

Today the throw is laundered into Data by `ExecuteHttpAsync`'s outer catch
(line 244-258) mapping `InvalidOperationException → ("ResponseTooLarge", 413)`.
That catch list is opaque: any unrelated `InvalidOperationException` from anywhere
inside `operation()` collapses to the same error. Returning `Data<string>` /
`Data<byte[]>` at the leaf would let the size-cap and slow-loris errors carry their
own keys without ambiguity, and the outer catch could shrink.

Same pattern on `CreateFileContentAsync` (1060) and `CreateFormContentAsync` (1081)
— they `throw new System.IO.IOException(...)` instead of returning a structured
result. The exception type is allowed (PLNG002 bans `System.IO.File/Directory/…`, not
the exception class), but the structural choice — exception-as-control-flow inside
a Data-returning module — is a smell. Pre-existing shape, but visible now that the
file is otherwise Data-clean post-Serializers refactor.

**Fix:** convert these four helpers' leaves to return `Data<T>` and let
`ExecuteHttpAsync`'s catch shrink to only system-level exceptions
(`TaskCanceledException`, `HttpRequestException`, `IOException`).

---

### F4 — `http/code/Default.cs:270-340` — `ReadLimitedString` and `ReadLimitedBytes` are 90% duplicate (SIMPLIFICATION, LOW)

Both functions:

- own a `MemoryStream` accumulator
- read in 8KB chunks
- gate on `totalRead > maxBytes` with the same throw
- gate on throughput < 1KB/sec for 30s with the same throw

Difference is the final step (`StreamReader.ReadToEndAsync` vs `ToArray()`).

**Fix:** one helper that returns `Data<byte[]>` (or `Data<MemoryStream>`), then a
thin `ReadLimitedString` wrapper that UTF-8-decodes. Halves the loop and keeps the
slow-loris/size cap in one spot — the next time we tweak the limit, we don't have
to touch both.

---

### F5 — `modules/file/read.cs:72` — bare `catch (System.Exception)` swallows OOM / SOE (BEHAVIORAL, LOW)

```csharp
try
{
    var exists = await p.ExistsAsync();
    if (exists.Success && exists.Value == false)
        await Context.Actor.Channels.Channel("builder").WriteAsync(...);
}
catch (System.Exception) { /* best-effort warning — never block Build() */ }
```

The "best-effort warning" rationale is sound — Build() must never fail because the
build-warning channel hiccuped. But a fully bare `catch (System.Exception)`
swallows `OutOfMemoryException`, `StackOverflowException`, `ThreadAbortException`,
and `AccessViolationException` along with everything else (the silent-error-critical
rule). The convention in this codebase is to exclude critical exceptions explicitly
— e.g. `http/code/Default.cs:504,681`:

```csharp
catch (Exception ex) when (ex is not (NullReferenceException or OutOfMemoryException or StackOverflowException)) { ... }
```

**Fix:** narrow the catch to that pattern.

---

### F6 — `http/code/Default.cs:512-523, 593-602, 912-924` — application/plang serializer reaches around the registry (SIMPLIFICATION, INFO)

Three sites still do raw `JsonSerializer.Deserialize<data.@this>(... _transportInOptions)`
with a hand-rolled `JsonException` catch:

- `ParseResponseAsync:512` — non-streaming application/plang
- `ParsePlangResponseAsync:591-602` — non-streaming application/plang body
- `StreamPlangAsync:917-924` — NDJSON line-by-line on the streaming path

These predate the Serializers-return-Data bonus and stay because the plang transport
needs the transport-specific `_transportInOptions` (signature inflow via `[In]`). Per
coder handoff this is intentional. The reading is harder, though: in an otherwise
Data-clean file, three local try/catch islands stand out, and the next reader will
ask "should this also go through the registry?"

**Fix:** either (a) add a one-paragraph comment near `ParsePlangResponseAsync` that
explains why this transport bypasses the registry (signature inflow, transport
options), or (b) factor a tiny `PlangTransport.DeserializeAsync(string|stream)`
helper that owns the options + try/catch. Not a correctness issue.

---

### F7 — `Ask.ToString() => Answer ?? ""` may leak user answers into diagnostics (BEHAVIORAL, LOW)

`output/ask.cs:34`. Coder flagged this in handoff "Known-fragile bits." Re-asserting
because the change is silent: any debug/log line that previously printed an `Ask`
via `.ToString()` for diagnostics (default = type-name rendering on POCOs) now
prints the user's typed answer.

In particular, the path-Authorize prompt-loop in `path/this.Authorize.cs:53,69-78`
builds a question that may include the answer ("Invalid answer 'foo'. …"). If that
question is rendered into a log via `WriteText` somewhere downstream, a typo of
sensitive input (typo'd password, secret) lands in logs. Today it routes only to
the `Output` channel via the prompt itself — but the pattern is fragile.

**Fix:** none required at this level (the channel is the right one). Worth a
docstring note on `Ask` that ToString carries the user answer and shouldn't
be used inside diagnostics. The architect's deferred "Permission becomes a
first-class option" item in `path/this.Authorize.cs:18-21` is the right long-term
fix — once `output.ask` grows structured options, the answer no longer rides the
free-form string.

---

### F8 — `Default.cs StampOnTerminalVariableSet:575-578` — user-hint precedence treats *any* `Type` parameter as a hint (BEHAVIORAL, INFO)

```csharp
var existing = a.Parameters.FirstOrDefault(p =>
    string.Equals(p.Name, "Type", StringComparison.OrdinalIgnoreCase));
if (existing != null) return;
```

Comment says "any explicit Type parameter the LLM emitted (including the literal
"object") is treated as the developer's (type) hint." Correct per architect's
spec, but fragile: there's no way to distinguish "LLM forgot Type" from "user
wanted Type=object." If a future LLM bug starts emitting `Type="object"` on every
variable.set, Build() inference silently stops firing across the board, and the
only signal is downstream behavioral drift.

**Resolution — closed, won't fix (Ingi 2026-05-27):** the suggested detector
("LLM emitted Type but source has no `(type)` hint, re-prompt") cannot be
written. Developer type-hints are natural-language and take any form:
`(json)`, `as int`, `make it integer`, `treat as a number`, or even
inferred from question text like "give me json". The LLM's job IS that
translation; a surface-pattern detector would either under-detect (miss
"as int") or over-detect (flag legitimate translations) and would defeat
the trust model.

The drift hazard is real but the catch-point is at user-facing behavior
(snapshot tests showing `(object)` instead of `(csv)`/`(json)`/etc), not
at compile-time validation. Don't reopen.

---

### F9 — Stage 0 v1 finding still present: `modules/builder/code/Default.cs:543` discards `err` (POLISH, LOW)

```csharp
var (handler, err) = modules.GetCodeGenerated(a);
if (handler is not modules.IClass classified) continue;
```

`err` is bound but never read. Either log it (a missing-from-catalog action is a
build-pipeline anomaly worth surfacing) or use the discard pattern `(handler, _)`.
Carried forward unchanged from v1.

---

### F10 — `modules/test/run.cs:77` — stale comment references removed symbol (READABILITY, TRIVIAL)

```
// (Goal.Path, GoalCall PrPath, test.File.Path / PrPath — all "/Modules/..." shaped)
```

`test.File` is the property *on* Run (not on Test). On a `Test.@this` value
called `test`, there is no `.File`. Should read `test.Goal.Path / PrPath`. Trivial
edit, but reads confusingly today.

---

### F11 — `modules/test/discover.cs:81-82` — stale "File.Goal is never null" docstring (READABILITY, TRIVIAL)

```
// Build a minimal goal from just the file's path so File.Goal
// is never null. Status=Stale with the read error as reason.
```

Property name is now `Goal`, not `File.Goal`. Coder swept the codebase to a new
type name but the comments are now off by a layer. Same pattern across the
discover/run files.

---

## Pass summary

- **Pass 1 (OBP rules)** — no rule violations; `[PlangType]` slim ✓; `IExitsGoal` lives
  with the type that owns the discipline ✓; `Response`/`Ask`/`Mock.@this`/`Test.@this`
  records all own their data without external-rules-enforcement.
- **Pass 1b (shape smells)** — clean. `Run.File` (F1) is a *naming* smell, not a shape
  smell — the type ownership is correct.
- **Pass 2 (simplification)** — F4, F6.
- **Pass 3 (readability)** — F1, F2, F10, F11.
- **Pass 4 (behavioral)** — F5 (silent-critical), F7 (Ask leakage), F8 (hint precedence).
- **Pass 5 (deletion test)** — Serializer try/catch sites checked. Each catch's
  exception list is the minimum that can actually fire from the wrapped call (Json
  paths: `JsonException`+`NotSupportedException`+`IOException` for stream;
  `JsonException`+`NotSupportedException` for string). No catch is unreachable;
  none is bare. Good shape.
- **Leaf-returns-Data scan** — only F3 fails (the four HTTP body / file-content
  helpers). Every other leaf in the diff (Run() methods, Build() methods, Serializer
  Serialize/Deserialize, ParseResponseAsync, path verbs) returns `Data` / `Data<T>`.

## Data<T> implicit-operator footgun audit

Per CLAUDE.md "Action `Run()` returns are typed — and the `Data<T>` implicit-operator
footgun." Audited every action whose `Run()` is typed `Task<Data<T>>`:

| Action | Pattern | Status |
|---|---|---|
| `output.ask` | `data.@this<Ask>.Ok(...)` and `.From(askResult)` | ✓ |
| `http.request` | `.From(result)` | ✓ |
| `http.upload` | `.From(result)` | ✓ |
| `mock.intercept` | `.Ok(handle)` | ✓ |
| `builder.actions` | `.From(result)` | ✓ |
| `builder.goals` | `.From(result)` | ✓ |
| `builder.types` | `.Ok(schema)` | ✓ |
| `goal.getTypes` | `.Ok(...)` | ✓ |
| `test.discover` | explicit `.Ok` / `.FromError` everywhere | ✓ |
| `test.run` | `.Ok(parentApp.Tester.Results)` | ✓ |

None of the typed forwarders use `return innerData;` — they all go through
`.Ok(...)` (owned construction) or `.From(...)` (forwarding a Data envelope).

## Files inspected (not exhaustive listing)

`output/ask.cs`, `http/Response/this.cs`, `http/code/Default.cs`, `http/request.cs`,
`http/upload.cs`, `http/HttpBuildHelpers.cs`, `file/read.cs`, `llm/query.cs`,
`mock/Mock/this.cs`, `mock/action.cs`, `tester/Test/this.cs`, `tester/Run.cs`,
`test/discover.cs`, `test/run.cs`, `builder/code/Default.cs`, `builder/actions.cs`,
`builder/goals.cs`, `modules/this.cs`, `types/this.cs`, `goals/this.cs`,
`types/path/this.Authorize.cs`, `data/ShouldExit.cs`, `IExitsGoal.cs`,
`channels/serializers/this.cs`, `channels/serializers/serializer/{this,Json,Text}.cs`,
`Utils/PathHelper.cs`, `modules/settings/Sqlite.cs`.

## Verdict

**FAIL — NEEDS WORK (low severity).** 11 findings. None blocks ship; the two deferrals
(F1 Run.File rename, F8 hint-precedence detection) are documented architectural
follow-ups; F3 + F4 are the meatiest cleanup. No correctness regressions detected.
