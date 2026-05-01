# tester v2 — result

Reviewed: coder/v2 commit `24cba238`, after codeanalyzer/v3 PASS at `ae827527`.

## Test runs

### C# (`dotnet run --project PLang.Tests`)
```
total: 2539
failed: 9
succeeded: 2530
```
The 9 failures are exactly the deferred Phase 5b/5c/6 stubs (4 `ListAddIdentityTests` + 5 `Plng001PostMigrationTests`), all `Assert.Fail("Not implemented")`. ✓ Matches coder's claim 2530/2539.

### PLang (`plang --test`)
```
166 total, 166 pass, 0 fail, 0 timeout, 0 stale, 0 skipped
```
✓ Matches coder's claim 166/166. The lowercase `tests/modifiers/` dir is gone (coder v2 cleanup), which incidentally closes v1 finding #7.

## Coverage on coder/v2 touched code

| File | Coverage | Notes |
|---|---|---|
| `PLang/App/Data/this.cs` | 87.4% | Was 86.4% pre-v2 — small uptick from new tests |
| `PLang/App/Utils/TypeConverter.cs` | 57.8% | Many pre-existing untouched paths |
| AsCanonical container branch (L487–495) | **9/9 = 100%** | Pinned by Rules 4c/4d/4e/4f |
| IsWalkableContainer / WalkContainerVars | 4/5 (L517 unreachable) | L517 is `return raw;` fallback gated by `IsWalkableContainer` callers — defensive |
| TypeConverter JsonArray arm (L129–137) | **9/9 = 100%** | Pinned by `JsonArrayToListOfClass` |
| TypeConverter JsonNode dispatch (L354–356) | **3/3 = 100%** | Pinned by both new tests |

**Coverage on the new code is solid.** The new tests do exercise each production line.

## Deletion-test results

| Production change | Tests that pin it | Result |
|---|---|---|
| AsCanonical container walk (L487–495 stripped) | 4c, 4d, 4e | ✅ 3 fail |
| AsCanonical container walk — full removal | 4c, 4d, 4e | ✅ 3 fail |
| **AsCanonical state-aliasing (L491–494)** — Properties, OnCreate, OnChange, OnDelete | (none) | ❌ **0 fail** — false-green |
| Partial-interp state-aliasing (L476–479, pre-existing) | (none) | ❌ **0 fail** — pre-existing false-green |
| TypeConverter JsonNode dispatch | both new tests | ✅ 2 fail |
| TypeConverter JsonArray arm | `JsonArrayToListOfClass` | ✅ 1 fail |
| Rule 4f (LiteralList) walk | (claims to mirror typed walk) | ❌ does NOT pin walk — values pass through identically without WalkList |

## Findings

### 1. (major) State-aliasing on AsCanonical container branch is unpinned — false-green

**File:** `PLang.Tests/App/DataTests/AsTIdentityTests.cs`
**Code:** `PLang/App/Data/this.cs:491–494`

Codeanalyzer/v3 flagged this as a test gap; **deletion test confirms it's a real false-green.** I removed all four alias lines (`transient.Properties = Properties; transient.OnCreate = OnCreate; transient.OnChange = OnChange; transient.OnDelete = OnDelete;`) on the AsCanonical container branch and ran the full `AsTIdentityTests` class — 14/14 still pass green.

The comment on Rule 4c claims AsCanonical returns "a fresh Data… (since the container is rewritten with resolved values)" but the assertions only check `ReferenceEquals(canonical, paramData) == false` and resolved value content. They never check that `canonical.Properties` / `canonical.OnChange` is `ReferenceEquals` to `paramData.Properties` / `paramData.OnChange`.

**Impact:** A future regression that drops the alias lines on the container branch silently breaks event-subscriber survival for any consumer that observes a list/dict slot through plain `Data`. The architect's identity-share-by-reference contract is unprotected on this branch.

**Suggestion:** add one test on each AsCanonical container path:
```csharp
[Test]
public async Task AsT_PlainDataTarget_ListWithNestedVars_StateAliasedOnTransient()
{
    var ctx = _app.User.Context;
    ctx.Variables.Set("greeting", "hello");
    var raw = new List<object?> { "%greeting%" };
    var paramData = new Data("Slot", raw) { Context = ctx };
    paramData.Properties.Set("note", "via-source");

    var canonical = paramData.AsCanonical();

    // Properties is ref-shared with source paramData.
    await Assert.That(ReferenceEquals(paramData.Properties, canonical.Properties)).IsTrue();
    await Assert.That(canonical.Properties["note"]!.Value).IsEqualTo("via-source");
    // Event lists too — pin OnChange specifically.
    await Assert.That(ReferenceEquals(paramData.OnChange, canonical.OnChange)).IsTrue();
}
```

