# auditor v2 — runtime2-generator-obp

## What this is

Cumulative audit on the Variable + IRawNameResolvable migration that landed
since auditor/v1 closed. Scope:

- coder/v6 (auditor/v1 #1 closure — Data<T> emission surfaces FromError into
  `__resolutionError`)
- architect/v5 → coder/v7 commits 1–4 — replaces `[VariableName] string` with
  `Data<Variable>` across 22 handlers; deletes Legacy property emitter,
  `__StripPercent` / `__Resolve<T>` / `__HasParam` helpers,
  `RawScalarValidations` block, the `[VariableName]` attribute. Adds
  `App.Variables.Variable` record + `App.Variables.IRawNameResolvable` marker
  + `Data.AsT_Impl` carve-out at lines 549-562 that reflects to
  `T.Resolve(string, Context)` for marker types BEFORE `%var%` substitution.
- coder/v7 commit 4 — `variable.set` `CopyProperties` fix + 4 ListAddIdentity
  stubs filled in.

Reviewers already on file: codeanalyzer/v4 (3 MINOR + 7 NIT, no MAJOR),
tester/v7 (4 minor), security/v2 (4 low). All PASS.

## What I did

Standard auditor process: read prior reports, then look in the seams between
them. The dominant signal was security/v2 finding #1 — 19/22 unguarded
handlers crashing with NRE on null Variable, rated **low** under the
user-sovereign threat model. Worth checking whether the rating holds when
the cross-file contract regression is the lens.

Five empirical checks:

1. **Trace AsT_Impl with raw=null and T=Variable.** Confirmed: bypass branch
   gates on `raw is string`, skipped for null; falls through every other
   branch to `WrapAs(null, ctx)` → `ConstructWrap<Variable>(default, ctx)` →
   `Data<Variable>{ Value=null, Success=true }`. The Success=true is
   load-bearing for the bug because the generator's getter only sets
   `__resolutionError = Backing` on `!Backing.Success`.

2. **Verify the [IsNotNull] count.** Security said "3 of 22 handlers".
   Reality: **0 of 22 Data<Variable> slots** carry `[IsNotNull]`. The 3
   security counted (`any.cs` Key + Operator, `group.cs` Key) carry
   `[IsNotNull]` on OTHER (non-Variable) properties — those decorations
   don't help when the Variable slot is missing because the validation loop
   only iterates over `IsNotNullProperties`.

