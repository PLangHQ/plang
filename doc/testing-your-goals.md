# Testing your goals

For someone writing goals. The question this page answers: how do I test a goal and read the result?

## A test is a goal in a `*.test.goal` file

```plang
MathIsSane
- set %a% = 2
- set %b% = 3
- set %sum% = %a% + %b%
- assert %sum% equals 5
```

Save it as `MathIsSane.test.goal`, build, and test:

```bash
plang build
plang --test
```

```
Test summary: 3 total, 1 pass, 2 fail, 0 timeout, 0 stale, 0 skipped
  [Pass] /MathIsSane.test.goal (57ms)
```

A test runs the first goal of its file. It passes when no step fails.

## assert

`assert` demands that something holds, and fails the step with a description when it does not. `assert %sum% equals 5` is the common one. The assert actions are `equals`, `notEquals`, `contains`, `notContains`, `isTrue`, `isFalse`, `isNull`, `isNotNull`, `greaterThan` and `lessThan` (the files in `os/system/modules/assert/`). A message can follow: `assert %sum% equals 6, "sum should be six"`.

A step that only asks whether something holds, to branch, is a [condition](conditions.md), not an assert.

## A failing test

A test with `assert %sum% equals 6` where the sum is 5:

```
Test summary: 3 total, 1 pass, 2 fail, 0 timeout, 0 stale, 0 skipped
  [Fail] /sub/Fails.test.goal (87ms)
    FAIL: /sub/Fails.test.goal
      Expected: 6
      Actual:   5
      Variables:
        %sum% = 5
        %a% = 2
```

The failure shows the expected and actual values and the variables at that point. `plang --test` exits 1 when any test fails or is stale, and 0 when all pass.

## What to run

- `plang --test` runs every `*.test.goal` under the current folder, including subfolders.
- `plang --test sub` runs the tests in the folder `sub`.
- `plang --test MathIsSane.test.goal` runs that one file.
- `plang --test.setting.parallel=1` runs one test at a time. Settings are written `--test.setting.<name>=<value>`. A name the test setting does not have is refused: `[UnknownSetting] Unknown setting 'bogus' on app.test.setting — no public-settable property.`

## A test that is out of date

A test is built like any goal. If its `.goal` changed after the last `plang build`, the run does not run it:

```
Test summary: 1 total, 0 pass, 0 fail, 0 timeout, 1 stale, 0 skipped
  [Stale] /MathIsSane.test.goal (0ms)
```

and exits 1. Run `plang build`.

## The report

`plang --test` also prints module and branch coverage tables after the summary, and writes `.test/results.json` in the app's folder. `--test.setting={"format":"junit"}` writes `.test/junit.xml` too: one `testsuite` per folder, one `testcase` per test, a `failure` element with the message on a fail:

```xml
<testcase name="/sub/Fails.test.goal" time="0.032">
  <failure>sum should be six — Expected: 6, Actual: 5</failure>
</testcase>
```

Variables shown in a failure are shown as they are. A variable holding a secret would print in the report. Do not put one in a test.

## A test's model answers

A test that asks a model reads its answers from a file beside it. `Ask.test.goal` keeps them in `Ask.test.llm.json`:

```plang
Ask
- ask the model "Say ok", write to %reply%
- assert %reply% is not null
```

- The first run asks the model, and writes `Ask.test.llm.json`: a list of `{request, response}`. The request holds `model`, `messages`, `temperature` and `max_completion_tokens`. No header is kept; a search of the file for the key found nothing.
- A later run answers from the file. With the model key unset and the endpoint set to a port nobody listens on, the run passed.
- `--test.setting.llm=live` asks the model every time and neither reads nor writes the file. With the dead endpoint it failed: `Error: Connection refused (127.0.0.1:1)`.
- A changed prompt is a new question; its old answer is not used. This is the repository's test guide (`Documentation/v0.2/building_plang_tests.md`); it was not run here.

Commit the file with the test. It is how a test runs with no key and no network.

## Where tests live in the plang repository

The repository keeps its tests in a folder `test/`, at the path of what they test (`test/module/loop/foreach/`). This page's examples are not from there; the folder is the repository's, not a rule of the tool. `plang --test` runs the tests under the folder you start it in, so start it in `test/` to leave other folders out.

For the assert actions: `os/system/modules/assert/`.
