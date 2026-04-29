# Auditor v1 — runtime2-builder-bootstrap

Branch: `runtime2-builder-bootstrap`
Date: 2026-04-29
Scope: Cross-cutting integrity audit. Three prior reviewers said pass.

## TL;DR

**Verdict: PASS** with 3 minor + 2 nit findings. None tip the audit.

The branch is solid. Codeanalyzer v1+v3 surfaced 18 file-level findings across 167 changed files; v2/v4 verified 13 of 14 closures behaviorally and escalated 1 carryover (the locale format-side asymmetry). Tester v4 mutation-tested 4 representative regressions, all 4 bit. Security v1 enumerated 6 findings (2 medium, 4 low) and gave PASS. The original 3 gaps from `coder/v1/report.md` (variable.set AsDefault, file.read ResolveVariables, single→list auto-wrap in TypeConverter) are all implemented and have tests.

The auditor's contribution is in five gaps:

1. **Verified codeanalyzer v4's escalated locale carryover is closed.** Commit `cc8e638d` (post-v4) made FormatValue, FluidProvider.FormatFormalValue, and ExampleRenderer all use `CultureInfo.InvariantCulture`. The asymmetry codeanalyzer v4 escalated is gone.

2. **Found the BuildingGuard removal commit's rationale references a guard that doesn't exist.** Commit `4633674c` justifies the removal by claiming `Build.IsEnabled` is enforced at "the file provider .pr write guard". DefaultFileProvider.Save has no such guard. Security F6 accepted the architectural choice as documented; the documentation it references is wrong.

3. **Categorized the 23 PLang F4 reds.** Tester carried these as a single bucket. ~6 are foundation-shaped (iteration/condition/context regressions); ~17 are module-domain (mostly missing-rebuild artifacts). The split matters for follow-up prioritization.

4. **Noted security F2's leak-path enumeration missed FluidProvider:94.** Security listed AssertionError.Variables and Error verbose dump. UI template rendering is the broader production surface and isn't enumerated.

5. **Confirmed Debug.Apply idempotency guard has zero test coverage.** Codeanalyzer v4 noted this; auditor confirms via direct grep — DebugSmokeTests calls Apply() once.

## Per-finding detail

### F1 — BuildingGuard rationale inaccurate (minor, architectural)

**File:** `PLang/App/modules/file/providers/DefaultFileProvider.cs:80` (Save method)
**Commit:** `4633674c` "Remove BuildingGuardTests — guard intentionally not restored"

Commit message:

> Building.IsEnabled property is still used by other layers (Variables resolution short-circuit, **file provider .pr write guard**, Actor setup gating, App shutdown) — those uses stay. The per-action guard added no value beyond what those layers already enforce.

Verification by `grep`:

```
PLang/Executor.cs:76                                    engine.Build.IsEnabled = true;
PLang/App/this.cs:397                                   if (Build.IsEnabled)
PLang/App/Variables/this.cs:480                         // BUT: when the app is in builder mode...
PLang/App/Actor/this.cs:120                             if (App.Build.IsEnabled && this != App.System)
PLang/App/modules/file/providers/DefaultFileProvider.cs:21    if (action.Context.App.Build.IsEnabled && path.Extension == ".pr")
PLang/App/modules/file/providers/DefaultFileProvider.cs:56    if (action.Context.App.Build.IsEnabled && path.Extension == ".pr")
```

Both `.pr`-extension checks in DefaultFileProvider are on the **Read** path:
- Line 21: snapshot retrieval
- Line 56: snapshot creation

The `Save` method (line 80-112) writes any value the caller hands it with no `Build.IsEnabled` or extension-specific gate. So `builder.goalsSave` invoking `file.Save` with a `.pr` path at runtime works. The "file provider .pr write guard" cited in the commit message does not exist.

**Why this matters:** The recorded threat-model statement is misleading. A future contributor reading the commit will look for a `.pr` write guard at the file-provider layer and not find one. Security F6 accepted the architectural choice "as documented" — but the documentation is inaccurate.

