# PLang Test Module — Design & Implementation Plan

## Problem

PLang needs a test tool that PLang developers use to test their apps. The current test runner is a thin PLang goal (`system/test.goal`) with no isolation, no coverage, no parallelism, and no diagnostics. When it hits a runtime bug (like the foreach skip), it silently breaks — 86 of 143 tests vanish with no error.

## Architecture

The test tool follows PLang's standard pattern: **C# module handlers** orchestrated by **PLang goals**. Same as file, output, condition — the module does the heavy lifting, PLang wires it together.

```
Test
- discover tests in 'Tests/' recursive, write to %tests%
- run tests %tests%, write to %results%
- write test report %results%
```

For now, `test.run` is a single C# action that handles everything internally. As PLang's capabilities grow (engine-as-variable, parallel foreach), it decomposes into smaller PLang steps.

## Module Actions

### `test.discover`

- C# handler: `Directory.GetFiles("*.test.goal", SearchOption.AllDirectories)`
- Returns structured list of test file entries
- Scans .pr files for `test.dependency` actions, builds dependency DAG
- Pure C# file discovery — never depends on PLang foreach working

### `test.run`

- C# handler: the engine of the test tool
- Creates a fresh `Engine` per test (auto-disposes when done — `IAsyncDisposable`)
- Each test gets its own temp directory — no shared SQLite, no state leakage
- Runs tests in parallel by default (`Task.WhenAll`)
- Respects dependency DAG from `test.discover` — dependent tests wait
- Hooks `Action.RunAsync` to log every `module.action` pair executed (coverage matrix)
- Collects structured results: pass/fail/error/skip, timing, failure details

### `test.report`

- C# handler: formats and outputs results
- Console output with pass/fail/error/skip counts and timing
- JSON file output for CI integration
- Coverage matrix: which module.actions were exercised, which weren't

### `test.dependency`

- C# handler: declares that this test depends on another
- Used as a step in test goals: `- add test dependency on Tests/Variable/Basic`
- The LLM maps this to `test.dependency` with a path parameter
- `test.discover` scans .pr files for these, builds DAG before execution
- All tests are parallel by default — dependency is the exception, not the rule

## Test Isolation

Each test gets:
- Fresh `Engine` instance
- Own temp directory as root
- Own SQLite (no shared identity/settings state)
- Clean `engine.Variables` — no leakage between tests

The current signing tests show why: "Identity 'testSigner' already exists" cascades because tests share state. With isolation, each test creates its own identity store.

## Module-Action Coverage Matrix

Hook into `Action.RunAsync` during test execution. Log every `module.action` pair that fires. After the run, cross-reference against the full action registry from `TypeMapping`.

```
MODULE-ACTION COVERAGE
======================
variable.set        ████████████ 47 tests   
condition.if        ████████     23 tests   
loop.foreach        ███          8 tests    
list.any            ░            0 tests    ← gap
list.group          ░            0 tests    ← gap
timer.start         ░            0 tests    ← gap

Coverage: 38/52 actions (73%)
```

Cheap to implement — one hook, one dictionary, one diff. Every CI run shows untested actions.

## Assertion Failure Diagnostics

Today: `Expected: "7", Actual: (null)` — tells you nothing.

Dream:
```
FAIL: assert %result% equals "7"
  %result% is UNSET (never assigned)
  
  Trace:
    Step 2: set %result% = %mathResult%
            %mathResult% = Data { Value=7, Type=int }
            But variable.set received null — %mathResult% not resolved
            
  Likely cause: %mathResult% was set by a step that failed silently
```

On assertion failure, dump `engine.Variables` — show all variable names and values. Walk backward through the step execution history to find where the expected variable went wrong. The engine already has this information — the assert handler just doesn't surface it.

## .pr Snapshot Testing (Builder Stability)

The builder is non-deterministic. Same `.goal` can produce different `.pr` files. Detect drift:

1. Each test has a `.golden.pr` — the known-good builder output
2. On build: semantic diff of new `.pr` vs `.golden.pr`
   - Ignore: description changes, whitespace, ordering
   - Flag: module/action changes, parameter additions/removals, value changes
3. Report which tests have builder drift before running them

```
BUILD STABILITY
===============
condition-equals.test.pr:  matches golden
condition-falsy.test.pr:   action changed: condition.compare → condition.if
                           (run test to see if new mapping works)
list-add.test.pr:          parameter dropped: Index
                           (likely builder regression)
```

This catches the "LLM helpfully corrects values" problem at the .pr level.

## Structured Test Output

```
PLang Test Results
==================
 PASS  Tests/Assert/Assert.test.goal                    (0.12s)
 PASS  Tests/Condition/Basic/Condition.test.goal         (0.08s)
 FAIL  Tests/Variable/Indexing/VariableIndexing.test.goal (0.04s)
       Step 3: assert %items[idx]% equals "b"
       Expected: "b"
       Actual:   (null)
       Variables: %idx%=1, %items%=["a","b","c"]
       
 SKIP  Tests/Http/HttpGet.test.goal                     (requires network)
 ERR   Tests/Event/Wildcard/EventWildcard.test.goal      (0.01s)
       ServiceError: ArgumentException at assert.GreaterThan

──────────────────────────────────────────
 143 total │ 128 passed │ 9 failed │ 4 errors │ 2 skipped
 Duration: 1.2s (parallel)
```

## PLang-Level Mutation Testing (Future)

The ultimate false-green detector. Mutate the `.pr` file (flip operator, swap parameters, change module) and re-run the test. If the test still passes, it's a false green.

```
MUTATION: Tests/Condition/Equals
  Mutant 1: operator "==" → "!="         → test FAILS (caught)
  Mutant 2: Left/Right swapped           → test PASSES (weak!)
  Mutant 3: GoalName "WhenEqual" → "X"   → test FAILS (caught)
  
  Mutation score: 66% (1 surviving mutant)
```

This is natural for PLang — .pr files are structured JSON, easy to mutate mechanically. No source code transformation needed.

## Implementation Priority

1. **`test.run` as single C# action** — isolation + parallel + basic results. Fixes the 86-skipped-tests problem permanently.
2. **Module-action coverage hook** — one hook in `Action.RunAsync`, one dictionary. Immediately shows gaps.
3. **Variable dump on assertion failure** — enhance assert handlers to dump `engine.Variables` on failure.
4. **.pr snapshot testing** — `.golden.pr` comparison after builds.
5. **Mutation testing** — future, once the basics are solid.

## Decomposition Path

`test.run` starts as one C# action. As PLang matures, it decomposes:

```
RunTest                                    
- create test engine, write to %engine%    → test.createEngine
- run %testFile% on %engine%               → test.runGoal  
                                           (engine auto-disposes when RunTest ends)
```

The .goal file keeps the same shape — just more steps mapping to smaller actions.
