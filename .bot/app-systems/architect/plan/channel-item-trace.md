# 8c: channel becomes an item, with `on.write`/`read`/`ask` (the architect's own trace)

Written before reading the coder's 8c sketch. The comparison is at the end. Ingi, 2026-09-27: "yes on app and channel". The naming rule (decisions 100/102/103) already lets channel types register under their namespaces.

## What exists today (read 2026-09-27, `channel/this.cs`, `channel/list/this.cs`)

- **The base** (`channel/this.cs:23`): `abstract class @this : IAsyncDisposable, IDisposable`.
  - Settings are CLR leaves: `Name`, `Direction`, `Buffer` (long), `Timeout` (TimeSpan), `Mime`/`Encoding`/`Encryption`/`Signing` (strings), `Created` (DateTime).
  - `Metadata` is an `IDictionary` marked "v1 compatibility". A grep finds no production reader.
  - `Events` is the per-channel binding list (`channel/event`).
  - `Actor` and `Channels` are `internal set`, stamped by `channel.list.Register` (`list/this.cs:137-142`): a *late stamp*.
- **Firing** (`:117-252`): `WriteAsync`/`ReadAsync` wrap `Write`/`Read` in `FireBefore`/`FireAfter`, and `AskAsync` fires only `OnAsk`, after the ask.
  - `MatchingBindings` merges three sources: the channel's own `Events`, the actor context's `Events` filtered by `ChannelName`, and `App.Event` filtered by name across actors.
  - **After-handler throws are swallowed** (`:232`), which is the silent failure decision 75.8 forbids.
  - `InvokeChannelHandler` passes a **null context** when the channel has no actor (`:248-251`).
- **`channel.list`** (the actor's channels):
  - four by-name doors: `Resolve` (`:77`, null or falls back to output), `Get` (`:106`, null, and treats an executing goal channel as missing: a **type-switch** in the registry, `channel is channel.type.goal.@this g && g.IsExecuting`), `Channel` (`:128`, the NoOp sink), `this[]` (`:156`, throws);
  - four passthroughs acting on one channel: `WriteAsync(name, …)` (`:189`), `ReadChannelAsync<T>` (`:199`), `WriteTextAsync` (`:212`) and `ReadTextAsync` (`:232`), the text ones type-switching on `stream`;
  - `CreateMemoryChannel` (`:254`), a verb+noun factory for tests;
  - `Actor { get; set; } = null!` (`:34`), another late stamp;
  - `ChannelNames`, a plural compound.
  - Callers: 7 of the by-name doors and 7 of the passthroughs outside the list itself.

## Shape

1. **`channel.@this : item.@this, IAsyncDisposable, IDisposable`**, with `[PlangType("channel")]` (the sealed word). Its types register by namespace with no word (`app.channel.type.goal`, …). `IsLeaf` is false, and Output is module-style, writing its `[Out]` settings.
2. **Events:** typed `on.write`, `on.read`, `on.ask` (before/after), and levels `[the channel type's entry, this channel]`.
   - `WriteAsync`: `on.write.Before(this, ctx, data)` → a failure or Handled answer is the write's answer → `Write` → `on.write.After(this, result, ctx)`.
   - `ReadAsync` works the same way.
   - `AskAsync` gets before-ask (new) and after-ask (today's `OnAsk`).
   - The context is the channel's actor's.
3. **After-failures become the result** (decision 89). This is a behaviour change: a failing after-write binding now fails the write instead of being swallowed. **Plang-visible, tell Ingi.**
4. **(Superseded by Ingi, decision 105: a channel fires only its own events through `[channel type, channel]`, a binding goes on the named channel itself, and there's no name filter.)** ~~One source of bindings. Type-level bindings with a name filter replace~~ the actor's `Events`-by-`ChannelName` and `App.Event`. A binding made before the channel exists still catches it by name, and scope `app` covers "every actor's logger". Removed: `channel/event`, the `Events` property, `MatchingBindings`, `FireBefore`/`FireAfter`, `InvokeChannelHandler`, `App.Event`. The binding's own re-entrancy guard replaces channel/event's flow-local guard. **Check the two are equivalent** for a before-write handler that writes to the same channel (per context, versus the old per async flow).
5. **`channel.list`, one-or-many:**
   - **One by-name door.** The executing-goal guard is the goal channel's own answer: a virtual on the channel (e.g. "is this channel taking writes now"), with the goal type overriding it. The registry stops type-switching. The four doors' different miss behaviours are four questions:
     - Resolve's "empty means output" belongs to the caller that has no name;
     - the NoOp sink is for callers that write opportunistically; that intent can be a `NoOp` the caller asks for;
     - the throw goes: a miss is a result.
     Settle with a trace of the 7 callers.
   - **The passthroughs go.** Callers write to the channel itself (`channel[name].WriteAsync(…)`), and "write text" is the channel's own virtual, which stream overrides. No type-switch in the list.
   - **`CreateMemoryChannel` goes.** Tests make `stream.Memory(name)` and add it.
   - **The late stamps:** a channel reaches its actor through its list (`Actor => _list.Actor`, the way module reaches `App` through its list, `module/this.cs:66-69`). That leaves one stamp, the list, set when the channel is added. The list itself is born with its actor. Constructors taking their list would be the full fix, which is wider than 8c.
6. **A null context goes away** once every channel reaches its actor through a list. A channel made without one (tests) gets a list in the test.

## What to watch

- **CLR leaves on the channel** (`Timeout`, `Buffer`, `Mime`, `Encoding`, `Created`): once channel is an item they're navigable (`%!…channel.timeout%`). They should be plang types (the app-model rule), but that's wider than 8c, so list it for stage 9.
- **`Metadata`**, the v1 dictionary: if a grep of `.goal` files and templates finds no reader, delete it.
- **Which channels the builder and `plang --test` open** (the output/error/debug roles, test sessions): run the builder rebuild check and read the test session output.
- **Snapshot/callback paths writing channels:** check that `message` (one-shot) channels still suspend and resume.

## Tests

- A before-write that fails or cancels is the write's answer. An after-write failure is the result, and the rest still run.
- A type-level binding filtered by name catches a channel registered later.
- An app-scoped binding covers another actor's channel.
- The goal channel isn't re-entered by its own body, with no type-switch in the list.
- Before-ask fires.
- The passthrough callers are rewritten.

## Comparison with the coder's sketch

(to fill in after reading it)
