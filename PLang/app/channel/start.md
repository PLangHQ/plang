# Channels — redirectable I/O

Every line of I/O in Runtime2 goes through a channel: what a program writes
out, what it reads in, what it asks a person, and what the runtime reports
about itself. This page is for the C# developer changing that path. It says
what a channel is, which one a given write reaches, and why no code writes to
the console directly.

```
app.channel.@this        PLang/app/channel/this.cs          namespace app.channel
app.channel.list.@this   PLang/app/channel/list/this.cs     namespace app.channel.list
app.channel.type.*       PLang/app/channel/type/<kind>/     namespace app.channel.type.<kind>
```

> **Scope.** This is the C# page. The plang user writes `write`, `ask` and
> `output` steps; the user-facing description of those actions lives in
> `os/system/modules/output/`, not here.

---

## 1. What a channel is

A channel is a **named, directional I/O stream owned by an actor**. It is an
`item` (the base is `app.type.item.@this`) and it is a structure, not a leaf:
`IsLeaf` is false, so it is written by its tagged members. A channel is a live
resource, so `Clone()` returns the same instance.

The members that matter when you read it:

| Member | Type | Meaning |
|---|---|---|
| `Name` | `string` | the channel's name, case-insensitive in the registry |
| `Direction` | `choice<ChannelDirection>` | `Input`, `Output`, or `Bidirectional` |
| `Mime` | `text` | the format that encodes and decodes its values, `text/plain` by default |
| `Encoding` | `string` | the text encoding name, `utf-8` by default |
| `Actor` | `app.actor.@this` | the actor it belongs to; its events fire in that actor's context |
| `Channels` | `channel.list.@this` | the registry it was registered into |
| `IsOpen` | `bool` | cleared by `Close()` and `DisposeAsync()` |

`CanRead` and `CanWrite` are derived from `Direction` and `IsOpen`. A concrete
kind can override them (a stream checks its `Stream.CanRead` as well).

> **Channels live on the actor.** `app.actor.@this.Channel`
> (`PLang/app/actor/this.cs`) is the actor's `channel.list.@this`. Each actor
> has its own set, so `System` and `User` do not share a registry. Reach the
> channel of the current actor through `context.Actor.Channel`.

---

## 2. The role names and the boot invariant

Three names are fixed. They are constants on the registry
(`PLang/app/channel/list/this.cs`):

```csharp
public const string Output = "output";
public const string Error  = "error";
public const string Input  = "input";
public const string Debug  = "debug";

public static readonly string[] Defaults = [Output, Error, Input];
```

- A `write` with no channel argument goes to `output`.
- An error write goes to `error`.
- A read, and the answer to an `ask`, come from `input`.
- `debug` is the diagnostic channel (§6).

**The all-three-roles invariant.** `Defaults` must all be registered before a
goal runs. `list.Verify()` checks it and returns `MissingRequiredChannelAtBoot`
(500) if one is missing. `app.Launch` calls `Verify()` on every actor
(`PLang/app/this.cs`, the loop over `actor.list.Items()`) before user code runs.
The entry point registers the channels; the registry does not create them.

`Remove` refuses a default name (`ChannelInvariantViolation`). To swap a
default's backing, use `Set`, which registers the new channel and disposes the
old one in one step, so the name always resolves.

---

## 3. Selecting a channel

The registry has three selectors. Pick by what a miss means:

| Selector | On a miss | Use it when |
|---|---|---|
| `list[name]` | throws `KeyNotFoundException` | the name is a default, which `Verify` guarantees |
| `list.Get(name)` | returns `null` | the name came from a user, so a miss is the caller's result to make |
| `list[System.Type]` | returns `null` | you want the open channel of a CLR class, under whatever name |

`Get` returns only a channel that is `Available`. A goal channel is not
available while its own goal body runs, so a body that writes to its own name
cannot loop back into itself. Sibling and late-registered channels stay
visible.

`list.list` enumerates the registered channels. `list.Report(failed)` is the
end of the error path: it writes to the actor's `error` channel, and if that
write fails it goes to the system actor's `error` channel. An error is never
dropped.

---

## 4. The write, read and ask surface

The base class has one public entry per operation. Each entry runs the
channel's own `on.write`, `on.read` or `on.ask` bindings before and after the
work (`PLang/app/channel/this.cs`):

| Public entry | Calls | Bindings |
|---|---|---|
| `WriteAsync(data)` | the abstract `Write(data)` | `on.write` before and after |
| `WriteText(text)` | `WriteAsync` with `text` wrapped in `Data` | as `WriteAsync` |
| `ReadAsync()` | the abstract `Read()` | `on.read` before and after |
| `AskAsync(action)` | the abstract `Ask(action)` | `on.ask` before and after |

A concrete kind implements the three abstract members:

```csharp
public abstract Task<global::app.data.@this> Write(global::app.data.@this data, CancellationToken ct = default);
public abstract Task<global::app.data.@this> Read(CancellationToken ct = default);
public abstract Task<global::app.data.@this> Ask(module.output.ask action, CancellationToken ct = default);
```

Two rules hold in the base, not in each kind:

- **A write's transport failure is an error result.** Any exception from
  `Write` becomes a `ServiceError` with key `WriteError`, except a program's
  own `AppException`, which keeps its key and reaches the action. The same
  split applies to read and ask.
- **Text is decoded in the channel's encoding.** The base `Read(byte[])` takes
  the bytes a kind read, decodes them through the `Mime` format, and converts
  text from `Encoding` to UTF-8. An unknown encoding name is an error that
  names it.

