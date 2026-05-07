# coder summary — runtime2-channels

## Version
v10 — Console.* purge: route production writes through channels.

## What this is

The branch is "runtime2-channels" — channels are the wired primitive for actor I/O, with the entire point being **redirectability**: a user can re-register the `output`/`error`/`debug` channel to a file, an in-memory buffer, an HTTP response — anything. But several production sites still wrote directly to `Console.WriteLine` / `Console.Error.WriteLine`, bypassing that surface entirely.

The most ironic offender was the Debug class itself: it exposed `Debug.Write(...)` (which routes through `System.Channels.Resolve("debug") ?? Resolve("error")`) and *its own docstring* said "use this instead of Console.WriteLine". Then 5 of its own internal sites wrote straight to `Console.Error`.

This pass purges all production `Console.*` writes. Remaining `Console.*` references in production are:
- `Console.IsInputRedirected` at `PLang/App/this.cs:562` — a **query** (am I in a TTY?), not a write. Stays.
- `Console.Error.WriteLine` at `PlangConsole/Program.cs:26` — process boundary; if `executor.Run` itself fails to wire channels, this is the last-resort error sink. Ingi explicitly chose to leave it.

## What was done

### 1. Debug class internal bypass (`PLang/App/Debug/this.cs`)

5 sites (lines 301, 310, 414, 521, 527) routed through `Debug.Write` (debug channel, falls back to error). The static lifecycle handlers (`BeforeStepHandler`, `AfterStepHandler`, `BeforeActionHandler`, `AfterActionHandler`, `AfterGoalHandler`) became `async Task<Data.@this>` so they could `await WriteFiltered(...)`. `WriteFiltered` and `WriteLlmBlock` returned `Task`. Sync event-handler sites (`LogMutation` / `LogEvent` subscribed as `Action<Data>`, the LLM file-write fallback inside an OnBeforeRequest lambda) use fire-and-forget `_ = Write(...)` — Console.Error was non-awaitable too, so ordering guarantees are unchanged.

### 2. Builder progress (`DefaultBuilderProvider.cs` + interface)

`Saved {goal} (1.2s)` and `Group promotion: N step(s) ...` were `Console.WriteLine` — now `await app.CurrentActor.Channels.WriteTextAsync("output", ...)`. Decision (after surfacing the question to Ingi): **not** routed through `Debug.Write`, because that gate (`if (!IsEnabled) return Task.CompletedTask`) would silence them whenever `--debug` is off — which is the default. These are user-facing build chatter, not diagnostic output.

`PromoteGroups` handler had to flip from `Data.@this` to `async Task<Data.@this>`. Updated `IBuilderProvider.PromoteGroups` and the `promoteGroups.Run()` shim to match.

### 3. LLM validation chatter (`OpenAiProvider.cs`)

Same call: `await app.CurrentActor.Channels.WriteTextAsync("output", ...)`. Same reasoning — regular user-facing output, not debug-gated.

### 4. App build prompt (`PLang/App/this.cs`)

The interesting case. Default channels are direction-split: `output` is write-only stdout, `input` is read-only stdin, `error` is write-only stderr. `Channel.Stream.AskCore` is bidirectional-stream-only — it writes the prompt to its own stream then reads, which works for memory/HTTP-session channels but not for the split console pair.

Resolved with a two-call pattern:

```csharp
var outputChannel = User.Channels.Get("output") as Channel.Stream.@this;
var inputChannel  = User.Channels.Get("input")  as Channel.Stream.@this;
await outputChannel.WriteTextAsync($"No app found at ... Create new app? (y/n): ");
using var reader = new StreamReader(inputChannel.Stream, leaveOpen: true);
var answer = (await reader.ReadLineAsync())?.Trim().ToLowerInvariant();
```

`Console.IsInputRedirected` (`is stdin a TTY?`) stays — that's the gate for *whether* to prompt at all, not *how*. No new API added (e.g. cross-channel `AskAsync`) — only one caller right now; if a second appears, that's the time to extract.

### 5. Test fixture update (`DebugSmokeTests.cs`)

`Console.SetError` capture was incompatible with channels: the channel is registered with `Console.OpenStandardError()` *at boot time*, capturing that Stream reference. Re-pointing `Console.Error` later doesn't affect the captured Stream. Replaced with a memory channel registered as `"error"` on the System actor — that's the redirection model channels exist to provide.

## Code example

```csharp
// Before: bypasses channels, ignores --debug routing and output redirection
Console.WriteLine($"  Saved {goal.Name} ({elapsed.TotalSeconds:F1}s)");

// After: routed through the actor's output channel — redirectable,
// captured by tests via memory channels, free to be re-registered to a file.
await app.CurrentActor.Channels.WriteTextAsync(
    global::App.Channels.@this.Output,
    $"  Saved {goal.Name} ({elapsed.TotalSeconds:F1}s){Environment.NewLine}");
```

For the Debug class internals it's the same shape but through the gated `Debug.Write` (debug channel surface), since those sites are conceptually diagnostic and *should* go silent without `--debug`.

## Tests

- C# (`dotnet run --project PLang.Tests`): 2755/2755 ✅
- PLang (`cd Tests && plang --test`): 199/199 ✅

Baseline matches; no regressions. The single test that broke (`DebugSmokeTests`) broke for exactly the reason the channels work was needed — its capture mechanism was the old bypass model. Now exercises the real channel path.

## Next bot

**codeanalyzer** — review for OBP / channel-discipline issues (e.g. is fire-and-forget acceptable for the Debug event-handler sites, or should those event lambdas return Task?), and any simplification opportunities. No new modules or actions added — `builder-handoff.md` not needed.
