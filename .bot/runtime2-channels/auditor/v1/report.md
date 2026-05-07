# auditor v1 — runtime2-channels

**Scope:** independent audit on the state at `38f9d153` (coder v8 + tester v7 PASS + codeanalyzer v4 PASS). No security pass has run on this branch — the auditor here also lifts the wire-surface concerns that would normally be a security input. Re-walks the new channel architecture, the `Variables.Calls` overlay, and the Stage 9 migration stub.

**Verdict: FAIL.** Five new findings; A1 (doc lies about signature coverage) and A3 (real `AskCore` bug) need fix before merge. All prior codeanalyzer findings (F1/F4/F5/F6 + B1/L1) confirmed closed by independent reading. A2 / A4 / A5 are deferred to their downstream feature work and don't block this merge.

**Next bot: coder.** Two pre-merge items (A1 + A3); after those land, re-run auditor for the close-out pass.

Baseline rebuilt from clean: C# 2762/2762, PLang 201/201.

---

## A1 — High (latent) — `MigrationEnvelope.Signature` does not cover `Payload` or `Config`

**File:** `PLang/App/Channels/Channel/this.cs:295-319` (`SignEmpty` + `ComputeSignature`) and `PLang/App/Channels/Channel/MigrationEnvelope.cs:41` (Signature comment).

```csharp
// MigrationEnvelope.cs
/// <summary>Signature bytes over (Name, Direction, Config, Payload).</summary>
public required byte[] Bytes { get; init; }

// Channel/this.cs
protected static byte[] ComputeSignature(string name, ChannelDirection direction, string identity)
{
    using var sha = SHA256.Create();
    var input = Encoding.UTF8.GetBytes($"{name}|{(int)direction}|{identity}");
    return sha.ComputeHash(input);
}

public static bool VerifyEnvelope(MigrationEnvelope envelope)
{
    var expected = ComputeSignature(envelope.Name, envelope.Direction, envelope.Signature.IdentityName);
    return expected.SequenceEqual(envelope.Signature.Bytes);
}
```

The doc comment on `Signature.Bytes` claims the hash is "over (Name, Direction, Config, Payload)". The implementation hashes `(name, direction, identity)`. **Payload and Config are unsigned.** An attacker with envelope-modify access can swap `Payload` (e.g. the `GoalMigrationPayload.Variables` snapshot, or a `Stream.MemoryStream` byte buffer) or `Config` (Buffer / Timeout / Mime / Encryption / Signing) and `VerifyEnvelope` still returns true.

`Signing` is a real provider reference field — flipping it from a strict signer to "auto" or `null` and re-shipping the envelope is a downgrade attack the signature was meant to prevent.

**Practical bound today:** no receive-side transport ships. `Channel.FromMigration` throws `NotImplementedException`. The bypass is latent — it materialises the moment any consumer accepts envelopes off the wire and trusts `VerifyEnvelope`.

**Severity:** High the moment the receive side lands; Medium-trending-High today because the `migrate` action is a *live* PLang surface that already produces these envelopes (see A2). The doc lying about coverage is the worst part — anyone pasting the signature into a transport will assume tamper-resistance the code does not provide.

**Fix:** include `Config` and a stable hash of `Payload` in `ComputeSignature`. Pseudocode:

```csharp
protected static byte[] ComputeSignature(string name, ChannelDirection direction,
    string identity, ChannelConfigSnapshot config, object? payload)
{
    using var sha = SHA256.Create();
    var canonical = JsonSerializer.SerializeToUtf8Bytes(new { name, direction, identity, config, payload },
        new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
    return sha.ComputeHash(canonical);
}
```

Or — if Stage 9 deliberately defers real signing — change the `Signature.Bytes` doc comment to read **"over (Name, Direction, Identity) — Stage 9 stub; Payload+Config not yet covered"** and add `[Obsolete("Stage 9 stub — do not consume across the wire")]` on `VerifyEnvelope`. Either is acceptable; the hidden mismatch between doc and code is not.

---

## A2 — Medium — `migrate` action exposes a Variables snapshot to user code with no permission check

**File:** `PLang/App/modules/channel/migrate.cs:23-37` and `PLang/App/Channels/Channel/Goal/this.cs:96-112`.

`channel.migrate` is a normal `[Action("migrate")]` callable from any PLang goal. For a goal-backed channel, the produced envelope's `Payload.Variables` is `Actor.Context.Variables.Snapshot()` — the full actor variables dictionary minus `!`-prefix / Settings / Dynamic.