### Interactive prompts

A stream channel's `Ask` is the two-call pattern. It writes the question
through the `output` channel, then reads one line from its own stream:

1. If the actor has a writable `output` channel, the question goes there. If
   not, and the channel itself can write, it writes the question to itself.
   With no writer at all, the question is dropped and the read still runs.
2. It reads one line from its own stream. End of stream is a `NoAnswer` error,
   not an empty answer, so a caller never loops on `""`.

The line is the answer as `text`. In plang's own format (§5, `InPlay`) the
question goes out as a pending `Ask` and the answer comes back as `Data`.

> The module side is `ask` (`PLang/app/module/output/ask.cs`): it asks the
> actor's input channel and answers with what came back.

---

## 5. The channel types

Each folder under `PLang/app/channel/type/` is one kind. The base `channel`
is abstract; `session` and `message` are the two structural bases, and the
concrete kinds sit on them.

| Kind | Source | What it routes to |
|---|---|---|
| `stream` | `type/stream/this.cs` | a `System.IO.Stream`. Console streams and handed-in streams (a response body, a memory stream in tests). Framed one message per line. Extends `session`. |
| `goal` | `type/goal/this.cs` | a `goal.call`. A write runs that goal with the written data as `%message%`, and returns the call's result. Extends `session`. |
| `session` | `type/session/this.cs` | abstract. A kept-open connection; `Ask` blocks until the answer arrives. |
| `message` | `type/message/this.cs` | abstract. A one-shot exchange; `Ask` returns a pending `Ask` with a `Snapshot`, and the goal resumes through a callback. |
| `file` | `type/file/this.cs` | a file on disk. Write-only. The value is encoded in the format the file's extension names, and lands in one write. |
| `test` | `type/test/this.cs` | a test run's session. Collects what is written, one line per write. |
| `noop` | `type/noop/this.cs` | a sentinel. Every operation answers `ChannelNotFound`. No caller in `PLang/` outside its folder was found in this read. |

> **`stream` is locked core.** The channel and stream surface is part of the
> locked core (the `channel` and `stream` items). Behaviour fixes and private
> refactors are fine. Changes to the public surface are not made in this
> folder; they go through the architect.

---

## 6. No `Console.*` in production C#

Channels exist so that I/O can be redirected: a test captures the output, a
host sends it to a file, a program running plang reads it as data. A direct
`Console.WriteLine` bypasses all of that, so the rule is:

- **Diagnostics** go to the debug channel: `App.Debug?.Write(...)`
  (`PLang/app/module/debug/this.cs`). The nullable `?.` is the gate. Debug is
  null in production, so the write is dropped. Under `--debug` it is written,
  to the System actor's `debug` channel, or to its `error` channel when there
  is no `debug` channel.
- **User-facing output** goes to the actor's `output` channel:
  `context.Actor.Channel[channel.list.@this.Output].WriteText(...)`. Do not
  route user output through `Debug`; its gate would silence it without
  `--debug`.
- **Prompts** use the two-call pattern in §4.

The rule and its test-fixture pattern are in `Documentation/v0.2/bans.md`,
section "Console.* Is Banned in Production C#". The two permitted exceptions are
`Console.IsInputRedirected` and `Console.IsOutputRedirected` (queries, not
writes), and the process-boundary fallback in `PlangConsole/Program.cs`.

---

## 7. How a plang `write` reaches a channel

The plang action is `write`. Its handler is `PLang/app/module/output/write.cs`,
and its whole job is one line:

```csharp
public async Task<data.@this> Start() => await Channel.WriteAsync(Data ?? Context.Ok());
```

The `IChannel` interface makes the source generator resolve the action's
`channel` parameter against the current actor's channels when the action
starts. No name means `output`. So a plang `write` is a `Channel.WriteAsync`
on the actor's `output` channel, and the value writes itself through the
channel's `Mime` format. The handler opens no carrier.

`ask` (`PLang/app/module/output/ask.cs`) is the same shape on the `input`
side: it calls `AskAsync` and answers with what came back. The user-facing
description of `write` and `ask` is in `os/system/modules/output/`.

---

## 8. Find ...

| You want | Source |
|---|---|
| the base class, its entries, and its bindings | `PLang/app/channel/this.cs` |
| the registry: defaults, selectors, `Verify`, `Report`, `Set`, `Remove` | `PLang/app/channel/list/this.cs` |
| the actor's channel set (`actor.Channel`) | `PLang/app/actor/this.cs` |
| the boot check that every actor is verified | `PLang/app/this.cs` (the loop calling `Verify()` in `Launch`) |
| the plang `write` action | `PLang/app/module/output/write.cs` |
| the plang `ask` action | `PLang/app/module/output/ask.cs` |
| the diagnostic write (`--debug`) | `PLang/app/module/debug/this.cs` |
| a console or handed-in stream | `PLang/app/channel/type/stream/this.cs` |
| a goal-backed channel (`channel.set` with a goal) | `PLang/app/channel/type/goal/this.cs` |
| a file as an output | `PLang/app/channel/type/file/this.cs` |
| the Console ban and its exceptions | `Documentation/v0.2/bans.md` |

---

## The tree from here

| Doc | Covers |
|---|---|
| `../start.md` | the app tree: the root, the `@this` pattern, the collection-node rule |
| `../actor/start.md` | the actor that owns a channel set |
| `../module/start.md` | the module system, including the `output` module that carries `write` and `ask` |
