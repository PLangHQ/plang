# Tester v3 — singular-namespaces — Findings

**Verdict: PASS.** All four v2 findings fixed and **mutation-confirmed honest**. Coder v3 changed
only tests + `.bot` + rebuilt `.pr` — zero production source — so this round is purely about test
honesty, and the tests hold up under deletion.

---

## Ground-truth runs (clean rebuild — stale-binary trap avoided)

| Suite | Result | Note |
|---|---|---|
| Clean build (`PlangConsole`) | 0 errors | rebuilt from `rm -rf bin/obj` |
| C# (`dotnet run --project PLang.Tests`) | **3696 / 3696** | +2 new throw-coverage tests |
| PLang (`cd Tests && plang --test`) | **253 / 253** | see HTTP note |

**HTTP flake (not a finding):** first PLang run showed 8 `[Fail]` — all under `/Modules/Http/*`,
all `{true, false}` condition mismatches. These hit `https://httpbin.org` externally; v3 changed
zero production source; a re-run returned **253/253, 0 HTTP fails**. Confirmed transient external
unavailability, identical to the intermittent flake noted in v1/v2. Not on this branch's surface.

---

## Finding-by-finding (each fix verified, not trusted)

### F1-RESIDUAL (was MAJOR) — RESOLVED, mutation-confirmed
The headline `type.Promote()` fail-loud throw now has real coverage, and the misnamed test was
corrected:

- `DataType_OnUnstampedData_ThrowsHard_NoSilentFallback` → **renamed**
  `ClrType_OnUnstampedDomainType_ReturnsNull`. Name now matches the body (it always pinned the
  silent-null `ClrType` path, which never calls `Promote()`).
- **New** `TypeFoldRead_OnUnstampedDomainEntity_ThrowsHard` — reads `t.Fields` on an unstamped
  non-primitive entity, asserts `Throws<InvalidOperationException>`.
  - **Mutation A (announced, reverted):** replaced the `Promote()` throw with `return this;` →
    **exactly** `TypeFoldRead_OnUnstampedDomainEntity_ThrowsHard` failed (3695/3696). The test
    catches its own deletion. Honest.
- **New** `TypeFoldRead_OnPrimitiveEntity_DoesNotThrow_EvenWithoutContext` — reads
  `app.Type["string"].Example` (no App-stamped Context), asserts `ThrowsNothing`. Pins the
  `_foldLoaded = true` primitive carve-out the throw is built around.
  - **Mutation B (announced, reverted):** deleted `_foldLoaded = true` from the 2-arg ctor →
    `TypeFoldRead_OnPrimitiveEntity_DoesNotThrow_EvenWithoutContext` failed (the carve-out is
    load-bearing; 2 failures total). The test catches its own deletion. Honest.

`git status` clean after both mutations — no source committed.

### N1 (was MINOR) — RESOLVED
`GetPrimitiveOrMime_ExternalFallbackCallSites_AllRemoved` now does
`await Assert.That(File.Exists(path)).IsTrue()` instead of `if (!File.Exists) continue;`. A
broken relative-walk now goes **red** instead of passing vacuously. The structural pin can no
longer silently stop protecting.

### N2 (was MINOR) — RESOLVED
`Capture.goal` now does `set %captured% = %!data%` (echoes the channel payload). Rebuilt `.pr`
confirmed: `variable.set(Name=%captured%, Value=%!data%)`. The test now pins **value-flow**, not
just reachability — a corrupted/dropped/swapped channel payload makes `%captured%` diverge from
the hardcoded `Expected` in `Start.test.goal`, going red. Test passes green (so `%!data%` resolves
to the written value), confirming the accessor delivers the payload intact.

### N4 (process) — RESOLVED
`coder/v3/baseline-tests.md` present (3694/3694 C# + 253/253 PLang pre-edit; 3696/3696 post). The
regression/pre-existing separation is now recorded.

---

## Builder false-green sweep
All branch `.pr` step `text` semantically match `actions[0]` module/action — including the
rebuilt `capture.pr` and `start.test.pr`. No action-index drift.

## Coverage
`app/type/this.cs` rose **62.3% → 78.3% line, 33.3% → 57.1% branch** — the Promote `Context==null`
branch is now exercised AND verified (Mutation A proves it, not just line-coverage). The v2
coverage-dazzle gap (executed ≠ verified) is closed. Full table in `coverage.json`.

## Nothing outstanding
Every v2 finding is closed with a test that fails when the behavior it guards is removed. This is
the standard for honest green, and v3 meets it.
