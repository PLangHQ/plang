# Tester v2 — singular-namespaces — Findings

**Verdict: FAIL** (one MAJOR false-green, surgical to fix).

Coder v2 is genuinely good work: 6 of my 7 v1 findings are fixed *honestly* — not papered
over. The suite is green for real on the renamed/sentinel/non-null reshape. But the **one new
behavior the coder added this version** — the `type.Promote()` fail-loud throw (commit
`3c1521c20`, the headline of plan F1) — has **zero test coverage**, and the test *named* for
it (`...ThrowsHard_NoSilentFallback`) asserts the opposite (a silent null return on a different
property). A reader scanning green test names believes the throw is verified. It isn't.

Per my standing rule (strict-red: any confirmed false-green = FAIL), this is a FAIL. The fix is
one test. Everything else is clean.

---

## Ground-truth runs (clean rebuild — stale-binary trap avoided)

| Suite | Result | Note |
|---|---|---|
| Clean build (`PlangConsole`) | 0 errors, 254 warnings (all pre-existing CS86xx) | rebuilt from `rm -rf bin/obj` |
| C# (`dotnet run --project PLang.Tests`) ×4 | **3694 / 3694**, 0 failed, every run | F6 flake gone |
| PLang (`cd Tests && plang --test`) | **253 / 253 pass**, 0 fail | branch tests all `[Pass]` |

The F6 flake (`BuilderValidate_CallsBuildOnEachAction_InOrder` racing on a shared `static`
`InvocationLog`) is fixed by `[NotInParallel]` — **4 consecutive clean runs** confirm it.
The `builder.validate: Failed to deserialize` line in PLang output is the same benign
mock-fixture diagnostic from v1; it is not a test failure (`253 pass, 0 fail`).

---

## Findings

### F1-RESIDUAL — MAJOR — the new `Promote()` throw is uncovered; its test verifies the opposite
**code:** `PLang/app/type/this.cs:168-174` · **test:** `NullabilityTests/NonNullInvariantTests.cs:23`

Coder v2's headline F1 addition (commit `3c1521c20`): `type.@this.Promote()` **throws**
`InvalidOperationException` when an unstamped (Context==null) non-primitive entity has its
schema properties (`Fields`/`Values`/`Shape`/…) read — converting a silent empty-fold footgun
into a fail-loud producer-bug signal. codeanalyzer v4 praised this as "fail-loud-at-source."

**It has no test.** Deletion test (announced, reverted, `git status` clean):

> Replaced the `throw` at `type/this.cs:168-174` with `return this;` → **3694/3694 still pass.**

Removing the entire fail-loud contract breaks nothing. A future refactor can silently restore
the old footgun and the suite stays green.

Worse, the test that *looks* like its coverage is a misdirection:

```csharp
[Test] public async Task DataType_OnUnstampedData_ThrowsHard_NoSilentFallback()
{
    var d = new app.data.@this<int>("", 0, new app.type.@this("not-a-primitive-domain-name"));
    await Assert.That(d.Type!.ClrType).IsNull();   // ← no throw; and ClrType ≠ the property that throws
}
```

- The name says **ThrowsHard / NoSilentFallback**; the body asserts a **silent null return**.
- It reads **`ClrType`** — which is `_clrType ?? Context?…​ ?? GetPrimitiveOrMime` and *never*
  calls `Promote()`. The throw lives on `Fields`/`Values`/`Shape`/`Example`/etc. So the test
  exercises a different code path than the one it's named for.

**Fix:** add a test that reads a fold property on an unstamped non-primitive entity and asserts
the throw — e.g.
```csharp
var t = new app.type.@this("not-a-primitive-domain-name"); // no Context
await Assert.That(() => _ = t.Fields).Throws<System.InvalidOperationException>();
```
and either rename `…ThrowsHard_NoSilentFallback` to describe what it really pins
(`ClrType_OnUnstampedDomainType_ReturnsNull`) or fold it into the throw test. Both inputs
deletion-confirmed today, so both lines of the throw block must be exercised.

