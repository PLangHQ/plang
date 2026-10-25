# Errors

For someone writing a program. The question this page answers: what happens when a step fails at run time, and how do I handle it?

## An unhandled error stops the run

The error is printed with its key and status, the step it came from, and the run exits 1:

```
GoalNotFound (404): Goal 'Early done=true' not found.
  at /Start.goal:14
    - call Early done=true, write to %got%
```

## Throw one

```plang
- throw error "boom"
```

## Handle it on the step

`on error` on a step names what to do when that step fails. It covers that step only, not the steps around it.

```plang
- read missing.txt, on error 404, write out "no such file"
```

`missing.txt` does not exist, so the step answers a 404 and the handler prints `no such file`. The run goes on.

Read the error inside the handler with `%!error%`:

```plang
- throw error "boom", on error write out "caught: %!error.message%"
```

Output: `caught: boom`.

## Retry and ignore

Put each `on error` clause on its own indented line:

```plang
- call Flaky
    on error retry 2 times
    on error ignore
```

`Flaky` adds 1 to `%attempts%` and throws. After the step, `%attempts%` was 3: the first try and two retries. `ignore` lets the run go on when they all fail.

Both clauses on one line, `call Flaky, on error retry 2 times, on error ignore`, did not build: `StepsBroken`, because the builder read the second `on error` as writing a field the step did not give. Use separate lines.

## More handler forms

The tests in `test/module/on/error` cover more (recovery chains, `order GoalFirst`, retries, nested handlers) and pass, 14 of 14. Read them as examples.

## Where the failure is at build time

A step the builder cannot make is not a run-time error. See [when a step fails](when-a-step-fails.md).

For the action: `os/system/modules/on/error.description.md`.
