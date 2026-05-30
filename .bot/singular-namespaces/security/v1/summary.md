# security — singular-namespaces — v1

## What this is

First security review on `singular-namespaces` (origin/runtime2..HEAD,
47 commits, 860 files changed). Branch is 4 stages atop a mostly
mechanical rename:

- **Stage 1** — folder/namespace rename to singular + lowercase
  (`app.modules` → `app.module`, `app.types` → `app.type`,
  `app.channels` → `app.channel`, `app.variables` → `app.variable`,
  `app.events` → `app.event`, plural collapses to `<x>/list/this.cs`).
- **Stage 2** — `actor.context.@this` / `app.@this` / `data._context`
  flipped to non-null (`null!` default + setters that no longer guard).
- **Stage 3** — `app.X` becomes a collection-node accessor:
  `app.X[name]` throws on miss; `app.X.list` enumerates;
  `app.Goal.current` reads the callstack.
- **Stage 4** — `type.@this` promoted to a real entity behind
  `data.Type`; `app.builder.type.Entry` parallel struct folded onto it;
  `type.list.@this.BuildTypeEntries` populates fold properties at
  catalog-walk; off-catalog reads `Promote()` and throw on unstamped
  non-primitive reads.

Prior to this branch: coder v3 + tester v3 + codeanalyzer v4 on the
same scope. No prior security pass.

## Verdict

**pass.** Zero high, zero medium open. Three low residuals
(none reachable on current call graphs; all latent on future
expansion).

| New | Severity | Title |
|---|---|---|
| F1 | low | `data._context = null!;` discipline is fragile — Wrap/Compress unguarded reads happen to be self-protecting via `Type.Kind`-null-coalesce; any future unguarded `_context.X` becomes an NRE on wire-deserialized Data |
| F2 | low | `Channel.@this.InvokeChannelHandler` forces null context with `binding.Handler(context!, ...)` for service-owned channels (handler signature is non-nullable) — relies on the convention "handlers don't read context"; one non-conforming binding NREs through the catch-bandwidth in `WriteAsync`/`ReadAsync`/`AskAsync` |
| F3 | low | `Wire.Read` does not stamp `Context` on the returned `Data` — by design (caller's job), but future channel-ingest wiring needs to remember it; same shape as the standing callback-wire `[Sensitive]`-filter gap |

All standing findings from prior branches **unchanged** by this rename
(see Memory: `Variables.Snapshot` no `[Sensitive]`, `OpenAi`
`ReadAllBytes` no cap, `callback.run` skips signing when
`RawSignature==null`, channel `Stream.ReadAllBytesAsync` ignores
`Buffer`, `AppChannels.Channel(string)` parity gap, `MigrationEnvelope`
keyless `Signature` shape).

## Process

1. **Static rules** — `scripts/semgrep-scan.sh` ran clean against the
   baseline of 15 known INFO hits. One new hit at
   `PLang/app/builder/type/Render.cs:91,102,115` — `JsonSerializer.Serialize`
   on `ActionRecord`/`ActionSpec` graphs at build time. Reviewed:
   build-time only, no `path.@this` / `[Sensitive]` / cycle reachable,
   accepted (consistent with the other 9 baseline hits flagged on
   similar build-time renderers).
2. **High-risk areas walked**:
   - **Type entity** (`PLang/app/type/this.cs`, `type/list/this.cs`) —
     `Promote()` throws InvalidOperationException on unstamped
     non-primitive reads, which is a developer-loud fail (intended);
     unreachable as user-input DoS since the throw is on the schema
     properties, not on identity reads. Index-miss `KeyNotFoundException`
     on `app.Type[name]` likewise. Recursion-bounded via
     `MaxGenericDepth = 20`.
   - **AuthGate canonicalization** (`PLang/app/type/path/file/this.cs:31-48`,
     `path/this.Authorize.cs:108-138`) — `Canonicalize` still routes
     every FilePath ctor through `PathHelper.GetFullPath`; the closed
     finding from `purge-systemio-from-actions/v2` (pattern:
     `pattern_authgate_canonicalization`) is **intact** after the
     rename. `Absolute.StartsWith(rootWithSeparator)` matches the
     canonicalized form.
   - **Signing pipeline** (`PLang/app/module/signing/code/Ed25519.cs:63-154`)
     — 9-step `VerifyAsync` intact: type → freshness → expiry → nonce
     replay → contracts → headers (constant-time `FixedTimeEquals`) →
     data-hash → signature. `ToSigningBytes` thread-safety unchanged.
     `Signature` class structure preserved across rename.
   - **`Assembly.LoadFrom`** — only one caller surfaced via semgrep
     (`PLang/app/type/path/file/this.Operations.cs:35`); upstream
     callers are the documented accepted-risk paths (`module/add.cs`,
     `code/load.cs`, `code/this.Snapshot.cs`). `Verb { Execute }`
     gate distinct from Read (Unix r/w/x model) is preserved.
   - **Wire depth limits** — `Wire.MaxReadDepth = 64` (intact),
     `Normalize.MaxNormalizeDepth = 128` (intact). No new
     un-bounded recursion introduced.
   - **`data.@this.Type` non-null flip** — Wire write side
     (`data/Wire.cs:386-393`) gates on `!data.Type.IsNull`; legacy
     `body.Type != null` check at `Wire.cs:200` is now always-true
     but harmless because the Type setter handles the `Null` sentinel.
     `Type.Kind`/`Compressible` graceful for unstamped Data (returns
     null/false via `Context?.` chains) — Wrap/Compress's removed
     `_context == null` guards happen to be safe because the
     null-Kind path returns `this` before any `_context.X` deref.
   - **`app.X[name]` throw-on-miss accessors** —
     `channel/list/this.cs:146`, `goal/list/this.cs:234`,
     `type/list/this.cs:218`, `format/list/this.cs:366` all throw
     `KeyNotFoundException` on miss. Search of call sites shows
     none currently fed from PLang user input — the user-input
     channels still route through `Get(name)` + `Data.FromError`
     (`app.module.file.read.cs:96` uses the NoOp-fallback
     `.Channel("builder")`, not the throwing indexer). Latent
     DoS-via-exception surface if PLang code starts using the
     throwing indexer with untrusted names — flagging here for
     future-vigilance, not a finding today.
3. **Mutation discipline**: no production edits made — pure reading
   pass. Standing memory items re-checked for rename damage; all
   intact at the new paths.

## F1 — Data._context = null! discipline (low)

**Where:** `PLang/app/data/this.cs:25` declares `_context = null!;`;
`this.Transport.cs:75` (Wrap), `:114` (Compress) and `:183`
(Decompress) read `_context.Actor?.Channel.Serializers...` (formerly
`_context?.Actor?.Channels.Serializers...`).

**What:** the Context property is typed non-nullable but defaults to
literal null at runtime. The `_context == null` guard at Wrap/Compress
was removed in the Stage-2 flip. Today the code path is self-protecting
because `Type.Kind` reads `Context?.App.Format.KindOf(Value)` via
nullable-chain on the **type entity's** Context (which mirrors Data's
Context), so an unstamped Data has `Kind == null` and returns `this`
before reaching the unguarded `_context.Actor`. The discipline is
fragile: a future caller adding any `_context.X` outside the
`Type.Kind`-null-coalesce shadow becomes an NRE on every
wire-deserialized Data (`Wire.Read` does not stamp Context — see F3).