```csharp
// Goal/this.cs:96
var payload = new GoalMigrationPayload
{
    GoalName = Goal.Name ?? "",
    Variables = Actor.Context.Variables.Snapshot()
};
```

```csharp
// Variables/this.cs:689
public Dictionary<string, object?> Snapshot()
{
    var dict = new Dictionary<string, object?>(...);
    foreach (var kvp in _variables)
    {
        if (kvp.Key.StartsWith("!")) continue;
        if (kvp.Value is Data.DynamicData) continue;
        if (kvp.Value is App.Settings.SettingsVariable) continue;
        dict[kvp.Key] = kvp.Value.Value;
    }
    return dict;
}
```

Any user-authored goal can:

1. Register a goal-channel via `set channel "x" call AnyGoal` (or read an existing one).
2. Call `migrate channel "x"` to receive a `MigrationEnvelope` whose `Payload` carries every non-`!`-prefix variable in scope.
3. Write the envelope to a user-controlled channel (any registered channel, including HTTP outbound).

`[Sensitive]`-marked properties on captured object graphs *are* stripped when the envelope is later JSON-serialised through `PlangDataSerializer` / `JsonStreamSerializer` (both apply `SensitivePropertyFilter.Strip`) — that mitigates the worst case but does not cover bare values stored under non-`!` names by user code.

Additionally, **values are captured by reference** (`kvp.Value.Value`, line 697) — `Variables.Snapshot`'s xmldoc explicitly documents this is safe only because today's only caller is "called from assert handlers on failure only; the App is about to be disposed, so by-ref is safe." The migrate path violates that invariant: the envelope is supposed to ship across processes, where by-ref is meaningless and any post-snapshot mutation of the captured object before serialisation creates a TOCTOU between observation and wire-emission.

**Severity:** Medium. PLang's actor model already gives a goal full access to `Variables` in-process, so this isn't a privilege escalation against today's threat model. It becomes a real exfil path the moment migration grows a transport that ships the bytes off-host without an explicit user gesture.

**Fix shapes:** (a) gate `migrate` on `Actor.Identity == App.System.Identity` until transport ships; or (b) change `Snapshot()` to clone-by-value (use `Data.@this.SnapshotClone(value)`, mirroring line 261); or (c) split `Snapshot` into a "diagnostic / by-ref" shape and a "wire / by-value" shape, with a comment forcing migrate to use the latter. (c) is the cleanest given Variables.Snapshot's xmldoc already calls out the invariant.

---

## A3 — Low (real bug) — `Stream.AskCore` leaks `StreamReader` and ignores `Encoding`

**File:** `PLang/App/Channels/Channel/Stream/this.cs:115-119`.

```csharp
try
{
    var reader = new StreamReader(Stream, leaveOpen: true);
    var line = await reader.ReadLineAsync(timeoutCts.Token);
    return Data.@this.Ok(line ?? string.Empty);
}
```

Two bugs in three lines.

**(a) StreamReader buffer-leak across calls.** Each `Ask` allocates a fresh `StreamReader` over the same underlying Stream. `StreamReader` pre-reads up to 1024 bytes into its internal buffer on the first read. On the next `Ask`, a new StreamReader is allocated and reads from the underlying Stream — but bytes already buffered by the first reader are gone. Repeated `Ask` on the same stream channel will drop input bytes whenever a `\n` arrives in the same buffer window as a previous prompt's tail.

The reader also is not disposed (no `using`). It's `leaveOpen: true` so the underlying Stream survives, but the reader's own state and byte buffers leak per call.

**(b) `Encoding` ignored.** F5 (codeanalyzer v1) was fixed for `ReadAllTextAsync` and `WriteTextAsync` via `ResolveEncoding()`. `AskCore` was not updated — `new StreamReader(Stream, leaveOpen: true)` defaults to UTF-8 and ignores the channel's `Encoding` property. So a channel configured `Encoding = "iso-8859-1"` round-trips text correctly via `WriteTextAsync` / `ReadAllTextAsync`, but `Ask` reads it through UTF-8.

**Severity:** Low — `Ask` on a Stream channel is the stdin-prompt path. Today's only Stream-Ask call site is interactive console reads (UTF-8, single line), where neither bug surfaces. The moment a non-console Stream-Ask scenario lands (HTTP request body, custom encoding), both bugs hit at once.

**Fix:**

