# tester v23 — parts 2–5 findings

Method: `dump_pr.py` renders every test step's text next to the action+params
its `.pr` saved; `smell.py` flags the known wrong-save shapes; `coverage.py`
checks each "uncovered" action as an exact module+name pair across the built
`.pr`s; `weakonly.py` finds passing tests whose only assertions are weak. All
read-only, over the 265 built test `.pr` and the three `plang --test` runs.

Severity: nothing here **blocks the merge**. The baseline is honest (every red
owned), git-clean, C# green but for the 6 carried snapshot. Every finding below
is test-quality → **carry**, which fits the frozen-cleanup bar.

---

## Part 2 — passing tests read against their `.pr`

**Mechanical smells are clean.** Across all 265 test `.pr`: no dropped operand,
no type-word turned into a truth check, no channel dropped, no no-op `on.error`,
except the baseline's known-carried `goalchannelrecursion`. Operator fidelity is
exact (`not %flag%` → `== False`, `>`/`>=`/`<`/`<=`/contains/startswith/endswith
all map right). `is [not] <type>` saves as `condition.if(Operator=is/isnot,
Right=<type>)`, the fixed form — not a value compare.

### Finding T5 (carry) — `GoalCallReturn` is a false-green on the value it names

```
module/goal/return/GoalCallReturn.test.goal
  - call ComputeAnswer, write to %answer%
  - assert %answer% is not null         <- only checks not-null
ComputeAnswer.goal:  - set %result% = 42
```

The test is titled "goal call with **return value**", but it never checks the
value. `ComputeAnswer` returns `42`; if the return mechanism handed back the
wrong non-null (a stale value, the goal object, the result dict) the test still
passes. Same shape as T1. **Fix:** `assert %answer% equals 42`.

### Weak-only cluster (carry) — strengthen or accept

Passing tests whose only assertion is `isNotNull`/`isNotEmpty`, no strong assert,
no condition — green whether the behaviour works or not:

- `module/goal/return/GoalCallReturn` (T5, the clear one)
- `app/stepresult/StepResult` — `set %x%="hello"` then `%!data% is not null`;
  the step result is "hello", never checked → could assert `%!data% equals "hello"`.
- `module/identity/{create,export,autocreate}` — smoke tests of "an identity was
  produced"; defensible, but a field assert would prove more.
- `module/channel/event/addonask/start` — binding smoke test.
- `module/llm/{llmcache,llmcontinue,llmformat,llmquery,llmschema}` — the T1 family.

Not all are findings; T5 and `StepResult` are the two where the test's own title
promises a value it doesn't check.

---

## Part 3 — every plan's indexed tests

- **app-systems plan:** healthy. 45 indexed tests pass; the 2 unbuilt
  (`TypedListHoldsItsType`, `UnknownSettingIsAnError`) are the baseline's
  known next-branch stale. Good.
- **snapshot plan:** carried by design (not built) — expected.

### Finding T2 (carry) — the task plan's plang rewrite is incomplete

`plan/task/start.goal` indexes ~29 behaviours; ~14 have **no `.test.goal`**:
- Stage3 (foreach-in-parallel): all 6 absent — `ParallelAnswersATask`,
  `ItemsRunSideBySide`, `WaitAnswersCountComplete`, `PlainForeachBlocks`,
  `CpuCapsItemsAtOnce`, `DefaultCpu`.
- Stage4 (builder goldens): all 5 absent — the `in parallel` / `don't wait` /
  plain build cases + `WaitNotSleep`.
- Stage2 tail: `task/list/{WriteTwiceKeepsBoth,WaitListVersusCurrent,OtherTypesReplace}`.

The task **module** core is fully tested in plang and green (`task/wait`,
`task/cancel`, `goal/call` parallel/plain). The missing behaviours are **all
covered in C#** — `PLang.Tests/Runtime/App/Goals/TaskTests.cs` has 39 tests
including `AForeachInParallel_AnswersATask…`, `AForeachInParallel_RunsAtMostCpuAtOnce`,
`ATaskWrittenOverATask_…WaitForTheListAnswersAllInOrder`,
`AFailureNobodyWaitedFor_GoesToTheErrorChannel…`, `Parallel_IsMadeFromWhatAProgramWrites`.
So the plan is a "rewrite each test in plang" index whose rewrite is unfinished,
with C# as a full backstop; `task/list` collecting is also next-branch (723).
The risk is only that the plan index reads as if those plang tests exist. Carry.

### Finding T3 (minor/carry) — list-query index points to a stale name

`plan/list-query/start.goal` has `call /module/list/query/WhereUnsetVariable`, but
the file is `WhereUnsetVariableIsNotFound.test.goal` (which exists and passes). The
behaviour is covered; the index reference is stale and would not resolve if the
index goal were run as a chain. One-line fix.

---

## Part 4 — missing coverage

### Finding T4a (note) — the run's coverage report under-credits

The `plang --test` coverage block reports `94/142` actions and marks
`on.cache` and `on.timeout` **uncovered** (`[ ]`), but both appear in built,
passing test `.pr`s (`timeoutproducescancellation` builds
`on.timeout(After='100ms')`; `ClausesCompose` builds `on.cache`). So `94/142`
understates real coverage — don't take it at face value.

### Finding T4b (carry) — genuine plang coverage gaps

Actions with no plang test, excluding the next-branch-owned set and the os/ui +
builder-internal modules (browser/screen/window/terminal/build.*, llm.decider):

| action | note |
|---|---|
| `http.download`, `http.upload` | `http.request` is tested; download/upload are not. The http module has **no** `.test.goal` of its own (request is exercised incidentally elsewhere). |
| `timer.start`, `timer.end` | `timer.sleep` is tested; start/end (the stopwatch pair) are not. |
| `variable.get`, `variable.exists` | `variable.set/clear/remove` tested; get/exists not — **likely C#-backstopped** (`PLang.Tests/.../VariablesTests.cs`, `VariableCodeTests.cs`). |

Carry — missing coverage on a frozen branch is not a failing test; flag for the
next branch's test pass.

---

## Part 5 — C# tests added since runtime2 pin behaviour

313 test `.cs` added on the line. Sampled across areas:

- `ScalarsAsNative/BoolWrapperTests` — **strong.** Truthiness, value equality +
  hash, bare serialize, `Compare → NotEqual`, ordering throws
  `IncomparableException`. Mutation-sensitive.
- `Generator/GivenSlotTests` — **strong.** Drives `list.split` and asserts the
  count (2 with `Empty=drop`, 3 default) — pins the given-vs-default contract
  (463), reds on a real regression.
- `CompareRedesign/Stage1_ComparisonEnumTests` — **borderline change-detector.**
  Pins the exact enum membership (`Less/Equal/Greater/NotEqual/Incomparable`) and
  a tautological `NotEqual != Incomparable`. Acceptable as a contract pin, but it
  tracks shape, not behaviour.

No false-greens in the C# sample. The added C# tests predominantly drive real
actions/types and assert observable outcomes.