3. **Trace the NRE escape path.** App.Run line 415 catch deliberately
   excludes NRE; the NRE escapes dispatch and is caught by Step.RunAsync
   line 157 (which doesn't exclude NRE). Result: the user sees
   `ServiceError(ex.Message="Object reference not set...", "StepError",
   400)` — without parameter name, without step text, without the Params
   snapshot the App.Run catch path attaches.

4. **Diff against pre-v7 contract.** The deleted RawScalarValidations block
   (recovered from `git show 0312f5f9`) emitted
   `ServiceError("'<value>' is empty — nothing to use as '<name>' in step:
   <step text>", "MissingParameter", 400)` for null/empty `[VariableName]`
   string slots. That diagnostic is gone. The architect/v5 plan's claim that
   `[IsNotNull]` would replace it is empty because no Variable slot has it.

5. **Empirical confirmation.** Wrote a transient TUnit test
   (`AuditorNullVariableVerifyTests`, removed before commit per auditor's
   read-only-on-code role) that constructed `Data.NotFound("Name")` and
   `new Data("Name", null)`, called `.As<Variable>(ctx)`, and asserted
   `Success=true ∧ Value=null`. Both passed. Also pinned that
   `(string)nullVariable` throws NRE. The bug reproduces at the Data layer
   exactly as security described.

Tester counts confirmed: C# 2550/2550, plang 166/166 — both green.

## Verdict

**FAIL** — 1 major + 2 minor findings.

- **Major #1 (cross-file)** — Missing-parameter diagnostic regression: 22
  migrated handlers now throw NRE instead of returning a graceful
  `MissingParameter` ServiceError. Generator-side fix at
  `Emission/Property/Data/this.cs:EmitProperty` is ~10 lines + 1 Discovery
  flag + 1 ActionClassInfo field. Missed by codeanalyzer (file-scope review
  doesn't span the architect-vs-implementation contract gap).

- **Minor #2 (review-gap)** — security/v2 #1 cites "3 of 22 handlers" —
  actual is **0 of 22 Variable slots**. The 3 they counted carry
  `[IsNotNull]` on different properties.

- **Minor #3 (review-gap)** — tester/v7 deletion-tested the carve-out
  (load-bearing, 35 tests pinning) but didn't write a regression test for
  the missing-parameter graceful-error path. The bug shipped past the test
  suite because the deleted contract was never pinned.

Severity escalated from security's **low** to my **major** because: (a) the
regression replaces a clear domain-aware diagnostic with a stack-derived
generic StepError; (b) the architect's plan was explicit about the
[IsNotNull] safety net which then wasn't applied anywhere; (c) it
demonstrably reproduces.

## Code example — the audit assertion

The exact path I verified empirically (transient test, removed):

```csharp
[Test]
public async Task NotFoundData_AsVariable_HasNullValueAndSuccessTrue()
{
    var notFound = global::App.Data.@this.NotFound("Name");
    notFound.Context = _app.Context;

    var resolved = notFound.As<Variable>(_app.Context);

    // CRITICAL: Success=true (no error captured) even though Value=null.
    // The generated property getter only sets __resolutionError on !Success,
    // so this resolution failure is silently masked.
    await Assert.That(resolved.Success).IsTrue();      // PASS
    await Assert.That(resolved.Value is null).IsTrue(); // PASS
}
```

The fix shape — generator-side at `Emission/Property/Data/this.cs:50` (the
non-nullable, no-default branch):

```csharp
// Before
get { if (Backing == null) {
    Backing = __ResolveData("Name").As<Variable>(Context);
    if (!Backing.Success) __resolutionError = Backing;
    SetFlag = true; }
    return Backing!; }

// After (one extra clause)
get { if (Backing == null) {
    Backing = __ResolveData("Name").As<Variable>(Context);
    if (!Backing.Success) __resolutionError = Backing;
    else if (Backing.Value == null && IsRawNameResolvable)  // <-- new
        __resolutionError = Data.@this.FromError(new ServiceError(
            "Required parameter 'Name' is missing or null",
            __step, __callFrames, "MissingRequiredParameter", 400));
    SetFlag = true; }
    return Backing!; }
```

`IsRawNameResolvable` plumbs through `Discovery/this.cs` (added to the
`ActionClassInfo` flag set the same way `IsSensitive` was added for v1 #1).
Same shape closes the trap for any future T : IRawNameResolvable.

## What the previous reviewers got right vs. what they missed

- **codeanalyzer/v4** got file-level findings right (3 MINOR DRY/stale-comment,
  7 NIT including the IRawNameResolvable contract-trap concern). Missed the
  cross-spec architect-vs-implementation gap because it lives between
  files, not in any single file. Not in their charter, but worth flagging.

- **tester/v7** strongly pinned the carve-out (35-49 tests, deletion-test
  honest), caught the misnamed PLNG001 test and the variable.set
  CopyProperties C# coverage gap. Missed the missing-parameter regression
  test that would have caught the major at land time.

- **security/v2** identified the bug correctly with a precise exploit_sketch
  and the right generator-side fix proposal. Severity rating (low) is
  defensible from a security lens (no external attacker; trust boundary
  holds) but understates the contract regression. Counted [IsNotNull]
  decorators with a too-loose predicate (handler has it anywhere vs.
  Variable slot has it specifically).

## Hand-off

Recommend **coder** next. Concrete ask:

1. Plumb `IsRawNameResolvable` through `PLang.Generators/Discovery/this.cs`
   into `ActionClassInfo`, mirroring how `IsSensitive` is plumbed in v5.
2. In `PLang.Generators/Emission/Property/Data/this.cs:EmitProperty`, after
   the existing `if (!Backing.Success) __resolutionError = Backing;`, add an
   `else if (Backing.Value == null && IsRawNameResolvable)` clause that
   captures a `MissingRequiredParameter` ServiceError. Apply to the three
   property emission branches (plain non-nullable, non-nullable with
   default, nullable — the nullable case probably wants a different
   handling: nullable Data<Variable> like foreach.KeyName intentionally
   permits absence, so skip the check there).
3. Add a regression test per migrated handler asserting
   `result.Error.Key == "MissingRequiredParameter"` when the Name slot is
   missing. Bulk-parametrize via TUnit's `[Arguments]` over the 22
   handlers — should compress to one test definition.

The codeanalyzer/v4 MINOR DRY findings (TryStaticResolve helper,
IsVariableNameSlot duplication) are optional and don't block; coder can
fold them into this round if convenient.

## Files touched (this session)

- `.bot/runtime2-generator-obp/auditor/v2/plan.md`
- `.bot/runtime2-generator-obp/auditor/v2/summary.md` (this file)
- `.bot/runtime2-generator-obp/auditor/v2/result.md`
- `.bot/runtime2-generator-obp/auditor/v2/verdict.json`
- `.bot/runtime2-generator-obp/auditor/summary.md` (appended v2 line)
- `.bot/runtime2-generator-obp/auditor-report.json` (replaced — v1's lives
  in v1/result.md, v2's is the branch-shared current)
- `.bot/runtime2-generator-obp/report.json` (appended session)

No production code or tests committed. The `AuditorNullVariableVerifyTests`
file used for empirical confirmation was removed before commit per the
auditor's read-only-on-code role.
