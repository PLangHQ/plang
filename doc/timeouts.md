# Timeouts

For someone writing a program. The question this page answers: how do I stop a step that takes too long?

```plang
Start
- call Slow, timeout after 100ms, on error write out "timed out"

Slow
- sleep 2 seconds
```

Output: `timed out`. The step is cancelled at its deadline and fails with a Timeout error, which an `on error` clause catches like any other. See [errors](errors.md).

## With retries

A retry gets its own deadline:

```plang
- call SlowTwice, timeout after 100ms, on error retry 2 times
```

In the test, the first two attempts are slow and time out, and the third is fast. `%attempts%` is 3 at the end. (`test/module/on/timeout/RetryGetsAFreshDeadline.test.goal`; both tests in `test/module/on/timeout` pass.)

## Forms

`timeout after 10 seconds`, `timeout 2 minutes`, `timeout after 100ms`. Only `100ms` was run in a scratch app; the others are the module's teaching (`os/system/modules/on/timeout.description.md`).

The timeout applies to the action in the same step. A step that only waits for time to pass, such as `sleep 2 seconds`, is the timer module and has nothing to bound.