```csharp
using var reader = new StreamReader(Stream, ResolveEncoding(), detectEncodingFromByteOrderMarks: false,
    bufferSize: 1024, leaveOpen: true);
var line = await reader.ReadLineAsync(timeoutCts.Token);
return Data.@this.Ok(line ?? string.Empty);
```

The `using` still doesn't fix problem (a) cleanly across calls — a permanent reader cached on the channel would. But for the current scope, `using + ResolveEncoding` closes both the leak and the encoding mismatch and is a one-line change.

---

## A4 — Note (latent) — `Variables.Set` dot/bracket-path branch bypasses `Calls.Current`

**File:** `PLang/App/Variables/this.cs:213+` (the dot/bracket path branch after the `rootName == name` simple-case branch).

The simple-name branch routes writes through `Calls.Current` when an overlay is active (lines 92-191). The dot-path branch (line 213+) writes directly to `_variables[rootName]` and never consults the overlay.

Today this is inert: no production code path calls `Calls.Push`. The reframe in coder v6 ("push iff fork") explicitly removed Push from `RunGoalAsync(GoalCall)` and `GoalChannel.InvokeGoal`. So `Calls.Current` is always null in production paths today, and the gap doesn't fire.

The moment a real fork operator pushes (parallel foreach iteration, async listener accept-loop), any goal-body line of the shape `set %user.name% = "x"` inside the fork leaks to the actor-shared dict. The overlay isolation is partial.

**Severity:** Note — not blocking. Already on todos.md per coder v6 ("full parallel-branch Variables isolation deferred to parallel-foreach work"). Worth a comment in the dot-path branch explicitly flagging the gap so a future implementer sees it without grepping the bot reports.

---

## A5 — Note — `PlangDataSerializer` lacks `MaxBytes` / `MaxDepth` caps (S-F3 carry-over)

**File:** `PLang/App/Channels/Serializers/Serializer/PlangDataSerializer.cs:23-36`.

Same shape as security v1's S-F3 on runtime2-callback. `_options` does not set `MaxDepth` (uses default 64) and there is no byte cap on `DeserializeAsync(Stream, ...)`. `SensitivePropertyFilter` is correctly applied (S-F4 closed).

**Severity:** Note. Defense-in-depth. No channel currently feeds wire-arriving envelopes into `Deserialize<Data>` and forwards the result to a privileged sink — the same bound that held S-F3 at Low on `runtime2-callback`. Bundle with the Stage 9 transport work.

---

## Confirmed closed (re-checked from sources)

| ID | Origin | Outcome |
|---|---|---|
| F1 | codeanalyzer v1: `Services` `ConcurrentBag` race | Closed — `ConcurrentDictionary<Guid, Service>` + `Service.Id`; atomic `TryRemove` (`Services/this.cs:14, 30`). |
| F4 | codeanalyzer v1: `EventContext` dead code | Closed — type and shape-test deleted. |
| F5 | codeanalyzer v1: `Stream` text I/O ignored `Encoding` | Closed for `ReadAllTextAsync` / `WriteTextAsync` via `ResolveEncoding` (`Stream/this.cs:174-180`). **Not closed for `AskCore` — see A3.** |
| F6 | codeanalyzer v1: `channel.set` couldn't make Bidirectional goal channels | Closed — `Direction` parameter + `ResolveDirection` (`channel/set.cs:30, 74`). |
| B1 (v3) | `Events._active` was `static` | Closed — `private readonly` (`Events/this.cs:22`). codeanalyzer v4 PASS confirmed; auditor concurs by independent reading. |
| L1 (v3) | `Enter` mutated shared `HashSet` | Closed — copy-on-write + Releaser restores parent reference (`Events/this.cs:69-86`). codeanalyzer v4 PASS confirmed. |
| F2 / B2 (v1/v2) | `RunGoalAsync(GoalCall)` raced on shared Variables | Reframed, not fixed. Coder v6's "push iff fork" model is consistent (`Goal/this.cs:46-77` comment). Latent until a fork operator pushes — see A4. Not re-opened here; the design call is recorded and accepted. |

---

## OBP shape check — new types

Walked the new types per the CLAUDE.md OBP smell checklist:

