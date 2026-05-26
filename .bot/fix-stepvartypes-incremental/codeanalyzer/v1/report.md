# Codeanalyzer Report — fix-stepvartypes-incremental v1

**Reviewed:** 8 C# files changed vs `merge-base(HEAD, origin/runtime2)` = `38722fc1f`.
The rest of the 39-commit diff is generated `.pr` JSON, `.goal`, web UI, and markdown — not C# review surface.

## Branch-wide notes

**Pre-existing System.IO.* in `PLang/app/modules/test/report.cs` (lines 40, 41, 49–50, 53–55, 298).**
Flagged so it isn't lost: `Path.Combine`, `Directory.Exists/CreateDirectory`, `File.WriteAllTextAsync`, `Path.GetDirectoryName`. Banned by CLAUDE.md ("No System.IO.* reaches in production C#"). All hits predate this branch (introduced by `e88eaee04` when `IFileSystem` was ripped out — likely deliberate at the time, since `path.@this` verbs replaced it). **Not a regression on this branch** — out of scope for the pass/fail verdict here. Worth a follow-up commit on `purge-systemio-from-actions` (or its successor) to route through `path.@this` verbs.

---

## PLang/app/modules/builder/BuildResponse.cs

### Verdict: CLEAN
Comment-only change. Describes the deserialization path correctly.

---

## PLang/app/modules/llm/code/OpenAi.cs

### OBP Violations
None.

### Simplifications
None.

### Readability

1. **Pricing data lives inline as a private static tuple array (lines 40–45).** Hard-coded, no override path. For 3 models and a single product (OpenAI) this is fine — the cost of a config indirection would exceed the benefit at current scale. **No change requested.** Worth one comment line about *who* updates it when a price changes (current comment says "bump when OpenAI publishes a change" — sufficient).

2. **`PriceFor` (lines 47–56) is dead-simple longest-prefix-match.** Reads cleanly. Order of entries in `Pricing` doesn't matter because `best` tracks the longest match. ✓

### Behavioral reasoning

1. **Cost arithmetic is correct given OpenAI's `usage` semantics.** `prompt_tokens` in OpenAI responses includes `prompt_tokens_details.cached_tokens`. `Math.Max(0, callPrompt - callCached)` is the right way to isolate the non-cached input bucket — the comment on lines 237–238 captures this; keep it. The `(decimal)int` casts and `/ 1_000_000m` keep everything in decimal — no float drift. ✓

2. **`unknownModelLogged` is hoisted above the `while` loop (line 161)** — so the debug write happens at most once across multiple tool-call iterations of the same `Query`. Good — prevents log spam.

3. **Truncated-path (`break`) now sets `Cost` and `CachedTokens` (lines 491–492).** Previously missing — a `MaxToolCalls` exit silently dropped cost from the result. Fix is correct.

4. **`Truncated = true` is set on the break-exit path but never on the normal-completion path** (line 493 vs lines 464–479). That's intentional — callers can test `Truncated` to detect the bailout. No issue, but worth noting for future readers.

### Verdict: CLEAN

---

## PLang/app/modules/test/report.cs

### OBP Violations
None new on this branch.

### Simplifications
None.

### Readability

1. **`ReportOptions` is a copy-constructed `JsonSerializerOptions` (lines 290–291).** Correctly avoids mutating the shared `Format.Options`. The comment block (lines 283–289) explains *why* (cycle in `AssertionError.Variables` graph) and points to the follow-up todo. Keep both.

### Behavioral reasoning

1. **`IgnoreCycles` silently drops the cycle.** That's the intended trade — the comment is honest about it ("Follow-up: prune Error/CallFrame instances out of the Variables snapshot at capture time"). The downstream JSON consumer never sees the back-reference; PLang assertions that walk `variables` will see a truncated graph. Acceptable as a stop-gap.

2. **`timings = run.Timings.Select(...).ToList()`** in the JSON envelope (line 259) — `Timings` is `IEnumerable<Timing>`, so this projects fine. ✓

### Verdict: CLEAN

---

## PLang/app/modules/test/run.cs

### OBP Violations
None. The event bindings are local closures whose lifetime is bounded by `childApp` (disposed via `await using` at line 75). Allocate / register / dispose all happen in `RunSingleAsync` — no allocate-here/clean-up-there split.

### Simplifications