**Severity:** low. Not reachable today (Wrap/Compress both bail
before the unguarded deref; EnsureSigned has its own explicit
throw at `:52-55`; ToSigningBytes is read-only on local
properties). Becomes a real NRE the moment a new Transport-side
method dereferences `_context` directly. Documented here so a
future audit catches the regression early.

**Fix posture (not applied):** either restore the `?` on the
field declaration and `?.` on the call sites, OR document the
no-context-Data class invariant on the property and add an
explicit assertion at the top of each method that reaches
`_context.X`. The architect's Stage-2 doc explicitly calls
the un-stamped Data path out as "intended NRE — that's the
bug the `?.` was hiding," so the design intent prefers
fail-loud; an explicit throw with a clear message beats an
NRE deep in the call chain.

## F2 — Channel handler null-context force (low)

**Where:** `PLang/app/channel/this.cs:236-252`. The diagnostic
notes "Most handlers don't read context" and forwards `null` to
`binding.Handler(context!, null, data)` when the channel has no
Actor (service-owned channels constructed without an Actor).

**What:** `binding.Handler` is the user-installed channel-event
handler delegate; its signature is `(actor.context.@this, action?,
data) → Task<data>`. Forcing `null` through `context!` works as
long as the handler doesn't dereference context. A
non-conforming binding (e.g. user installs an event.on dispatch
handler against a service-owned channel) NREs inside the
handler — caught by the outer `WriteAsync`/`ReadAsync`/`AskAsync`
catch (line 125, 146, 164) and surfaces as `WriteError`/`ReadError`/
`AskError`. So this is **DoS-bounded** (one channel call fails
loudly), not a corruption or auth-bypass risk.

**Severity:** low. Not exploitable beyond per-call error. The
Debug.Write at `:250` surfaces the case in --debug output, so
operators can see the misconfiguration.

**Fix posture (not applied):** either skip the handler when
context is null (silent skip — risks masking misconfig), or
change the handler signature to `actor.context.@this?` (forces
all handlers to null-guard — broader API surface change). Today's
choice (forward null with `!` and diagnostic) is defensible.
Flagging because the `null!` forcing was new on this branch
(Stage 2 nullability flip) and the catch-bandwidth that contains
the NRE is broad — `Exception ex` minus a handful of
process-fatal types.