**Severity: minor.** No new exploitable bug — the actual posture is what it is, and security's F6 evaluation (low, "user installed and signed, that's the trust boundary") still holds. The issue is doc/comm accuracy in version-control history.

**Suggestion:**

Pick one:

(a) Update the threat-model docs to state the actual posture: *"No per-action guard, no extension-specific guard at the file provider. Signed runtime goals can mutate sibling .pr files via builder.goalsSave + file.Save; the signature applies to the .pr as installed, not to the .pr currently on disk. This is on the user via signature trust — they signed the goal that uses this capability."*

(b) Add the guard the commit message references — single line in DefaultFileProvider.Save:

```csharp
if (path.Extension == ".pr" && !action.Context.App.Build.IsEnabled)
    return Data.@this.FromError(new ServiceError(
        "Cannot write .pr files outside build mode", "BuildOnlyWrite", 403));
```

This restores the explicit gate the commit message describes. Note that this would also block the `goalsSave` path during runtime, which may not be desirable if there's any legitimate runtime use case. Decision required.

### F2 — Step.Clone() partial-deferral hides regression risk (minor, review-gap)

**File:** `PLang/App/Goals/Goal/Steps/Step/this.cs:183`
**Test:** `PLang.Tests/App/Modules/modifier/ModifierRegistryTests.cs:78`

Codeanalyzer v3 #3 flagged Step.Clone() missing 7 properties (PriorText, Guidance, Level, Confidence, Formal, Source, Keep — all new this branch). Coder explicitly deferred. Codeanalyzer v4 marked the deferral as defensible because of zero production callers.

The auditor's escalation: the deferral is defensible **today** but creates a landmine. The only test (`StepClone_ClonesActionModifiers`) asserts modifier-copy behavior:

```csharp
await Assert.That(clone.Actions[0].Modifiers.Count).IsEqualTo(1);
await Assert.That(clone.Actions[0].Modifiers[0].Module).IsEqualTo("cache");
await Assert.That(clone.Actions[0].Modifiers).IsNotSameReferenceAs(step.Actions[0].Modifiers);
```

The new properties aren't in the assertions. So a future test or production caller that adds `step.Guidance = "..."` and then expects `clone.Guidance` to round-trip will silently get null with no test failure. This is the "Clone/copy family" memory pattern (review-pattern #19) — the pattern's recurrence on this branch is the third instance (after Context.Clone/CreateChild and Variables.Clone).

**Severity: minor** — latent until a future caller activates it. Today the method is dead weight.

**Suggestion:**