1. **`RunSingleAsync` is ~170 lines and registers four event bindings inline (lines 91–190).** Each binding is conceptually one *capability*: coverage, output capture, BeforeStep timing, AfterStep timing. Could be extracted to four `RegisterXxx(childApp, testRun, ...)` helpers, which would let the method body read as a story:
   ```
   RegisterCoverage(childApp, testRun);
   RegisterOutputCapture(childApp, testRun, out var outputBuf);
   RegisterStepTimings(childApp, testRun, test.Path);
   ```
   **Low priority** — current code does read top-down and each block has a focused comment. The cost of four small helpers is just file size. Leave for now; revisit if a fifth binding gets added.

### Readability

1. **`var stepStarts = new Dictionary<int, long>();` (line 158) is captured by two separate `EventBinding` closures (BeforeStep, AfterStep).** Single-threaded within a child App (PLang steps run sequentially in one goal call), so the lack of synchronization is correct. The `Timings` class comment makes this scope explicit. ✓

2. **Local function `IsEntryGoalStep` (lines 160–162)** — small, focused, single use site is BeforeStep + AfterStep. Good shape.

3. **Newline behavior on output capture (lines 142–145):** the comment is candid that a payload already ending in `\n` will produce a blank line. Acknowledged trade. Brittle for assertion-style tests that compare exact stdout but acceptable for the rendered UI artefact.

### Behavioral reasoning

1. **`priority: int.MaxValue` on every binding** — same as the existing coverage binding pattern in this method (pre-existing). Consistent with the established convention.

2. **`childApp.User.Context.PushCancellation(cts)` / `PopCancellation()`** is correctly paired across the `try` / `finally`. ✓

3. **`Timeout = 0` interaction** — `TimeSpan.FromSeconds(0)` → `cts.CancelAfter(TimeSpan.Zero)` cancels immediately. If a user sets `Timeout=0` in `Testing`, every test would record `Status.Timeout`. Probably fine (nonsensical input → nonsensical output) but worth a sentence in `Testing.Timeout` docs if not already there. **Not a code change.**

### Verdict: CLEAN

---

## PLang/app/modules/this.cs

### Diff
Description string for `%var%` slots: `"%var% string"` → `"%var%"`.

### Verdict: CLEAN
The new comment (lines 318–323) is exactly the right kind: explains *why* the prior text was wrong (variable resolves to anything at runtime, not necessarily string) and *what symptom* it produced (spurious `ambiguousMapping` warnings). Earns its place.

---

## PLang/app/tester/Run.cs

### OBP Violations
None. `Run` owns its `Timings` outright; `Timings` is the new collection type with its own discipline.

### Simplifications
None.

### Readability

1. **Rename `CapturedOutput` → `Output`** — shorter, matches `Timings`. Aligns with the entity's other fields (`Status`, `Error`, `Duration`). ✓

### Verdict: CLEAN

---

## PLang/app/tester/Timing.cs

```csharp
public sealed record Timing(int StepIndex, double Ms);
```

### Verdict: CLEAN
One-line record. Earns its place — `Run.Timings` semantically holds a list of *these*, not anonymous tuples. The XML doc is clear about why step text is excluded (webui resolves by index against goal source = single source of truth).

---

## PLang/app/tester/Timings.cs

### OBP Violations
None. Textbook OBP: private `List<Timing>`, narrow `Add(stepIndex, ms)` surface, `IEnumerable<Timing>` for read. Matches the four-item smell checklist in reverse — *this is what fixing a smell-1 violation looks like*.

### Simplifications
None.

### Readability

1. **Class doc (lines 5–13)** explains the threading model (single-Run scope), why no concurrency surface is needed, and why nested sub-goal steps don't appear (AfterStep on caller fires after sub-goal returns). All non-obvious. Keep.

### Verdict: CLEAN

---

## Summary

| File | Verdict |
|------|---------|
| `PLang/app/modules/builder/BuildResponse.cs` | CLEAN |
| `PLang/app/modules/llm/code/OpenAi.cs` | CLEAN |
| `PLang/app/modules/test/report.cs` | CLEAN (carries pre-existing System.IO debt — see top) |
| `PLang/app/modules/test/run.cs` | CLEAN (low-priority: extract four bindings to helpers if a fifth gets added) |
| `PLang/app/modules/this.cs` | CLEAN |
| `PLang/app/tester/Run.cs` | CLEAN |
| `PLang/app/tester/Timing.cs` | CLEAN |
| `PLang/app/tester/Timings.cs` | CLEAN |

**Overall verdict: PASS.** No OBP violations on this branch. Behavioral changes (cost math, output/timings capture, `%var%` slot description) are well-reasoned with comments that explain the *why*. The two follow-ups (Variables-snapshot cycle prune, System.IO route-through-`path.@this`) are already tracked in code comments and todos.
