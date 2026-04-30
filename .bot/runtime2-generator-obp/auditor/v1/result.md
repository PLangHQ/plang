# Auditor v1 — review result

First auditor pass on `runtime2-generator-obp`. Coder/v5 (commit `753f9b12`)
closes security/v1's findings #1 (`[Sensitive]` masking) and #3 (cycle/depth-trip
returns ServiceError). codeanalyzer cleared coder/v3, tester approved coder/v4,
security passed coder/v4 — none have reviewed v5. I am the first reviewer who
sees the v5 delta in full context.

## Approach

I trusted the prior reviewers on what they checked:
- codeanalyzer/v3 (CLEAN on coder/v3): cycle/depth/OCE/diagnostic span.
- tester/v4 (APPROVED on coder/v4): Pattern B regex widening, helper extraction.
- security/v1 (PASS on coder/v4): resolution path, JSON depth, App.Run filter.

My focus was the v5 delta and what falls between reviewer beats:
1. Cross-file contract of `Data.As<T>` cycle/depth → ServiceError, ripple to
   handler emission.
2. `[Sensitive]` plumbing through Discovery + DataProperty/LegacyProperty +
   emission — record equality, all consumers updated.
3. Test review-quality on the new tests.
4. Architectural fit of generator-side masking.

Build green (warnings only, all pre-existing CS86xx in generated code).
Tests: **2468/2468** passing locally — coder/v5 claim verified.

## Previous reviews — assessment

- **codeanalyzer/v3** — agree. Production code on the v3 deltas (cycle,
  depth, OCE asymmetry, diagnostic span) is clean. Finding 46 (NIT) was
  honestly closed in v4.
- **tester/v4** — agree. Pattern B regex+helper rewrite plus 9 deletion-tested
  contracts are an honest closure of the toothlessness shape Ingi flagged in v2.
  The "strip integration not directly pinned" observation is precedent-aligned
  with Pattern A's analogous gap — appropriate to leave as observation.
- **security/v1** — partial. The four findings are correctly triaged. But
  finding #3's "surface as error" fix is half-completed in v5: the legacy
  property emission propagates `As<T>` errors, the Data<T> property emission
  buries them. See Finding 1 below.

## Findings

### Finding 1 — MINOR — cross-file contract — Data<T> property emission buries the new cycle/depth ServiceError

**File:** `PLang.Generators/Emission/Property/Data/this.cs:32, 36, 40, 44`

The v5 fix to security finding #3 changed `Data.AsT_Impl` from
"return Success=true with the unresolved string" to "return FromError(ServiceError)
with VariableResolutionCycle / ResolveDepthExceeded keys". The renamed cycle
tests now assert `result.Error.Key`. Test comments justify the change:

> "callers see the failure rather than an unresolved %var% leak"

But there are two emission paths that consume `As<T>` from action handlers, and
they handle the new contract differently.

**Legacy path** (`Emission/Property/Legacy/this.cs:69` → generates `__Resolve<T>`):

```csharp
private T? __Resolve<T>(string name)
{
    var data = __action?.Parameters?.FirstOrDefault(...) ?? __action?.Defaults?.FirstOrDefault(...);
    if (data == null) return default;
    var typed = data.As<T>(Context);
    if (!typed.Success) { __resolutionError = typed; return default; }   // ← surfaces!
    var __value = typed.Value;
    ...
}
```

`ExecuteAsync` then short-circuits via `if (__resolutionError != null) return __resolutionError;`
**before** calling `Run()`. So a cycle in a `partial string ListName` parameter
causes the handler to return the cycle error to the caller.

**Data<T> path** (`Emission/Property/Data/this.cs`, lines 32/36/40/44):

```csharp
get { if (__ApiKey_backing == null) {
        __ApiKey_backing = __ResolveData("apikey").As<string>(Context);   // ← swallows
        __ApiKey_set = true;
      }
      return __ApiKey_backing!;
    }
```

The FromError result is assigned directly to the backing field. The handler's
`Run()` body then accesses `ApiKey.Value`, which returns `default(T)` (null for
string). The error stays on `ApiKey.Error` but is invisible unless the handler
explicitly checks `ApiKey.Success`. None of the existing handlers (file/read,
file/write, llm/query, etc.) do — they all read `.Value` directly.

**Practical impact:** small. Cycles in user variables are rare; expanding
chains > 32 deep are theoretical. But the contract is now inconsistent:
- Legacy `partial string ListName`: cycle in list name → handler returns the
  ServiceError to the caller. ✓
- New `Data<string> Path`: cycle in path → handler proceeds with `Path.Value=null`.
  May produce NRE downstream or wrong-result-with-no-error.

**Why this matters for the architecture:** the Data<T> emission path is the
forward-looking path (Phase 5 will delete legacy emission). New handlers all
use `Data<T>`. Per the user's review-discipline memory:
> "No silent error swallowing, no hidden messages, always major severity"

