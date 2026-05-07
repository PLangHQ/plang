# v10 plan — purge Console.* from production C#, route through channels

## Problem

The branch is "runtime2-channels" — channels are the wired primitive for actor I/O.
But several production sites still write to `Console.*` directly, bypassing the
redirectable channel surface. Most ironic offender: the Debug class itself
exposes `Debug.Write(...)` (which routes through the `debug` channel falling
back to `error`), then 5 of its own internal sites write straight to
`Console.Error`. A user who re-registers the `debug` channel to a file will
get only half their output captured.

## Scope (decided with Ingi)

Fix:
1. **Debug class internal bypass** (5 sites in `PLang/App/Debug/this.cs`):
   - line 301 `LogMutation` — variable change dump
   - line 310 `LogEvent` — watch event banner
   - line 414 `EmitLlmBlock` — file-write failure warning
   - line 521 `WriteFiltered` — main debug stream (BeforeStep/AfterStep/LLM blocks)
   - line 527 `AfterGoalHandler` — goal completion banner
2. **Builder progress** (`DefaultBuilderProvider.cs:192,519`) — debug-y, route through `Debug.Write`.
3. **LLM validation chatter** (`OpenAiProvider.cs:375,382`) — debug-y, route through `Debug.Write`.
4. **App build prompt** (`App/this.cs:566-567`) — interactive ask. Use `User.Channels` output (write prompt) + input (read line). The interesting case.

Leave alone:
- `PlangConsole/Program.cs:26` — process-boundary last resort. App may have failed before channels were registered.
- Test files (`DebugSmokeTests.cs`, `TypeMismatchExample.cs`) — not production code.

## Design

### Debug class — route through `Debug.Write`

`Debug.Write(object?)` already exists at line 111 and routes
`System.Channels.Resolve("debug") ?? Resolve("error")`. Drop-in replacement.

- **Async-context sites** (`BeforeStepHandler`, `AfterStepHandler`,
  `AfterGoalHandler`) return `Task<Data>` already. Convert to `async Task<Data>`
  and `await context.App.Debug.Write(...)`. `WriteFiltered` returns
  `async Task`. `WriteLlmBlock` returns `async Task`.
- **Sync-callback sites** (`LogMutation`, `LogEvent`, `EmitLlmBlock`'s file-fail
  fallback) are subscribed as `Action<...>` event handlers and can't be made
  async without changing the event signature. Use `_ = Write(...)` —
  fire-and-forget. Console writes were already non-awaitable, so ordering
  guarantees don't change.

### Builder + LLM provider

Both already have `context.App` in scope. Replace `Console.WriteLine(...)` with
`await context.App.Debug.Write(...)`. These are async methods, no signature
churn.

Decision: route through `Debug.Write` (debug channel) rather than the `output`
channel — these messages are diagnostic chatter, not user-facing program
output. They should be silenceable independent of program output.

### App build prompt — the interesting case

Current code (lines 562-571):
```csharp
if (Console.IsInputRedirected)
    return Data.@this.FromError(...);

Console.Write($"No app found at {AbsolutePath}. Create new app? (y/n): ");
var answer = Console.ReadLine()?.Trim().ToLowerInvariant();
```

Channels available on `User.Channels` at this point:
- `output` — `Channel.Stream.@this` wrapping `Console.OpenStandardOutput()`, write-only
- `input`  — `Channel.Stream.@this` wrapping `Console.OpenStandardInput()`, read-only

The natural primitive is `Stream.AskCore(prompt, ct)` — but it writes the
prompt to its OWN stream, then reads. That works for a bidirectional channel
(memory, HTTP session) but NOT for the default split console pair (output is
write-only, input is read-only). So `AskCore` on `input` would fail to write
the prompt; on `output` would fail to read the answer.

**Two-call approach** (chosen):
```csharp
var output = User.Channels.Get(global::App.Channels.@this.Output) as Stream.@this;
var input  = User.Channels.Get(global::App.Channels.@this.Input)  as Stream.@this;
await output.WriteTextAsync($"No app found at ... Create new app? (y/n): ");
using var reader = new StreamReader(input.Stream, leaveOpen: true);
var answer = (await reader.ReadLineAsync())?.Trim().ToLowerInvariant();
```

`Stream.@this` already exposes `WriteTextAsync` and the raw `Stream`. The
`StreamReader` mirrors what `AskCore` does internally (line 117). Keep
`Console.IsInputRedirected` check — that's about *whether* to prompt at all,
not *how* to prompt; it correctly remains a `Console` query.

**Not adding new API** (e.g. `Channels.AskAcrossAsync(out, in, prompt)`) — only
one caller right now, no need to design a primitive on speculation. If a
second caller appears, that's the time to extract.

## Sequence

1. Debug class — refactor static handlers to async, route 5 sites through `Debug.Write`.
2. DefaultBuilderProvider — 2 sites.
3. OpenAiProvider — 2 sites.
4. App build prompt — 2 sites.
5. Build, run both test suites.
6. Update summary.md, commit, push.
