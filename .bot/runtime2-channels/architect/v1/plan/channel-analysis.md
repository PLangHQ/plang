# Channels — what we have, what we're missing, what only PLang can do

## What we have (in this branch)

| Feature | Notes |
|---------|-------|
| `Buffer` (size) | per-channel byte buffer |
| `Timeout` | per-write/per-read, ISO 8601 |
| `Mime` | content type, drives serializer pick |
| `Encoding` | UTF-8 default |
| `Encryption` (optional) | provider reference |
| `Signing` (default System) | every I/O signed — language primitive |
| `Direction` | Input / Output / Bidirectional |
| `Role` | Output / Error / Input — runtime navigation by role |
| Channel kinds | Stream, Goal (under Session); Web (under Message, deferred) |
| Suspend/resume on Ask | via existing callback machinery |
| Fan-out | via Goal channel composition (no new type needed) |
| Cancellation | via CancellationToken on async APIs |
| Per-actor scope | each Actor owns its Channels collection |
| Per-call Service scope | App.Services collection, parent-tagged |
| Recursion safety | Goal channel writes resolve to original streams |

## What "standard" channel/streaming systems have that we don't

Six gaps worth naming. Some are this-branch-deferrable, some need design before they bite.

### 1. Backpressure / flow control

Reactive Streams, Go channels (bounded), .NET `System.IO.Pipelines` all express "the consumer can ask for N more, the producer waits." Without it: a slow consumer + fast producer = unbounded memory growth or dropped data.

We don't have it. Today our writes are fire-and-forget through a Stream. For a TCP/WebSocket Conversation channel feeding from a fast remote, this matters.

**Defer or address.** Defer for this branch (no consumers exercising it yet). When TCP/WebSocket lands, design then — probably a `BufferPolicy` enum on Channel: `Block` / `Drop-Oldest` / `Drop-Newest` / `Error`.

### 2. Heartbeat / keepalive

For Session channels (kept-open), connections die silently. WebSocket has ping/pong, TCP has keepalive. Without it: the channel reports "open" but writes silently fail or hang.

We don't have it. Stream channels backed by `System.IO.Stream` inherit whatever the underlying transport does (TCP keepalive at OS level), but no first-class concept.

**Address when persistent connections land.** A `Heartbeat: TimeSpan?` config on Session channels — if set, the runtime probes periodically and surfaces a typed `ChannelDead` error.

### 3. Reconnect / retry

Adjacent to keepalive. When a connection drops, do we retry? With backoff? How many times?

We don't have it. Current design: connection drops → channel errors out → propagates. Recovery is the user's problem (could write a goal that re-registers).

**Probably user-space.** A reconnecting channel is a goal channel that wraps a Stream channel and reopens on error. Composable. Don't need new infra.

### 4. Async iteration

Modern languages: `await foreach` over a channel reads continuously. C# `IAsyncEnumerable<T>`. PLang: we have `ask` (one-shot suspend) but no streaming iterator.

We don't have it. For TCP/WebSocket message streams, every message would otherwise need explicit polling.

**Already addressed indirectly via events.** Conversation channels expose `on message call DoStuff` — that's the iteration. Each message fires `DoStuff`. Functionally equivalent to async iteration without the iterator abstraction.

### 5. Buffering policy

`Buffer` today is just a byte size. What happens when the buffer is full? Block? Drop? Error? Unspecified.

**Address when it matters.** A `BufferPolicy` enum (above) settles it. Default `Block` (back-pressure caller). Other options for explicit drop-tolerant systems.

### 6. Observability / metrics

Channels are I/O surfaces. Knowing write count, byte count, latency distribution, error rate per channel is high-value. None today.

**Defer to a separate concern.** Probably hooks into PLang's existing event/snapshot system rather than a Channel-specific feature. Snapshots already capture state; metrics are a thin layer above.

### Tradeoff summary

