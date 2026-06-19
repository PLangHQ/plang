# Tester report v1 — template-stamping-at-read

**Date:** 2026-06-19
**Scope:** C# test suites only. The PLang (`plang --test`) suite is known-broken on
this branch (per Ingi) and is **out of scope** — see note at the bottom.
**Context:** No test-designer ran for this branch (per Ingi), so coverage gaps on
the branch's headline behavior are expected. This pass hunts for them.

## C# suite result — green, honestly

Clean rebuild (nuked `bin/obj`, `dotnet build PlangConsole`), all six suites:

| suite     | total | failed |
|-----------|-------|--------|
| Modules   | 973   | 0      |
| Types     | 726   | 0      |
| Wire      | 515   | 0      |
| Data      | 938   | 0      |
| Generator | 203   | 0      |
| Runtime   | 796   | 0      |
| **total** | **4151** | **0** |

The green is real (not a stale-binary or warm-cache artifact). The migrated
"real read path" tests (`RealGoalLoad.ViaChannel`) do a genuine serialize →
stream-channel → read round-trip (`PLang.Tests/Shared/RealGoalLoad.cs`), not a
no-op. Template-stamping at the `.pr`-load seam (`Data.Authored()`/`StampedForm`,
incl. text/list/**dict**) is exercised behaviorally by `DataAsTResolutionTests`
and `TemplateStampOnReadTests`. Scalar wrapper build-out (number/text/datetime/
date/time/duration/bool/null) is behaviorally covered.

## Findings

### F1 — HIGH — the branch's headline runtime rule has ZERO C# coverage

The headline change — a `variable.set` whose `Name` holds a **text** is declined
(`CreateDeclined`, *"a variable names a thing… never created from a value"*) — is
the exact rule that just broke the entire PLang suite. It has **no C# test**.

- Decline lives at `PLang/app/variable/this.cs:67-74` (`Variable.Create`).
- **Mutation proof:** I neutered the decline (accept the text value as a name
  instead of failing), rebuilt, and re-ran Modules + Data + Types + Runtime
  (3433 tests) — **all stayed green.** Reverted; `git status` clean.
- **Root cause of the blind spot:** every test builds raw-name params through
  `PrParam`/`TestAction`, which auto-stamp `type:variable` on raw-name slots
  (`PrParam.cs:29-40`). So *every* test takes the happy (stamped) path; nothing
  ever feeds a value-typed `Name` to assert the decline. `PrParam.cs:10` even
  documents the decline in a comment — but no test asserts it.

**Missing tests to add:**
1. `Variable.Create(textValue, asking)` → `asking` fails with key `CreateDeclined`
   and returns null (direct unit test).
2. A `variable.set` action whose `Name` param is `type:string`/`type:text` with a
   value → run declines `CreateDeclined` (handler-level; must bypass `PrParam`'s
   auto-stamp by building the Data with a non-variable type explicitly).
3. Positive control: same step with `type:variable` succeeds — proves the gate is
   the type stamp, not something else.

### F2 — MEDIUM — temporal comparison operators untested at the condition/operator level

`<`, `>`, `<=`, `>=` are behaviorally tested for **datetime/date/time/duration**
only at the low-level `Stage4_PerTypeCompareTests`. The user-facing path —
`Operator.Evaluate` and the condition/if handler — only exercises `==` and
numbers/strings (`OperatorTests`, `DefaultEvaluatorTests`, `ConditionHandlerTests`).

A bug routing `Operator(">").Evaluate(datetime_a, datetime_b)` through the
condition path would pass the whole suite (caught only by Stage4, which doesn't
go through Operator). Add: `<`/`>`/`<=`/`>=` across datetime/date/time/duration
through `DefaultEvaluator`/`Operator` (and at least one in an integrated
`if`-handler condition).

### F3 — LOW — two structural tests that wouldn't catch a gutted implementation

- `OperatorTests` `Choices_ContainsAllOperators` — asserts the operator-name list
  contains the strings; a gutted `Operator.Evaluate` would still pass.
- `Stage1_ComparisonEnumTests` — asserts enum member *names* exist and aren't
  equal; would not catch swapped/inverted `Less`/`Greater` semantics.

These aren't wrong, but they don't substitute for behavioral coverage (F2 covers
the real gap).

## Verdict

**C# suites: PASS (honest green, 4151/0).** The migration and template-stamping
work is genuinely tested.

**But:** F1 is a confirmed coverage hole on the branch's *headline* runtime rule —
proven by mutation that nothing in C# guards the born-typed decline. Given no
test-designer ran for this branch, I recommend the F1 tests be added before merge
(the rule is currently guarded only by the *negative* signal of the PLang suite
breaking). F2 should follow. I did not author the tests (tester does not commit
source); they're specified above for the coder/test-designer.

## PLang suite — out of scope, but one observation

`plang --test` aborts at its own runner bootstrap: `os/system/test.goal:4`
(`set default %path% = '.'`). The committed `os/system/.build/test.pr` carries
the old `variable.set` shape (`Name` param `type:"string"`, value `%path%`) and
was never rebuilt since the collections-are-data merge — so the new born-typed
rule declines it and the whole suite aborts before discovery. This is a stale
`.pr` artifact, and it's actually *positive* evidence that F1's runtime rule
fires correctly. Repo-wide, 628 committed `.pr` files still carry the old
`type:"string"` Name shape vs ~539 migrated to `type:variable` — a `plang build`
sweep is needed to remigrate them. Flagged for the coder; not graded here.
