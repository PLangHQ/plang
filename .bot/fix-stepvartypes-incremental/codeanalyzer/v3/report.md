# Codeanalyzer Report — fix-stepvartypes-incremental v3

Branch-wide OBP shape-smell scan, applying the **proposed 5th smell** (producer raw, consumers transform) alongside the four canonical ones. Build is green (0 errors). Tests are green per tester v4 (208/208 PLang, 3036/3036 C#).

## Headline finding — OBP Smell #5: leading-`/` form is *canonical* on this branch, but consumers still TrimStart it

The branch's path-canonicalization commit (`7ed35b550`) changed `path.@this.Relative` to **always** return leading-`/` form ("/Modules/foo.goal", "/Cache/Start.goal"). The contract is documented at `PLang/app/types/path/this.cs:111-115`:

> Canonical PLang root-relative form: leading "/" anchors at the app root, "/" as separator regardless of OS (matches Goal.Path / GoalCall.PrPath stored in .pr files).

The producer is consistent. Two consumers don't trust it:

**PLang/app/modules/test/run.cs:165**
```csharp
var entryGoalPath = test.Path.TrimStart('/');
```

**PLang/app/modules/test/run.cs:168**
```csharp
&& string.Equals(step.Goal?.Path?.ToString().TrimStart('/'), entryGoalPath, StringComparison.Ordinal);
```

The leading comment at line 161-164 admits the design decision:
> step.Goal.Path arrives with a leading slash from .pr deserialization, so normalize both ends before compare.

But after `7ed35b550`, both `test.Path` (from `discover.cs:74`, sourced from `goalFile.Relative`) *and* `step.Goal?.Path?.ToString()` (`path.@this.ToString()` returns `Relative`) **both have the leading slash by contract**. The two `.TrimStart('/')` calls cancel each other out. Comparing the raw forms would also pass.

- **Smell:** #5 (producer raw, consumers transform). Defensive trim at consumer when the producer's contract guarantees the shape.
- **Producer:** `app.tester.File.Path` is `string`, sourced from `path.@this.Relative` — canonical leading-`/`.
- **Why it's wrong now:** the trim is **cargo defensive code**. It happens to work today; if someone deletes one trim but not the other (asymmetry tell), the equality silently breaks for every test discovery.
- **Root-level fix:**
  1. Drop both `.TrimStart('/')` calls. Compare canonical forms directly. The comment at lines 161-164 should be deleted along with them (it documents a workaround for a bug that doesn't exist post-`7ed35b550`).
  2. Or, if the comment IS describing a real residual mismatch (e.g., a test's `discover.cs` happens to pass a non-canonical `Path`), the fix belongs in `discover.cs` to enforce canonical form on `File.Path` once at construction, not at every consumer.

**Confidence: high.** I traced both ends. `discover.cs:74` `relGoalPath = goalFile.Relative` and `goalFile` is a `FilePath` whose `.Relative` returns `path.@this.Relative` (canonical leading-`/`). `Goal.Path?.ToString()` returns `path.@this.ToString()` which returns `Relative`. Same canonical form on both sides.

### Sibling pattern — `test.PrPath` re-resolved at consumer

**PLang/app/modules/test/run.cs:211**
```csharp
var goalCall = new GoalCall { PrPath = global::app.types.path.@this.Resolve(test.PrPath, childApp.User.Context) };
```

`File.PrPath` is `string` (typed at `tester/File.cs:17`). Every consumer that uses it as a path must call `path.@this.Resolve(test.PrPath, ctx)` to convert. There's one such consumer today; if `test.PrPath` were already a `path.@this`, this line collapses to `PrPath = test.PrPath`.

- **Smell:** weaker variant of #5 — single consumer, low cost. But the producer (`File`) is **the** publishing type for test metadata; downstream consumers (web UI, other test modules, future ones) will all repeat the `Resolve` call.
- **Root-level fix:** make `File.Path` and `File.PrPath` typed as `path.@this` from the start. The `[LlmBuilder]` attribute on those properties suggests they're stored as strings in `.pr` files — fine; the `path.@this.JsonConverter` (new on this branch — `path/this.JsonConverter.cs`) handles string ↔ typed serialization. The string-on-disk constraint doesn't require string-in-memory.
- **Priority:** medium. Single offender today; will multiply.

---

## Proposed OBP Smell #6 — `tester/File.cs` holds Goal + flat mirror of Goal's properties

Surfaced after the v3 report was first drafted, on explicit user feedback. Filed as a 6th-smell proposal in `.bot/<branch>/claude-md-proposals.md`; flagged here against this branch's `File.cs`.

```csharp
// PLang/app/tester/File.cs
public sealed class File
{
    [LlmBuilder] public string Path { get; init; } = "";                  // = Goal.Path?.ToString()
    [LlmBuilder] public string PrPath { get; init; } = "";                // = Goal.LoadedFromPrPath?.ToString()
    [LlmBuilder] public string EntryGoalName { get; init; } = "";         // = Goal.Name
    [LlmBuilder] public Status Status { get; set; } = Status.Ready;       // legitimate — File's own lifecycle
    public string Directory { get; init; } = "";                          // = Goal.Path?.Parent
    public Goal? Goal { get; init; }                                      // the reference
    public string? GoalHash { get; init; }                                // = Goal.Hash (or similar)
    public string? BuilderVersion { get; init; }                          // = Goal.BuilderVersion (or similar)
    public HashSet<string> Tags { get; } = new(...);                      // flagged separately under smell #1
    public string? StatusReason { get; set; }                             // legitimate — describes the !Ready state
}
```

- **Smell:** proposed #6 (reference + flat mirror). Six of the public properties are reachable through `Goal` when `Goal != null`; only `Status`, `Tags`, and `StatusReason` describe state that `Goal` can't carry (i.e. discovery-time lifecycle).
- **Severity:** **MEDIUM**. Multiple call sites already read both views (`file.Path` in `report.cs`, `file.Goal?.Path` in some code paths) — drift is theoretical today but the class is *designed* to allow it. Fix at construction is easy now; expensive once consumers proliferate.
- **Construction-time pay:** every `new File { Path = ..., PrPath = ..., Goal = ... }` in `discover.cs` populates both — the flat fields and the reference. Forgetting one (e.g. updating `Goal` later via `init` mutation impossible, but updating a property *of* Goal after File is constructed) makes them drift silently.
- **Memory cost:** 5 strings + 1 nullable string + Directory string ≈ 5 × ~30 bytes (header + small string) > 1 × 8-byte reference. Per discovered test file. Adds up at scale.
- **Why v1/v2/v3 missed it the first pass:** Pass 1b runs 4 items mechanically. "Holds reference AND flat copy" isn't one of them. Same structural gap as smell #5 — the rule has to exist before the bot applies it.
- **Root-level fix:**
  1. Delete `Path`, `PrPath`, `EntryGoalName`, `GoalHash`, `BuilderVersion`, `Directory` from `File`.
  2. Add `Goal { get; init; }` as **non-nullable** when `Status == Ready` (which it is by construction in `discover.cs:DiscoverOne` for the Ready path).
  3. For the Stale/Skipped path where `Goal` may be null, keep `StatusReason` as the carve-out. Consumers reading `file.Path` etc. switch to `file.Goal?.Path?.ToString()` — and the OBP-5 fix at `test/run.cs:165,168` happens *for free* because the leading-slash question now points at `path.@this`, the single owner.
- **Cross-cutting benefit:** closing this finding folds the smell-5 cargo trims into the same migration. One bigger PR, two findings closed.

## OBP Smell #1 — public mutable collection with discipline outside the owner

### `app.tester.Run.UserTags`
```csharp
// tester/Run.cs:38
public HashSet<string> UserTags { get; } = new(StringComparer.OrdinalIgnoreCase);
```
Mutated from outside the owner:
```csharp
// modules/test/tag.cs:24
currentTest.UserTags.Add(tag);
```
- **Smell:** #1. Allocate in `Run`, mutate from `tag.cs`. The discipline (which strings are valid tags, dedupe rules, case insensitivity) lives partly on the field (case-insensitive comparer) and partly is implicit at the call site.
- **Severity:** LOW. One caller, no concurrency in this code path (single test on single thread), simple API. The fix (`Run.AddUserTag(tag)` + `IReadOnlyCollection<string> UserTags`) would barely change the call site.
- **Pre-existing on the branch:** yes. v1 didn't flag this because v1 ran the 4-item checklist mechanically; smell #1 didn't reach a "yes" in v1 by my own (insufficient) judgment — I gave `Run.cs` a CLEAN. Reverting that to NEEDS WORK (LOW).

### `app.tester.File.Tags`
```csharp
// tester/File.cs:38
public HashSet<string> Tags { get; } = new(StringComparer.OrdinalIgnoreCase);
```
Mutated from outside:
```csharp
// modules/test/discover.cs:160
foreach (var tag in tags) file.Tags.Add(tag);
```
- **Smell:** #1, identical shape to `Run.UserTags`.
- **Severity:** LOW. Same fix shape.
- v1 didn't review `File.cs` — new in v3.

### Why both are LOW, not HIGH

Both collections are single-writer, single-reader, no threading concerns, no eviction logic, no invariants beyond "case-insensitive set." The cost of the call-site mutation today is one line each; the cost of the OBP-correct form is also one line each. Move on it whenever `Run` or `File` next get touched — don't fix in isolation.

---

## Repeated guard across sibling methods — `condition/code/Default.cs`

**Three identical guards added by this branch:**

```csharp
// Evaluate(If) at line 16
if (!action.Operator.Success || action.Operator.Value == null)
    return global::app.data.@this<bool>.From(action.Operator);

// Evaluate(Elseif) at line 31
if (!action.Operator.Success || action.Operator.Value == null)
    return global::app.data.@this<bool>.From(action.Operator);

// Evaluate(Compare) at line 46
if (!action.Operator.Success || action.Operator.Value == null)
    return global::app.data.@this<bool>.From(action.Operator);
```

Then in each method's `try` block, line 20 / 35 / 50 dereference `action.Operator.Value.Evaluate(...)` — the guard *removed* the null risk only inside this method. Three methods, three identical guards. Pass 4.5 (root-cause) tells: **shape tell #6** (defensive null checks scattered across consumers) and **volume tell #2** (diff spans many files for a small bug — but here it's many *methods* for one bug).

- **Where the broken contract really lives:** `Operator` comes from `if.cs:12`, `elseif.cs:11`, `compare.cs:10` as `data.@this<Operator>` — the LLM builder fills it. The mismatch is that the builder occasionally produces a step with `Operator.Success = false` or null Value, and the runtime evaluator has to tolerate that. The producer-side fix would either:
  - Make `Operator` non-nullable in the catalog so the builder is forced to populate it (PLNG001-style hard gate, see CLAUDE.md "Property kinds"), OR
  - Add a single private method `EnsureOperator(data.@this<Operator>) → data.@this<bool>?` and call it from each Evaluate, so the three guards become one.
- **Severity:** LOW. The current shape is testable and behaves correctly; the smell is maintenance — a fourth `Evaluate(...)` overload added later will copy the same three-line guard.
- **Root-level fix preferred:** the `EnsureOperator` helper. PLNG001 hard-gating is a larger architectural change.

---

## Re-validation of post-v2 commit `9af7fd8b2` — test.report nested-test suppression

```csharp
// report.cs:33-45
if (testing.CurrentTest == null)
{
    var console = new StringBuilder();
    RenderConsole(console, results, testing);
    RenderCoverageTables(console, testing, Context.App.Modules);
    await Context.App.CurrentActor.Channels.WriteTextAsync(global::app.channels.@this.Output, console.ToString());
}
```

- **Pass 4.5 check:** is this a symptom-patch?
  - **Symptom:** nested `test.report` calls polluted parent `plang --test` stdout.
  - **Producer:** `test.run` sets `Tester.CurrentTest` on the child App during execution. That flag *is* the right signal — "am I inside another test."
  - **Verdict:** **not a symptom-patch.** The guard reads producer state (`CurrentTest != null` means "test.run owns this"), and acts at the consumer (suppress its own output). Producer / consumer roles are correctly assigned. The comment at lines 33-38 names the reason precisely.
- **Issue (LOW):** the guard wraps the **console output**, but `Run()` still does all the work of building the test artefact below (writing `.test/results.json` / `.test/junit.xml`). When a parent test runs child tests, both will write to the same path one after another — the child overwrites first, then the parent overwrites with the parent's full set. That's fine *if* the parent's `Results` aggregates children, but worth verifying. Not a code change request from me — flag for tester.

---

## Open LOWs from v2 still present

1. **`PLang/app/modules/test/report.cs:49`** — `var app = Context.App;` is unused. Trivially fixable.
2. **`PLang/app/modules/test/report.cs:307`** — `new[] { '/', '\\' }` allocated per row inside `GroupBy`. Hoist to `private static readonly char[] PathSeparators`.

Neither blocks. Both still LOW.

---

## Files not flagged

These earned a clean read with the wider lens:

- `PLang/app/types/path/this.cs` — canonical-form definition (line 111-118). Producer-side. Clean.
- `PLang/app/types/path/this.Derivation.cs` — adds `[JsonIgnore]` on `Parent`. Pass 4.5: not a symptom-patch — root cause is "self-referencing root would recurse to max-depth"; the right fix is at the producer, exactly what was done. Clean.
- `PLang/app/types/Conversion.cs` — uses `ContextualReadOptions(context)` symmetrically for read+write so a `path.@this` round-trips through `PathJsonConverter`. Comment names *why*. Pass 4.5: not symptom-patch — converter consistency *is* the root. Clean.
- `PLang/app/formats/this.cs` — adds `.template` and `.liquid` MIME mappings. Pure data, no shape concern.
- `PLang/app/modules/test/discover.cs` — drops `NormalizeRelative(goalFile.Relative)` and the locally-built `relPrPath = ".build/" + stem + ".pr"`, trusts `goalFile.Relative` and `prFile.Relative` directly. Pass 4.5: **textbook root-cause fix** — moved the slash-normalization discipline from the consumer back to the producer (`path.@this.Relative`). This is the migration the test/run.cs lines 165/168 still need.
- `PLang/app/modules/builder/BuildResponse.cs` — comment-only. Clean.
- `PLang/app/tester/{Run,Timing,Timings,File}.cs` aside from the smell-#1 findings above. Clean.
- `PLang/app/modules/llm/code/OpenAi.cs` — v1 cost-math findings hold. The `ResolveImage` rewrite (merge content from `purge-systemio-from-actions`) is already reviewed elsewhere.
- `PLang/app/modules/this.cs` — `Describe()` async / `ResolveMarkdownTeachingRoot` returning `path.@this?` are merge content.

---

## Summary

| File | v3 verdict | Findings |
|------|------------|---|
| `PLang/app/modules/test/run.cs` | NEEDS WORK | OBP-5 redundant TrimStart at lines 165, 168; OBP-5 weak variant at line 211 (`PrPath` re-resolve) |
| `PLang/app/modules/test/report.cs` | NEEDS WORK (LOW) | Dead `var app`, PathSeparators per-row alloc (carryover from v2) |
| `PLang/app/tester/Run.cs` | NEEDS WORK (LOW) | OBP-1: `UserTags` mutated from `tag.cs` |
| `PLang/app/tester/File.cs` | NEEDS WORK (MEDIUM) | **Proposed OBP-6: holds Goal reference AND flat mirror (Path, PrPath, EntryGoalName, GoalHash, BuilderVersion, Directory).** Plus OBP-1: `Tags` mutated from `discover.cs`. |
| `PLang/app/modules/condition/code/Default.cs` | NEEDS WORK (LOW) | Repeated guard across 3 sibling methods — extract `EnsureOperator` helper |
| All others | CLEAN | — |

**Overall verdict: NEEDS WORK** — one HIGH (OBP-5 cargo trims at `test/run.cs:165,168`), one MEDIUM (proposed OBP-6: `File.cs` holds `Goal` reference + flat mirror), four LOW.

The pattern I missed in v1/v2: I ran the OBP shape-smell checklist as a 4-item mechanical pass over the diff. The new 5th item (producer raw, consumers transform) catches exactly what `test/run.cs:165,168` is doing, and the broader-branch lens catches the `UserTags`/`Tags` smell-#1 cases too.

### Hand-off

For coder, in priority order:

1. **`PLang/app/tester/File.cs` — collapse the flat mirror.** Delete `Path`, `PrPath`, `EntryGoalName`, `GoalHash`, `BuilderVersion`, `Directory`. Route the 4 consumers in `report.cs` (`run.File.Path`, line 112/122/259/325) and the 2 in `run.cs` (`test.Path`, `test.PrPath` at 165/211) through `file.Goal?.Path`, etc. This closes the proposed OBP-6 AND folds the OBP-5 fix at `run.cs:165,168` into the same migration (the canonical-leading-/ question now points at `path.@this`, the one owner — no more TrimStart at the consumer because the consumer is reading the path-typed value directly).

2. **`PLang/app/modules/test/report.cs:49`** — delete unused `var app = Context.App;`.

3. **`PLang/app/modules/condition/code/Default.cs:16, 31, 46`** — extract `private static data.@this<bool>? GuardOperator(data.@this<Operator> op)` returning the From-Operator value when invalid, null otherwise; replace 3 inline guards.

4. **Optional / next-touch:** `Run.UserTags` and `File.Tags` → private collection + `Add*` method. `report.cs:307` → hoist `PathSeparators`.

**If step 1 is deferred** (it's the biggest change), step 1' is the smaller fallback: drop both `.TrimStart('/')` calls at `run.cs:165, 168` and the stale comment at lines 161-164. The trims are doing nothing today; deleting them removes the asymmetry trap without restructuring `File.cs`. But this leaves the OBP-6 root unfixed.
