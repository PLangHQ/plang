# Codeanalyzer v1 — runtime2-channels

**Scope:** `PLang/App/Channels/`, `PLang/App/Services/`, `PLang/App/modules/channel/`, `PLang/App/Actor/this.cs`
**Verdict:** FAIL — 2 real bugs, 1 latent crash, 3 design/consistency issues

---

## F1 — `Services.Remove` is racy under concurrency [Bug]

`Services/this.cs:33-35`

```csharp
var keep = _services.Where(s => !ReferenceEquals(s, service)).ToList();
while (_services.TryTake(out _)) { }
foreach (var s in keep) _services.Add(s);
```

`ConcurrentBag` has no `Remove`. The drain-and-rebuild is not atomic. A concurrent `New()` between the `Where` snapshot and the `TryTake` drain produces a service that appears in neither `keep` nor `_services` after the rebuild — silently gone.

Fix: replace `ConcurrentBag<Service>` with `ConcurrentDictionary<Guid, Service>`, give each `Service` a `Guid Id { get; } = Guid.NewGuid()`, remove via `_services.TryRemove(service.Id, out _)`.

---

## F2 — `GoalChannel.InvokeGoal` races on `%!data%` [Bug]

`Channel/Goal/this.cs:59`

```csharp
ctx.Variables.Set("!data", data);   // actor-level namespace
...
return await app.RunGoalAsync(Goal, ctx, ct);
```

`ctx` is the actor's shared `Context`. Two concurrent writes to the same goal channel both mutate the same variable slot — the second overwrites before the first `RunGoalAsync` reads. `PushChannelsOverride` (via `AsyncLocal`) correctly isolates channel resolution per-call; `Variables` is not isolated the same way.

Fix: pass `!data` as a call-local frame (child `Variables` scope, or call-local dict on `RunGoalAsync`'s parameter bag) rather than mutating actor-global variables.

---

## F3 — `InvokeChannelHandler` NRE when channel has no actor [Latent crash]

`Channel/this.cs:244-245`

```csharp
var ctx = Actor?.Context!;           // Actor nullable, ! suppresses warning
return binding.Handler(ctx!, null, data);  // NRE if Actor == null
```

`Actor` is nullable by declaration. Any channel constructed without going through `Channels.Register` (tests, service channels not yet wired) has `Actor == null`. Adding an event binding to such a channel and triggering it NREs rather than returning a `Data.Error`.

Fix: guard before firing — if `Actor == null`, return a `MissingActor` `Data.Error` from `FireBefore`, or skip with a diagnostic.

---

## F4 — `EventContext` is dead code [Mismatch]

`Channel/EventContext.cs` declares `EventContext { Channel, Data, AskCallback? Ask }`. `InvokeChannelHandler` never constructs it — handlers receive `(ctx, null, data)`, raw `Data`, not `EventContext`. The Stage 8 test at line 35 constructs it in isolation to check shape, not via the firing path.

The handler contract as documented (EventContext) does not match the handler contract as implemented (raw Data). Wire `EventContext` into the firing path or delete it.

---

## F5 — `Stream.ReadAllTextAsync` / `WriteTextAsync` hardcode UTF-8 [Silent contract violation]

`Channel/Stream/this.cs:157-167`

```csharp
return global::System.Text.Encoding.UTF8.GetString(bytes);   // always UTF-8
var bytes = global::System.Text.Encoding.UTF8.GetBytes(text);  // always UTF-8
```

The channel exposes `public string Encoding { get; init; } = "utf-8"` but these convenience methods ignore it. Setting `Encoding = "iso-8859-1"` has zero effect on text I/O.

Fix: resolve via `System.Text.Encoding.GetEncoding(Encoding)` at call time.

---

## F6 — `channel.set` cannot create Bidirectional goal channels [Design gap]

`modules/channel/set.cs:47-49`

```csharp
var direction = string.Equals(name, App.Channels.@this.Input, …)
    ? ChannelDirection.Input
    : ChannelDirection.Output;   // all else is Output, Bidirectional unreachable
```

`GoalChannel` extends `Session.@this` — the kept-open, Ask-capable pattern — but every goal channel from `channel.set` is stamped `Output`, so `CanRead = false`. A channel named "chat" registered this way cannot be asked without inconsistency (AskCore runs, direction flag says otherwise).

If Ask on goal channels is a supported use case, add a `Direction` parameter to `Set`, or infer from name heuristics. If not intended, document that Ask on goal channels is unsupported.

---

## Summary

| # | Location | Severity |
|---|---|---|
| F1 | `Services/this.cs:33-35` | Bug — concurrent Remove+New silently drops services |
| F2 | `Channel/Goal/this.cs:59` | Bug — `%!data%` races under concurrent writes |
| F3 | `Channel/this.cs:244` | Latent crash — NRE when Actor is null |
| F4 | `Channel/EventContext.cs` | Mismatch — type declared, never used in firing path |
| F5 | `Channel/Stream/this.cs:157-167` | Silent bug — Encoding property ignored |
| F6 | `modules/channel/set.cs:47-49` | Design gap — Bidirectional goal channels unreachable |