Defer: 1, 2, 4, 5, 6 — they need real consumers to motivate the right shape. Address (1) and (2) when TCP/WebSocket lands; (5) at same time. (3) doesn't need core support. (4) is already covered by the events story. (6) is a separate observability initiative.

What this branch ships covers everything HTTP and console need, which is the realistic forcing function.

## What PLang can do that others can't

The genuinely novel territory — things other languages cannot do without writing them from scratch.

### A. Channels are language primitives the LLM-builder reasons about

In Go, Java, Rust, C# — channels are library abstractions. Developers thread them through code. The compiler doesn't know what they are; the LLM doesn't either.

In PLang, the builder catalog includes the channel inventory per actor. The LLM reads `- write 'hi' to debug` and *resolves intent against an inventory*. Other languages can't do this — channels aren't first-class enough to surface to the toolchain. A user can write fan-out, retry, transformation in plain English and the builder picks the right channel.

### B. Provably-authenticated channels

Every byte through every channel is signed by an Identity (System by default, or whatever the actor carries). Audit logs aren't a feature you bolt on — they're a consequence of the channel architecture. Replay any channel's history and verify integrity cryptographically. No language ships this; it's always a library or middleware.

Concrete capability: "show me everything `User` wrote to `output` last hour, with proof none of it was tampered with."

### C. Suspend / resume across channels

Other languages: read from a channel, get bytes, return. If you want to "ask the user a question," you do an out-of-band UI mechanism.

PLang: a channel's `Ask` can suspend the entire app, serialize state, resume later when the answer arrives. This works across HTTP boundaries, across days, across process restarts. The suspend/resume infrastructure is already shipped (callback branch). Channels expose it polymorphically — Session does direct read, Message returns a callback the runtime reifies.

No language ships this. Erlang has selective receive but not multi-day persistence. Phoenix Channels reconnect but don't persist suspended computations.

### D. Channels as goals — programmable middleware in the language

A channel can BE a goal. So channels are *computable*: filter, transform, encrypt, route, batch — all written as ordinary PLang goals. Other languages need pipeline frameworks (Rx, Akka Streams, Kafka Streams, gRPC interceptors). PLang gets it from one primitive: goal-as-channel + recursion rule.

Concrete: redact PII from `output`, audit-log all `error` writes, fan-out `notify` to email + Slack + Teams — each is a 3-line goal. No framework needed.

### E. Cross-actor channels with identity boundaries

Two PLang apps talking: User actor on app A writes to a channel; bytes arrive at app B's channel signed by A's identity. App B's runtime *automatically* validates the signature against known identities and rejects unsigned data. Auth handshake is inherent — neither app writes auth code.

In every other language: each protocol layer (HTTP, gRPC, MQTT) does its own auth, with its own libraries, with its own bugs. PLang collapses the layer.

### F. Time-travel debugging includes channel state

Snapshots (runtime2-callback) capture the App. They capture which channels are registered, what's bound to each role, who their parent is, what their identity is. You can replay from any snapshot and the channel topology is exactly what it was. "What was `output` set to when this bug fired?" — answerable from the snapshot, not folklore.

This is unique to having snapshots + channels both as language primitives. Most systems have one or the other.

### G. Self-describing streams via Data envelope

Every Data carries type info, properties, signature. A channel can negotiate based on what's flowing through. Reader knows "I got a `Person` record with these fields, signed by X, expiring at Y." gRPC needs `.proto` files. PLang gets it from `Data.Type`.

Concrete: an HTTP channel can route to different handlers based on `Data.Type` without parsing the body. The type is on the envelope.

## The disposition

Most "missing standard features" are deferrable until forcing functions arrive (TCP, WebSocket, observability initiative). The PLang-unique territory is mostly already enabled by the architecture we're shipping — what's left for the future is exposing it through tooling (the audit/replay UX, the suspended-channel debugger, the cross-actor handshake demo).

For this branch: ship the foundation. The unique capabilities emerge naturally from the OBP shape we've chosen, not from extra features.