A symmetric test for the dict case is good hygiene but the list case is enough to fail the deletion.

### 2. (minor) Pre-existing false-green: state-aliasing on partial-interpolation branch

**File:** `PLang.Tests/App/DataTests/` (no test exists)
**Code:** `PLang/App/Data/this.cs:476–479`

Same shape as #1 but on the partial-interpolation branch (introduced before coder/v2). Deletion test on those four lines also yields 14/14 green. Pre-existing — not introduced or worsened by coder/v2 — but flagged here because adding the test from #1 would naturally close this too if generalized to a `"prefix-%var%-suffix"` partial input.

**Suggestion:** the test in #1 plus one for partial interpolation, e.g. `paramData.Value = "Hello %name%"` with assertion that `canonical.Properties` is ref-shared.

### 3. (minor) Rule 4f (LiteralList) does not pin the walk

**File:** `PLang.Tests/App/DataTests/AsTIdentityTests.cs:245–259`
**Code:** `PLang/App/Data/this.cs:487–495`

Test comment claims "literal list (no %vars%) still walks. Symmetric with the typed path: As<T> on a List<object?> always allocates via WalkList, and AsCanonical mirrors that. Asserting on values (not ref-equality) keeps the contract focused on resolution semantics."

But asserting on values means the test passes whether or not WalkList runs — `["a","b","c"]` is the same list contents either way. If AsCanonical returned `this` for literal lists (skipping the walk), Rule 4f would still pass green. **The walk-on-literal-list contract is not actually pinned.**

**Impact:** Low. The walk-on-literal-list is largely cosmetic (the architect doesn't require it; it's an artifact of using the same WalkList helper for everything). But the test claims to assert it and doesn't.

**Suggestion:** either weaken the comment to "preserves values when no %vars% are present" (current assertion shape), or strengthen the assertion to `await Assert.That(ReferenceEquals(canonical, paramData)).IsFalse();` to actually pin "fresh Data is returned even for literal lists."

### 4. (informational) Carryover from v1 — 6 of 7 findings still open

Coder/v2 was a separate scope (LLM-builder NRE root cause) and did not address most v1 tester findings. Coverage on the unchanged files matches my v1 numbers within rounding:

| v1 finding | v1 status | After v2 | Closed by v2? |
|---|---|---|---|
| #1 MintTyped cold types (decimal/float/DateTimeOffset/Guid/byte[]/reflection) uncovered | open | set.cs 89.2% (was 89.0%) | ❌ no |
| #2 list.add complex-snapshot path uncovered | open | list/add.cs 56.1% (unchanged) | ❌ no |
| #3 Variables.Set dot-path JsonElement-shape regression unprotected | open | Variables/this.cs 90.9% (was 90.6%) | ❌ no |
| #4 Set.ValidateBuild error-message + Run() Unknown-type uncovered | open | unchanged | ❌ no |
| #5 Set_IntValue accepts Data<int> OR Data<long> | open | unchanged | ❌ no |
| #6 Replacement-NOT-fires-OnDelete unpinned | open | unchanged | ❌ no |
| #7 Stale tests/modifiers/ legacy directory | open | **deleted** | ✅ closed |

These are missing-coverage / weak-assertion findings, not regressions. The v2 work doesn't make any of them worse. They remain valid recommendations for a future coder-tester loop.

## Verdict

**Approved (pass).** Coder/v2's two bug fixes are correctly identified, root-cause, and pinned by 5 of the 6 new tests via deletion test. The new code is 100% line-covered on the changed paths.

One **major** new test gap surfaces from the deletion test (codeanalyzer/v3 also flagged it predictively): the four state-aliasing lines on the AsCanonical container-walk transient have no assertion. Verified false-green. One small test would close both this and the pre-existing partial-interp version.

Two minor items: the partial-interp branch is pre-existing unpinned; Rule 4f's "walk on literal list" claim isn't actually asserted.

Carryovers from v1 (6 of 7 still open) are out of scope for this version's review.

## Recommended next

**auditor** — production behavior is correct, tests are honest with one durable gap and one minor weakness that don't block. Adding the one missing aliasing test from finding #1 on a follow-up touch is cheap insurance; deferring is acceptable.

If Ingi wants the v1 coverage gaps closed before merge, **bounce to coder/v3** with a focused list (MintTyped cold types, list.add complex-snapshot, Variables.Set dot-path JsonElement shape, set.cs error paths, plus the new aliasing test).
