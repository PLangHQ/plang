# Decision 126 (asks wait without a limit by default): the architect's own trace, before the coder's

Read at 9c398c543+. The comparison with the coder's trace goes at the end.

## Where an ask's limit comes from today

- **Only the stream channel limits an ask:** `channel/type/stream/this.cs:125-126` links a CTS to the ask's `ct` and applies `CancelAfter(Timeout)`. On expiry it's an `AskTimeout` 408 (`:142-146`).
- `Timeout` is `channel/this.cs:54`, 30 s by default, init-only. It's set by `channel.set`'s `Timeout` parameter (`module/action/channel/set.cs:53`, also 30 s by default).
- `http/this.cs:56`'s `Timeout = 100 s` is on the http *path* (the HTTP client), not a channel. It isn't part of this.
- The other channels' `Ask` (goal, test, message, noop, file, http) apply no limit.
- **`channel.Timeout` has no other reader.** `Read` and `Write` never apply it, so after 126 nothing reads it.

## Two findings

1. **The ask passes no cancellation token.** `module/action/output/ask.cs:90` calls `input.AskAsync(this)`, so `ct` is `default` all the way down to the stream's `ReadLineAsync`. Neither app shutdown nor a program's `timeout` modifier (`timeout.after` pushes its CTS onto the context, `module/action/timeout/after.cs:31-36`) reaches the wait. Today the 30 s limit hides this. **With no limit by default, an ask could wait forever even through shutdown.** So 126 must pass `Context.CancellationToken` into `AskAsync`.
2. **A program's own limit already has a door: the `timeout` modifier.** `ask "…", timeout 2 days` maps to `output.ask` plus `timeout.after`, which cancels through the context token once (1) is fixed. So the ask needs no timeout option of its own: one door per question.

## What 126 leaves open

- **`channel.Timeout` becomes a property nothing reads.** 126's wording ("stays a transport limit for read and write") was mine, not Ingi's, and read and write don't honour it. Two options:
  - (a) Delete it together with `channel.set`'s `Timeout` parameter. That's plang-visible: the catalog loses a parameter, so check the builder twins and any `.goal` that sets a channel timeout.
  - (b) Make stream read and write honour it. That's new behaviour: a slow network write would fail at 30 s by default.
  
  My lean is (a): a limit nothing enforces shouldn't exist, and a transport limit can come back when a transport needs one.
- **A `.goal` sets it:** `Tests/Channels/Add/WithConfig/Start.test.goal:4` (`timeout: PT30S` on `set channel`).

## Comparison with the coder's trace (its 126 message, read after this was pushed)

- **Same:**
  - the stream's `CancelAfter(Timeout)` is the only limit;
  - the other channels apply none;
  - `ask.cs:90` passes no token;
  - `timeout.after` is the program's limit, with no new ask parameter.
- **The coder's adds:**
  - a test's own timeout (`test/this.cs:134-136`) and Ctrl-C also can't end a waiting ask today;
  - the concrete test flips.
- **Mine adds:** `channel.Timeout` has no reader after the change, because read and write never apply it. The coder's "Timeout stays for read and write" is not what the code does. There is also the `.goal` that sets it.
- **Ruling:** the coder's changes 1–3 go ahead. Whether `channel.Timeout` (and `channel.set`'s parameter) is deleted is plang-visible, so it goes to Ingi; the coder leaves it untouched in this slice.