- `App.Channels.@this` — public mutable surface is `Register/RemoveAsync/Get/Resolve`; no public List/Dict; `_channels` is private `ConcurrentDictionary` with discipline owned in-type. Clean.
- `App.Channels.Channel.@this` — `Events` slot is the new `Events.@this` type; `Metadata` is public `IDictionary` (compatibility with v1 surface, noted), but no cross-file lock on it. Acceptable.
- `App.Channels.Channel.Events.@this` — owns `_list` + `_lock` + `_active`; external surface is `Add/Match/IsActive/Enter/Count`. Clean (codeanalyzer v4 confirmed).
- `App.Variables.Calls.@this` + `Calls.Call.@this` — overlay owns its `_entries` + `Caller` walk; outer `@this` owns `AsyncLocal<Call?>` and the `RestoreCurrent` discipline. Clean. Note: `Call._entries` is plain `Dictionary`, not concurrent — relies on AsyncLocal flow-isolation. Read-time iteration during a parent flow's write to its own `_entries` is theoretically racy if a child flow ever inherits the parent reference and reads while the parent is still writing. Today no caller does — and fork operators that will push their own scope on top won't write into the parent's overlay either. Acceptable for the stated scope, but the safety argument depends on "no cross-flow sharing of a single Call object" — worth an xmldoc invariant.
- `App.Services.@this` + `App.Services.Service.@this` — flat collection, atomic Remove, Service.Id is the stable key. Clean.

No new cross-file lock targets. No "allocate-here / mutate-there / clean-up-elsewhere" splits introduced.

---

## Verdict matrix

| Finding | Severity | Confirmed? | Blocking |
|---|---|---|---|
| A1 — `MigrationEnvelope.Signature` doesn't cover Payload/Config | High (latent) | ✅ | Pre-merge for the receive side; **doc-vs-code mismatch should be fixed pre-merge regardless**. |
| A2 — `migrate` exposes Variables snapshot, no permission, by-ref | Medium | ✅ | Recommend pre-merge gate (System-only, or by-value snapshot) before any transport ships. |
| A3 — `Stream.AskCore` StreamReader leak + ignores Encoding | Low | ✅ | Recommend pre-merge — one-liner. |
| A4 — `Variables.Set` dot-path bypasses `Calls.Current` | Note | ✅ | Defer to parallel-foreach. Add inline comment. |
| A5 — `PlangDataSerializer` no size/depth caps | Note | ✅ | Bundle with Stage 9 transport. |

`fail` — A1 + A3 are pre-merge work. Stage 9 stub status doesn't excuse the doc/code mismatch on `Signature` (A1) or the `AskCore` regression that F5 missed (A3). Once coder addresses both, re-run auditor for the close-out PASS.

**Next bot: coder.** Scope:
- A1 — either expand `ComputeSignature` to include `Config` + `Payload`, or `[Obsolete]` `VerifyEnvelope` and rewrite the `Signature.Bytes` xmldoc to match what's actually hashed.
- A3 — one-liner in `Stream/this.cs:115-119`: `using var reader = new StreamReader(Stream, ResolveEncoding(), detectEncodingFromByteOrderMarks: false, bufferSize: 1024, leaveOpen: true);` — covers both the missing `using` and the encoding gap.
- Add a regression test for A3 (round-trip a non-UTF-8 line via Ask on a memory channel; assert input bytes aren't dropped across two consecutive Asks).

Out of scope for this coder pass (logged, deferred): A2 (gate `migrate`, by-value Snapshot — bundle with transport), A4 (dot-path overlay routing — bundle with parallel-foreach), A5 (size/depth caps — bundle with Stage 9 transport).

---

## What was done

- `git checkout runtime2-channels` (`38f9d153`).
- Read prior reports: `architect/v1`, `test-designer/v1`, `coder/{v1..v8}`, `codeanalyzer/{v1..v4}`, `tester/v7`.
- Clean rebuild: `rm -rf */bin */obj && dotnet build PlangConsole` → 0 errors / 454 warnings.
- C# tests: `dotnet run --project PLang.Tests` → 2762/2762 pass.
- PLang tests: `cd Tests && ../PlangConsole/.../plang --test` → final summary 201/201 pass.
- Re-walked the wire-and-fork surface from sources: `Channels/{this.cs, Channel/this.cs, MigrationEnvelope.cs, Channel/{Stream,Goal,Events}/this.cs, Serializers/Serializer/PlangDataSerializer.cs}`, `App/modules/channel/{set,remove,migrate}.cs`, `App/Variables/{this.cs, Calls/this.cs, Calls/Call/this.cs}`, `App/Services/{this.cs, Service/this.cs}`.
- No code edits — auditor reports, doesn't change code (per `98596b63` proposal).
