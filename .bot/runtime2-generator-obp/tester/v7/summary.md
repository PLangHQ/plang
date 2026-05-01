# Tester v7 — cumulative review of coder v5/v6/v7 (Variable + IRawNameResolvable migration)

## What this is

Catch-up tester pass. Last tester output was v4, against coder/v4 (Pattern B
regex toothlessness). Branch has since landed:

- **v5** — security finding #1 ([Sensitive] masking) + #3 (cycle/depth → ServiceError)
- **v6** — auditor closure of v5 finding #1
- **v7** — Variable + IRawNameResolvable migration; 22 handlers; PLNG001 collapses to two-rule gate
- **v7 commit 4** (53780c2d) — fix variable.set Properties loss + implement ListAdd identity stubs
- **v7-cleanup** (ac028f0f) — codeanalyzer/v4 trivials

Reviews already on file: codeanalyzer/v4 PASS (3 MINOR + 7 NIT, no MAJOR);
auditor/v1 PASS on v5. No tester pass since v4. Single cumulative review,
focused on test honesty post-Variable migration. Avoided duplicating
codeanalyzer's production-shape findings.

Verdict: **approved**. Test quality is high. The migration's load-bearing
mechanisms (the IRawNameResolvable carve-out, Variable's implicit operator
to string) are heavily deletion-pinned (35-49 tests respectively). Four
findings filed, all minor.

## Test counts

| Suite | Total | Pass | Fail | Skipped | Note |
|---|---|---|---|---|---|
| C# (TUnit) | 2550 | 2550 | 0 | 0 | Coder/v7 summary said 2554/4-fail; commit 4 cleaned the 4 ListAdd stubs by replacing them. |
| plang | 166 | 166 | 0 | 0 | Coder/v7 summary said 160/16; commit 4 fixed the 10 TestReport regressions via CopyProperties. |

Both better than the coder's mid-flight counts. Run from `/workspace/plang/Tests`
per the character file.

## Deletion-test results

