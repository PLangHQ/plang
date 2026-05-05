# Variable capture semantics for error-retry

`%!error.callback%` is a **synthetic, lazily materialised** PLang property. It has no cost until read.

## Throw-time view via Diff reverse-apply

When read, the runtime computes Variables-at-throw-time by:

1. Take current `App.Variables.@this`.
2. Walk the callstack diff stream, filtered to events with `timestamp > error.throwTime`.
3. Reverse-apply each `Set` (restore each variable's `Before` value).

This means:

- Error handler mutations (`- set %name% = "ble"` inside the handler) appear in the live App's Variables but are *reversed* in the callback's view.
- The developer writing the error handler never has to reason about variable name collisions with the failed code.

## Diff is required on the error path

When `Flags.Diff` is off and an error occurs, the runtime auto-flips it for the duration of error processing. Cost is paid only on the error path, which is rare. No conditional code path on the consumer side.

## Providers don't get the rich treatment

Providers do **not** have diff tracking. The callback captures their registry-layer selection state at materialisation time. Convention: error handlers should not mutate provider selections. If they do, the callback reflects the post-handler selection — the runtime will not catch this. Honest asymmetry: vars get rich treatment because we have rich tooling for them; providers get the pragmatic one.

## Multiple reads in one handler

Multiple reads of `%!error.callback%` in one handler with var mutations between them produce different callbacks. **Convention: read once, in one place.** The materialisation is idempotent only if the live state hasn't changed.