Pick one (today's middle-ground is the worst option):

(a) **Delete Step.Clone() outright.** Zero production callers verified. Drop 30 lines + the modifier test. Future need can construct a fresh step. Pure deletion-test win.

(b) **Fix Step.Clone() to copy all 18 properties** + add a reflection-based test that fails when a new property isn't propagated:

```csharp
[Test]
public async Task StepClone_CopiesAllProperties()
{
    var step = new Step { /* set every public property to a non-default */ };
    var clone = step.Clone();
    foreach (var prop in typeof(Step).GetProperties()
        .Where(p => p.CanWrite && !IsComputed(p)))
    {
        await Assert.That(prop.GetValue(clone)).IsEqualTo(prop.GetValue(step));
    }
}
```

(c) **Mark `[Obsolete("Use construction or fix to copy all properties — see auditor v1 F2")]`** so any new caller fails at compile time and forces a decision.

### F3 — F4 cluster homogeneous-bucket categorization (minor, review-gap)

**File:** various PLang test goals (see issue text).

Tester v1-v4 carried F4 as a single bucket: 23 reds + 4 stale, "scope-out, separate-branch work, accepted by coder v1 explicit scope." That framing treats the cluster as homogeneous. Categorizing by failure shape:

**Foundation-shaped (~6 reds):**
| Test | Failure |
|---|---|
| `Tests/Builder/ForeachCallsGoalPerItem.test.goal` | `Expected: 3, Actual: 2` — iteration count |
| `Tests/Modules/Loop/Foreach/Dictionary/ForeachDictionary.test.goal` | `Expected: 3, Actual: 1` — dict iteration |
| `Tests/Modules/Condition/Compound/Mixed/ConditionCompound.test.goal` | `Expected: "yes", Actual: (null)` |
| `Tests/Modules/Variable/ContextVars/Basic/ContextVars.test.goal` | `engine name should be set → null` |
| `Tests/App/SetupGoal/Start.test.goal` | `Expected: True, Actual: (null)` |
| `Tests/Modules/Error/Types/ErrorTypes.test.goal` | `Expected: "TestKey", Actual: (null)` |

These look like real iteration / condition / context regressions, not missing rebuilds.

**Module-domain (~17 reds):**

| Group | Count | Common signature |
|---|---|---|
| Signing | 8 | "Contract mismatch", "File not found: .build/sign.pr", "Action 'timeout.after.after' not found" |
| Identity | 2 | Default identity not surfaced |
| Crypto | 1 | "Algorithm 'bcrypt' is not supported" |
| Ui | 2 | Render-with-params |
| Event | 3 | Remove/Override/Priority |
| Test/Discover | 1 | Stale-when-pr-missing |

Several of these (Signing's "File not found .build/sign.pr") are likely missing-rebuild artifacts that resolve when the modules' `.pr` are regenerated.

**Why this matters:** The follow-up branch can rebuild modules and verify Signing/Identity/Crypto/Ui/Event in one sweep, but the foundation reds need code investigation. Without splitting, the follow-up could "fix the easy ones" and leave the foreach/condition/contextVars regressions as a slow leak.

**Severity: minor** — all known, all carried over, none surface as production failures today (the failing tests are the only signal).

**Suggestion:**

When handing F4 to the next branch, split into two priority tiers:

- **Tier A (foundation, code investigation):** Foreach call counts; Foreach over Dictionary; Condition Compound; ContextVars; SetupGoal initialization; Error Type round-trip.
- **Tier B (module-domain, mostly missing-rebuild):** Signing 8, Identity 2, Crypto 1, Ui 2, Event 3, Test/Discover 1. Verify by checking if `.build/sign.pr` etc. exist before debugging.

Tier A blocks; Tier B is a rebuild sweep.

### F4 — Variables.GetAll() Fluid leak path not enumerated by security (nit, review-gap)

**File:** `PLang/App/modules/ui/providers/FluidProvider.cs:94`

```csharp
foreach (var kvp in action.Context.Variables.GetAll())
{
    fluidContext.SetValue(kvp.Key, FluidValue.Create(kvp.Value.Value, options));
}
```

Security F2 enumerated leak surfaces for `Variables.GetAll()`:

> Affected files:
> - PLang/App/Variables/this.cs
> - PLang/App/modules/assert/AssertSnapshot.cs
> - PLang/App/Errors/Error.cs

It missed `PLang/App/modules/ui/providers/FluidProvider.cs:94`. UI templates are the broadest production surface — a user template like `<pre>{{ apiKey }}</pre>` (where `apiKey` was loaded from settings into a `%apiKey%` variable) renders the cleartext secret to the HTML response. This is standing pre-branch behavior — not introduced here — so it isn't a regression.

**Why this matters:** Security's proposed fix (Sensitive flag on `Data`, honored by every snapshot/serialization path) must be honored by **Fluid template rendering** as well, not just JSON-serialization paths. The auditor's note ensures that's documented when the fix lands.

**Severity: nit.** Standing behavior, not a regression. Doc/threat-model completeness.

**Suggestion:** Add `PLang/App/modules/ui/providers/FluidProvider.cs:94` to security F2's `affected_files`. When the proposed Data.Sensitive flag is implemented, ensure FluidValue.Create (or a wrapper) honors it.

### F5 — Debug.Apply `_applied` guard zero test coverage (nit, contract)

**File:** `PLang/App/Debug/this.cs:80`
**Test:** `PLang.Tests/App/Testing/DebugSmokeTests.cs` (calls Apply once)

Codeanalyzer v3 #6 flagged that Debug.Apply was not idempotent (every call double-subscribed events). Coder added `_applied` bool guard at line 80 + early-return at line 120-121. Codeanalyzer v4 verified the fix and noted "no current test bites." Auditor confirmed by direct grep: DebugSmokeTests calls Apply once.

The fix is correct. The guard is defensive — single caller today (Executor.cs:53) — but the regression it prevents (double subscription) is exactly the kind of bug a future contributor could re-introduce by removing the guard "for cleanup," with no test failure.

**Severity: nit.** Defensible per coder; auditor flags for future awareness.

**Suggestion:**

Either:

(a) **Add a deletion-test** that pins the guarantee:

```csharp
[Test]
public async Task Apply_CalledTwice_DoesNotDoubleSubscribe()
{
    _app.Debug.Apply(new Dictionary<string, object?> { ["level"] = "action" });
    _app.Debug.Apply(new Dictionary<string, object?> { ["level"] = "action" });
    // Run an action; assert handler ran exactly once, not twice.
}
```

(b) **Add a grep-findable comment** on the field:

```csharp
// MUST stay — see Debug.Apply idempotency guard.
// Removing this would re-introduce double-subscription on every re-call.
private bool _applied;
```

## What I verified, and didn't re-check

### Verified

- **Codeanalyzer v4's escalated locale carryover is closed.** Commit `cc8e638d` made the format side InvariantCulture at all three sites: `DefaultBuilderProvider.FormatValue:445`, `FluidProvider.FormatFormalValue:143`, `ExampleRenderer:108`. Confirmed by grep.
- **Original 3 coder gaps are implemented and tested.** AsDefault on variable.set (settests.cs:65, 85), ResolveVariables on file.read (FileHandlerTests.cs:101, 119), single→list auto-wrap in TypeConverter (line 156-167). Bonus: file.read uses `skipInfrastructure: true` — security improvement not in original spec, with a dedicated test (`Read_ResolveVariablesTrue_BlocksInfrastructureVariables`).
- **Security F1 — ParamSnapshot bypasses [Sensitive] — is real.** Verified at `LazyParamsGenerator.cs:701, 703` (PrValue/FinalValue emit with no [Sensitive] check) → `Error.cs:215, 220` (FormatVerboseValue → unfiltered). The fix needs both a generator-side mask AND the format-side filter.
- **NormalizeParameterTypes error round-trip surfaces correctly.** Re-traced the path codeanalyzer v4 already documented: `NormalizeParameterTypes` (line 582) → `validationErrors` (line 240) → `BuildValidation ActionError` (line 327+). The trace is sound.
- **IsCatalogDescription has TWO production callers** (lines 263, 616), not one. The tester's coverage analysis covered the helper itself; both call paths are correct (line 263 is for goal.call CLR-name guard, line 616 is for parameter-type normalization).

### Not re-checked

- Per-file OBP analysis (covered by codeanalyzer v1+v3+v4 across 167 files).
- Per-line coverage of `IsCatalogDescription` and math `ExamplesForLlm` (covered by tester v4 mutation tests).
- Security F1/F3/F4/F5 attack-surface enumeration (covered by security v1, mostly correct).

## Verdict rationale

The character file says "fail (any critical/major findings)". I have 3 minor + 2 nits, no critical or major. Verdict: PASS.

The judgment call I considered: should F1 (BuildingGuard rationale inaccuracy) be major because it concerns a security boundary? I decided **minor** because:

1. The actual code posture matches what security accepted (low severity, "user installed and signed").
2. The issue is documentation accuracy in version-control history, not a new exploitable bug.
3. Per memory's "rate for intended purpose, not current callers" — the intended posture is what security signed off on; the documentation just doesn't capture it accurately.

If Ingi disagrees and pushes severity up, I'll defer to that — but I'd rather flag clearly and let the user decide than auto-rate-down a documentation issue.

## Recommend next: docs

Per character file: pass → suggest the **docs** bot next. The two minor findings worth recording in branch docs:

- The actual BuildingGuard threat-model posture (no .pr write guard, signed goals can mutate siblings).
- The Step.Clone deferral as a known landmine for future contributors.

The original 3 gaps (variable.set AsDefault, file.read ResolveVariables, single→list auto-wrap) deserve dedicated developer-facing documentation since they're new public-facing capabilities.