### F8-RESIDUAL — MINOR (process) — no `baseline-tests.md` for v2
`coder/v2/` has no `baseline-tests.md` (v1 had none either). I can still separate regression
from pre-existing because the full suite is green, but the artifact is required by the coder
workflow. Flagging per process; not a code finding.

### N1 — MINOR — `GetPrimitiveOrMime_ExternalFallbackCallSites_AllRemoved` can pass vacuously
`NonNullInvariantTests.cs:57-78`. The source-grep pin does `if (!File.Exists(path)) continue;`
— if the `BaseDirectory → repo-root` relative walk ever breaks (CI layout change, runner move),
**both files are skipped and the test asserts nothing → green**. It resolves correctly today
(verified: `repo/PLang/app/data/this.cs` exists), so it's a real pin *now*, but the silent-skip
makes it fragile. Suggest asserting `File.Exists` (fail if the guard file can't be found)
rather than `continue`.

### N2 — MINOR — F7 channel-accessor test pins reachability, not value-flow
`Tests/SingularNamespaces/ChannelWriteThroughAccessor/` (Start + Capture). Strictly better than
v1's no-assertion smoke test — it now pins that the accessor `Write` path **reaches handler
code** (`Capture` must run for `%captured%` to be set; deletion of the accessor write path →
`%captured%` null → assert fails). Good. But `Capture.goal` does
`set %captured% = "hello from channel accessor"` — a **hardcoded literal identical to the
written value**, not an echo of the input. So if the channel delivered the *wrong* data,
`%captured%` would still equal the expected string and the test would pass. Value-flow through
the channel is untested. To close: have `Capture` echo whatever the write delivers (e.g. set
`%captured%` from the channel payload variable) so a corrupted/dropped value diverges.

---

## What's honestly fixed (credit where due)

- **F2 golden → real gate.** SHA256 of `schema.ToJson(indent:false)` **and** `schema.TypeSchemas`,
  pinned to constants, with `>1000` / `>100` length guards so a hash-of-empty can't pass. Delete
  `BuildTypeEntries` and the schema SHA diverges → fail. This is the gate the architect spec'd.
- **F3 → paths now distinguishable.** Switched `int` → `path`: an explicit guard asserts
  `GetPrimitiveOrMime("path") IsNull()`, then asserts the stamped read resolves to
  `app.type.path.@this` (namespace + name). The registry and static-fallback answers now
  genuinely diverge, so the `_NotStaticFallback` claim is verified.
- **F4 → reads the entity for real.** `set %name% = "alice", type=text/plain` then
  `assert %name!Type% equals "text/plain"`. `.pr` confirmed:
  `variable.set(...Type="text/plain")` + `assert.equals(Expected="text/plain", Actual=%name!Type%)`.
  The `!Type` navigator forces the value through the type entity. Honest.
- **F5 → typed error, real index-miss.** `.pr` confirmed: `output.write(Data="ping",
  channel="absent-channel-xyz")` — channel is a **literal** (type `channel`), not an unset
  variable; on-error captures `%!error.Key%`; asserts `equals 'ChannelNotFound'`. Tests the
  registry index-miss with the *typed* key, not "any error." Honest.
- **F6 → flake fixed.** `[NotInParallel]` on `Stage0_BuildMethodTests`; 4 consecutive clean
  C# runs (the v1 race produced "0 items but expected 3" on ~1-in-3 runs).
- **Builder false-green sweep clean.** Every branch `.pr` step `text` semantically matches its
  `actions[0]` module/action. No action-index drift.

---

## Coverage

See `coverage.json`. The new surfaces show high line% — but note the **coverage-dazzle trap**
this version proves directly: `type/this.cs` Promote() executes in many stamped-path tests
(line "covered"), yet the **Context==null throw branch is behaviorally unverified** (deletion
test: removing it changes nothing). Executed ≠ verified — F1-RESIDUAL is exactly this gap, and
line coverage would have hidden it.