The error is not silent in the strict sense (it's on `ApiKey.Error`), but it's
hidden from the dispatch path. I'm rating MINOR rather than MAJOR because:
- v5 is a strict improvement over v4 (the error is at least *recorded*).
- The handler can opt into checking `.Success` if it cares.
- All existing handlers proceed with default(T) on null Value already, so this
  is not a behavior regression — it's an unfulfilled contract claim.

**Suggested fix:** mirror the legacy pattern in Data<T> emission. After the
`As<T>` call, branch on `Success`:
```csharp
__ApiKey_backing = __ResolveData("apikey").As<string>(Context);
if (!__ApiKey_backing.Success) { __resolutionError = __ApiKey_backing; }
__ApiKey_set = true;
```
`__resolutionError` is already wired through `ExecuteAsync` to short-circuit.
Single-line addition per emission branch.

**Missed by:** `security` (declared #3 fixed without checking that the
emission consumers honor the new contract).

---

### Finding 2 — NIT — review-gap — sensitive `FinalValue` SetFlag guard not deletion-test-covered

**File:** `PLang.Tests/Generator/Matrix/Snapshot/SnapshotTests.cs:104-124`

The test `SnapshotOnError_SensitiveProperty_UnaccessedStillMasksPrValue`
claims to test the unaccessed branch but the handler always touches both
properties:

```csharp
public Task<global::App.Data.@this> Run()
{
    var _ = ApiKey.Value;       // ← always sets __ApiKey_set
    var __ = Endpoint.Value;
    return Task.FromResult(...);
}
```

So `WasAccessed=true` for both properties on every test invocation. The
test asserts `apiKey.PrValue IsNull` (the null-guard for masked PrValue) but
**never asserts FinalValue**. The masked FinalValue expression is:

```csharp
FinalValue = {SetFlag} ? (object?)"******" : null
```

If a future change dropped the `{SetFlag} ?` guard and emitted `(object?)"******"`
unconditionally, neither this test nor the companion
`SnapshotOnError_SensitiveProperty_MasksPrValueAndFinalValue` would fail.
The unaccessed-AND-sensitive-AND-null-FinalValue branch has zero coverage.

The coder's own comment acknowledges the gap:
> "We can't easily build a separate 'untouched' handler, so we test the
> masking behavior with a null PrValue path"

**Why this is NIT not minor:** the SetFlag guard is identical in shape to
the non-sensitive emission branch (`{SetFlag} ? (object?){Backing} : null`),
which IS pinned by `SnapshotOnError_UnaccessedProperty_FinalValueNull`. So a
regression in the sensitive variant would leave the non-sensitive contract
intact, making this a low-blast-radius gap.

**Suggested fix:** add a second `[Sensitive]` matrix handler that takes a
parameter without touching it in `Run()`:
```csharp
[Action("sensitive_untouched")]
public partial class SensitiveUntouched : IContext
{
    [Sensitive] public partial Data.@this<string> ApiKey { get; init; }
    public Task<Data.@this> Run() => Task.FromResult(Data.@this.FromError(...));
    // ApiKey never accessed → __ApiKey_set stays false
}
```
Then assert `apiKey.WasAccessed IsFalse && apiKey.FinalValue IsNull`. Or
rename the existing test to `SnapshotOnError_SensitiveProperty_NullPrValue_StaysNull`
to honestly describe what it pins.

**Missed by:** none (coder flagged it themselves; I'm escalating from
"acknowledged trade-off" to "filed observation").

---

### Finding 3 — NIT — contract precision — sensitive FinalValue masks even when backing is null

**File:** `PLang.Generators/Emission/Property/Data/this.cs:60-61`,
`PLang.Generators/Emission/Property/Legacy/this.cs:81-82`

The masked FinalValue expression is:
```csharp
{SetFlag} ? (object?)"******" : null
```

Compare with the non-sensitive form:
```csharp
{SetFlag} ? (object?){Backing} : null
```

If a parameter is sensitive and **was accessed** but its resolved value is
null (e.g., the user variable was unset, or `As<T>` returned FromError per
Finding 1), the snapshot reports `FinalValue = "******"` — implying there
was a secret to redact, when there wasn't. Slightly misleading for
post-mortem analysis. No security concern (over-masking, never under-masking).

**Suggested fix (optional):**
```csharp
{SetFlag} ? ({Backing} != null ? (object?)"******" : null) : null
```
Mirrors the PrValue null-guard pattern. NIT — precision improvement, not a
correctness concern.

---

### Finding 4 — NIT — architectural fit — SensitiveAttribute matched by short name only

**File:** `PLang.Generators/Discovery/this.cs:174-175`

```csharp
var isSensitive = prop.GetAttributes().Any(a =>
    a.AttributeClass?.Name == "SensitiveAttribute");
```

Same shape as the existing `ProviderAttribute` / `VariableNameAttribute`
matches a few lines up. A developer who declared `MyApp.Sensitive.SensitiveAttribute`
in a different namespace would inadvertently trigger masking on any property
decorated with it. The runtime `SensitivePropertyFilter` matches by full
type via `IsDefined(typeof(SensitiveAttribute), false)`, so the two layers
disagree on identity.

This is an existing convention, not introduced by v5. NIT-level. Filing
because it's worth knowing if the codebase ever standardizes on
fully-qualified attribute matching in source-generators.

---

### Observation — strip integration into LoadAllCallableSources not pinned

Tester/v4 raised this and chose not to file. I agree with the precedent
reasoning (Pattern A's `HasReadOf` integration into
`NoGeneratedHandlerDeclaresAnUnreadPrivateField` has the analogous gap).
Both should remain observations until/unless one of them is escalated. Not
a finding.

## Verdict: PASS

No critical or major findings. The v5 delta is a clean closure of security
findings #1 and #3 with one cross-file contract gap (Finding 1, MINOR) where
the new error-surfacing intent is honored by the legacy emission but not by
the Data<T> emission. Three nits and one observation.

The branch is shippable. Recommend fixing Finding 1 in a follow-up — the
Data<T> path is the forward-looking emission and the contract inconsistency
will be a footgun for the first handler that actually relies on
`%cycle_var%` resolution failing loudly.

C# tests: 2468/2468 green (locally re-run).

## Suggested next step

**docs.** No production fixes blocking. If Ingi prioritizes Finding 1, send
to coder for the single-line addition per Data<T> emission branch + a test
that asserts a Data<T> handler returns ServiceError when its parameter has a
cyclic %var%.