## F3 — Wire.Read does not stamp Context (low)

**Where:** `PLang/app/data/Wire.cs:129-156`.

**What:** the wire converter reads a Data envelope from JSON and
returns it; it never sets `Context`. By design — the caller
(`PlangDataSerializer`, callback dispatch, future channel ingest)
is the only thing that knows the right Context to stamp. Stage-2
flipped `_context` to non-null with a `null!` default, so a
deserialized Data is in the same "looks non-null at compile time,
is null at runtime" trap as F1.

**Severity:** low today. The current callers
(`PlangDataSerializer.DeserializeAsync`, internal callback
dispatch) all wire Context immediately after Read. The risk is
future: when a new wire-ingest path lands (channel HTTP receive,
new transport) and forgets to stamp Context, the resulting Data
silently NREs on the first `_context.X` read. Same shape and same
latency-to-real-impact as the standing
"callback wire serializers don't apply `SensitivePropertyFilter`"
finding (Memory).

**Fix posture (not applied):** convention is fine; the call-site
audit happens at channel-ingest commit time. Worth a paragraph in
`Documentation/Runtime2/data-spec.md` under §15a noting that
`Wire.Read` returns a Context-less Data and every new ingest path
**must** stamp before reading.

## Standing findings — re-checked after rename

All standing findings from Memory walked through their new home;
none broken by rename:

- `Variables.Snapshot()` no `[Sensitive]` → still at
  `PLang/app/variable/list/this.cs:723-733` (was
  `PLang/app/variables/this.cs`). Same Medium standing.
- `path.@this.IsUnder` canonicalization closed at v2 → confirmed
  intact at `PLang/app/type/path/file/this.cs:31-48` +
  `path/this.Authorize.cs:130-138`. Regression suite at
  `PLang.Tests/App/Types/PathTests/DotDotTraversalRegressionTests.cs`
  still present (file unchanged by rename — under
  `PLang.Tests/App/Types/` which keeps PascalCase).
- `callback.run` signing-skip gap (Medium standing) — file moved
  to `app/module/callback/run.cs`; logic unchanged.
- `AppChannels.Channel(string)` NoOp-fallback parity gap (Low
  standing) — file at `app/channel/list/this.cs`; `Channel(name)`
  method (around line 110-130 in new file) still uses raw
  `_channels.TryGetValue`. One reachable caller at
  `app/module/file/read.cs:96` writes "builder" warnings — pattern
  unchanged.
- `MigrationEnvelope` keyless Signature shape (Low/latent) —
  unchanged.
- `Channel.Stream.ReadAllBytesAsync` ignores Buffer (Medium today,
  stdin-local) — unchanged.

## Code examples

The Stage-2 flip and what makes F1 latent rather than active:

```csharp
// PLang/app/data/this.cs:25
private actor.context.@this _context = null!;    // typed non-null, runtime null until stamped

// PLang/app/data/this.Transport.cs:73-86 — Wrap
public @this Wrap()
{
    if (Type == null) return this;               // dead — Type is non-null (returns type.Null)
    var kind = Type.Kind;                        // Type entity's Context?.App.Format.KindOf(Value)
    if (kind == null) return this;               // <-- live shield: null Context → null Kind → return
    var outer = new @this("", this, type.FromName(kind));
    outer.Context = _context;                    // assigning null is fine
    return outer;
}

// PLang/app/data/this.Transport.cs:108-115 — Compress (same shield)
public async Task<@this> CompressAsync(...)
{
    if (Type == null) return this;               // dead
    if (!Type.Compressible) return this;         // <-- live shield: null Context → Compressible=false
    var serializer = _context.Actor?.Channel.Serializers...   // unguarded — would NRE without the shield above
}
```

The `Type.Kind`-null-coalesce is the only thing keeping Wrap and
Compress safe on unstamped Data. Any new method that reads
`_context.X` outside that shadow becomes a real NRE on
wire-deserialized payloads.

## Run results

- semgrep — 16 findings (15 baseline INFO + 1 new INFO at
  `builder/type/Render.cs`, build-time renderer, no sensitive graph
  reachable → accepted).
- Prior bots' green claim (PLang 253/253, C# 3696/3696) not
  re-run by security — relied on as honest per `tester v3 PASS`
  and `codeanalyzer v4 PASS`.

## Next bot

**Next bot: docs.** Three low residuals are documentation gaps,
not code defects: F1 wants a class-invariant note on
`data.@this.Context`; F3 wants a `data-spec.md` §15a paragraph
on `Wire.Read` Context-less return. F2 is a defensible runtime
choice that does not need follow-up. No coder action required.
