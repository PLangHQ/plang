# Codeanalyzer v5 — review of coder v10

**Verdict: PASS (with 3 minor notes)**

Reviewed scope: 4 production C# files changed in `efdc3d12` ("purge production Console.* writes").

- `PLang/App/Debug/this.cs` — 5 internal sites switched from `Console.Error.*` → `_ = Write(...)` (fire-and-forget) or `await debug.Write(...)`. Five `Task.FromResult` handler returns rewritten as `async`.
- `PLang/App/modules/builder/providers/DefaultBuilderProvider.cs` — 2 progress lines via `app.CurrentActor.Channels.WriteTextAsync(Output, ...)`. `PromoteGroups` made `async Task<Data>`.
- `PLang/App/modules/builder/providers/IBuilderProvider.cs` + `promoteGroups.cs` — interface + caller updated to match.
- `PLang/App/modules/llm/providers/OpenAiProvider.cs` — 2 validation chatter lines via same `WriteTextAsync(Output, ...)`.
- `PLang/App/this.cs` — interactive build prompt now writes via `outputChannel.WriteTextAsync` and reads via a `StreamReader` over `inputChannel.Stream`.

---

## Pass 1a — OBP rule check

No new violations introduced.

- All channel access goes through `Channels.Get` / `Channels.WriteTextAsync` / `Stream.@this.WriteTextAsync` (the type's own surface). No external code mutates a public collection or takes a cross-file lock.
- Debug class continues to own its own discipline; it now consumes the `Channels` surface as a client, which is the right direction.

## Pass 1b — Shape smell checklist

Re-checked the four items against changed files:

1. Public mutable collection with rules enforced from outside? — **No** for v10 changes. (Carry-over from v2: `Channel.Events` field surface — unchanged in v10, not in scope.)
2. `lock(other.X)` from outside? — **No**.
3. Same logical thing stored twice? — **No**.
4. Allocate / mutate / cleanup split across three files? — **No**. The interactive prompt's `StreamReader` is locally scoped (`using var reader`, `leaveOpen: true`) — channel still owns the stream.

## Pass 2 — Simplification

### N1 — `App/this.cs:571-578` bypasses `Channels.WriteTextAsync` helper for the write side
```csharp
var outputChannel = User.Channels.Get(global::App.Channels.@this.Output) as global::App.Channels.Channel.Stream.@this;
...
if (outputChannel == null || inputChannel == null)
    return Data.@this.FromError(...);
await outputChannel.WriteTextAsync(...);
```
`PLang/App/Channels/this.cs:222` already exposes `WriteTextAsync(channelName, text)` that does the cast, error-wraps a non-stream channel, and returns `Data` with proper service errors. The current code reimplements ~half of that, *and* discards the return value (any I/O exception is silently swallowed — `WriteTextAsync` would return a `WriteError`).

For the read side a manual `StreamReader` is justified — there is no single-line read helper on `Channels`, and `ReadTextAsync` reads to EOF (won't return on stdin). So the direct `Stream` access stays.

Recommendation (coder pickup): for the write half, prefer:
```csharp
var write = await User.Channels.WriteTextAsync(global::App.Channels.@this.Output, prompt);
if (!write.Success) return write;
```
This also collapses the channel-not-found / cast-failed branches into the helper's own `MissingRequiredChannel` error path. Not a bug — Console.Write also swallowed exceptions — but the new surface is strictly better and the v10 plan already called the channel surface "the wired primitive."

### N2 — Builder + LLM provider drop the `WriteTextAsync` `Data.@this` return
```csharp
await app.CurrentActor.Channels.WriteTextAsync(global::App.Channels.@this.Output,
    $"  Saved {goal.Name} ({elapsed.TotalSeconds:F1}s){Environment.NewLine}");
```
Three sites in `DefaultBuilderProvider.cs:192,520` and `OpenAiProvider.cs:375,383` ignore the returned `Data`. `Console.WriteLine` was also fire-and-forget, so this is parity, but routing through a channel now produces a structured error envelope on failure that nobody reads. For diagnostic chatter that's defensible; flagging it so the next round can decide whether to log on failure or stay silent on purpose.

## Pass 3 — Readability

### N3 — Plan/implementation drift on channel choice for builder + LLM progress
v10 `plan.md` says builder + LLM provider "route through `Debug.Write`" with rationale "diagnostic chatter, not user-facing program output." The actual code routes through `Channels.WriteTextAsync(Output, ...)`. The code's choice is the right one — `Debug.Write` is gated on `IsEnabled` and would only show under `--debug`, but `"  Saved Foo.goal (1.2s)"` and `"Validation failed (retry 2/3)"` are baseline build UX that should always show. So implementation > plan here. The drift is documentation-only, but worth noting so the diary/summary records the actual decision.

## Pass 4 — Behavioral reasoning

Walked the changed sites for silent breakage:

- **`WriteFiltered` null-guard** (`Debug/this.cs:521`): old code `Console.Error.Write(output)` ran unconditionally; new code returns `context.App?.Debug?.Write(output) ?? Task.CompletedTask`. If `Debug` is null, output is silently dropped where it used to print. In practice these handlers only fire when subscribed via `Apply()`, which itself runs on a `Debug` instance — so `Debug` is non-null at every reachable call site. No real regression, but the elvis chain hides what was previously an unconditional write.

- **Fire-and-forget Tasks** (`LogMutation:301`, `LogEvent:313`, `EmitLlmBlock:415`): `_ = Write(...)`. `Write` is gated on `IsEnabled` so it's a free no-op when debug is off. When enabled, a failure inside `WriteCore` (serializer / stream broken) becomes an unobserved task exception. The trade-off matches the comments — `Console.Error` was non-awaitable too. Acceptable; documenting so it isn't relitigated.

- **Encoding inconsistency** (`App/this.cs:578`): the new `StreamReader(inputChannel.Stream, leaveOpen: true)` uses .NET's default (UTF-8 + BOM detection) and ignores `Channel.@this.Encoding`. `Stream.@this.AskCore` does honor it via `ResolveEncoding()`. For a y/n prompt over stdin this is invisible, but if `Encoding` is later set on the Input channel, this path silently disagrees with `AskCore`. Cheap fix when the prompt is touched again — not blocking.

- **`promoteGroups` async chain**: `IBuilderProvider.PromoteGroups` is now `Task<Data>`; `promoteGroups.cs:23` returns the task directly (no `Task.FromResult` wrapper) — correct. Verified the only other caller is the interface contract.

## Pass 5 — Deletion test

- The two `if (outputChannel == null || inputChannel == null)` branches in `App/this.cs:573-575` would only trip if `PlangConsole` failed to register defaults at boot, which the constructor at `App/this.cs:352-362` does unconditionally. If `WriteTextAsync` were used instead (see N1), the helper's own `MissingRequiredChannel` error covers this and the manual guard collapses.

---

## Verdict: PASS

No bugs, no latent crashes, no OBP violations. Three minor notes (N1 prompt-write helper, N2 ignored Data returns, N3 plan/code drift) are coder-pickup quality items, not blockers. The branch's stated theme — channels as the wired primitive — is upheld: every removed `Console.*` now flows through a channel (or through `Debug.Write` which itself flows through a channel). Behavior parity for diagnostic output is preserved.

Carry-over from v2 still open and out of v10 scope: `Channel.Events` public-list shape smell. Not introduced by v10; remains a future cleanup target.