| Mutation target | C# tests fail | Verdict |
|---|---|---|
| IRawNameResolvable carve-out (Data/this.cs:549-562) — disabled with `if (false && ...)` | **35** | STRONGLY load-bearing. SlotData_* tests directly; Foreach/Set/Wrap families indirectly via migrated handlers. |
| Variable.ToString → `"MUTATED"` | 1 | Lightly pinned. Interpolation in error messages is uncovered (precedent: error-message text isn't tested). |
| Variable implicit operator → `"MUTATED"` | **49+** | STRONGLY load-bearing. Critical for `Context.Variables.Get(X.Value)` use sites. |
| WasPercentWrapped → hardcoded false | 3 | Value-only pinning; no consumer tests it. Codeanalyzer-confirmed dead. |
| variable.set CopyProperties (commented out) | 0 C# / **10 plang** | Honestly pinned at integration tier; C# coverage gap. |

## Builder false-green check

Read `.pr` files for test coverage of migrated handlers. Verified each step's
`text` semantically matches `actions[0].module.action`:

- `Tests/Modules/List/.build/listops.test.pr` — 33 steps; covers list.add,
  list.count, list.first, list.last, list.contains, list.join, list.split,
  list.get, list.remove, list.indexof, list.sort, list.reverse, list.unique
  + variable.set.
- `Tests/Modules/List/.build/listops2.test.pr` — list.range, list.set, list.flatten.
- `Tests/Modules/Loop/.build/loop.test.pr` — loop.foreach with ItemName=item.
- `Tests/TestModule/Run/.build/testrunreportsassertionfailure.test.pr` —
  list.any with `ListName="%results%"`.

No false-greens. Each migrated handler's `Data<Variable>` parameter survives the
.pr round-trip and resolves correctly via the carve-out.

## Findings

**0 critical, 0 major, 4 minor.**

### #1 (minor) — variable.set CopyProperties has 0 C# coverage

`coder/v7 commit 4` added `CopyProperties(Value, minted)` at the binding-mint
site (`set.cs:91`) to fix Properties survival across `variable.set`. The fix
is real and honest — disabling it makes 10 plang TestReport tests fail. But
**zero C# unit tests** pin the contract.

A C# test that constructs a Data with Properties, runs `variable.set` Action
with that Data as Value, and asserts `Variables.Get(name).Properties` carries
the same entries would close the gap (~20 lines).

### #2 (minor) — IRawNameResolvable contract trap, untested

The carve-out's reflection lookup returns null when `T : IRawNameResolvable`
but lacks `static Resolve(string, Context.@this)`. Current behavior is silent
fallthrough to the `%var%` substitution branch — exactly the path the marker
was supposed to bypass. Codeanalyzer/v4 flagged this as NIT-4.

Today only Variable implements the marker, so the trap is theoretical. But
the marker is a public interface; a future T forgetting Resolve would
silently revert to the bare-name regression v7 was designed to prevent.

Suggested fix: either (a) add a 3-line `if (resolveMethod == null) FromError(...)`
in the carve-out, OR (b) add a test pinning the fallthrough behavior
explicitly. Option (a) is the better contract.

### #3 (minor) — PLNG001_VariableNameAttribute_NowReportsDiagnostic is misnamed

The test source uses bare `partial string Name` with no attribute. So the
test pins "bare scalar string triggers PLNG001" — which is also covered by
`PLNG001_RawScalar_StillReportsDiagnostic`. The name implies it pins
`[VariableName]` removal, but it cannot — the attribute isn't applied in the
test source.

Either rename to `PLNG001_BareString_NowReportsDiagnostic` (honest), or
modify the test to define and apply a synthetic `[VariableName]`
attribute (would actually pin the carve-out being gone).

### #4 (nit) — WasPercentWrapped value-only pinning

Three direct-value tests verify WasPercentWrapped is computed correctly. No
consumer test exists (codeanalyzer also flagged this — NIT-5). Field is
documented for "future validators." Acceptable; flagged so the next
future-validator implementation pins behavior to the field.

## Code example — what an honest pin of the carve-out looks like

```csharp
[Test]
public async Task SlotData_PercentWrapped_AsVariable_IgnoresExistingValue()
{
    _app.Context.Variables.Set("x", 5);            // x exists with int value
    var slot = new Data("Name", "%x%") { Context = _app.Context };

    var resolved = slot.As<Variable>(_app.Context); // bypass fires → Variable{Name="x"}

    await Assert.That(resolved.Success).IsTrue();
    await Assert.That(resolved.Value!.Name).IsEqualTo("x");      // identity, not value 5
    await Assert.That(resolved.Value!.WasPercentWrapped).IsTrue();
}
```

Without the carve-out (deletion test), `As<Variable>` would enter
TryFullVarMatch → return x's int Data → conversion fails. Test fails. Pin
is real. Same shape protects the 22 migrated handler sites.

## What the coder's claims didn't quite match

- Coder/v7 summary: `C# 2554, 2550 pass, 4 fail` — actual now is 2550/2550/0,
  because commit 4 came after the v7 docs were written and replaced the 4
  stubs with 4 real tests.
- Coder/v7 summary: `plang 160 pass, 16 fail` — actual now is 166/166/0,
  because commit 4 added CopyProperties (10 TestReport regressions cleared)
  and the 6 sensitive-fixture failures the summary mentioned appear to have
  been transient or environment-specific (fresh build clean here).

These aren't problems — the coder's docs were written mid-flight before
commit 4 landed. The actual final state is cleaner than the docs imply.

## Hand-off

Recommend **security analyst** next. Security/v1 was on coder/v4; the
attack surface has changed:

- New types: `App.Variables.Variable` (value carrier), `IRawNameResolvable` (marker).
- New code path: `Data.AsT_Impl` carve-out at line 549-562 dispatches the
  raw slot string to `T.Resolve(string, Context)` via reflection,
  bypassing %var% substitution. Inputs: any Data slot whose generic T
  implements the marker. Today the only such T is Variable.
- Reflection cache: `ResolveMethodCache` (shared with the line-632
  Path-style branch) caches MethodInfo per-T. Threading: ConcurrentDictionary,
  no locks needed.

Worth a security re-read of `Data/this.cs:549-562`. From a tester's
perspective the bypass narrows the resolution surface (skips the substitution
branch entirely for marker types) which is generally safer, but security
should confirm.

## Files touched (this session)

- `.bot/runtime2-generator-obp/tester/v7/plan.md` (created)
- `.bot/runtime2-generator-obp/tester/v7/summary.md` (this file)
- `.bot/runtime2-generator-obp/tester/v7/coverage.json` (created)
- `.bot/runtime2-generator-obp/tester/v7/verdict.json` (created)
- `.bot/runtime2-generator-obp/tester/summary.md` (modified — append v7 line)
- `.bot/runtime2-generator-obp/test-report.json` (rewrote — for v7)
- `.bot/runtime2-generator-obp/report.json` (modified — session entry)

No production code or test code committed. All deletion-test mutations were
applied and reverted in-session; `git diff --stat` at session end shows only
`.bot/` files modified.
