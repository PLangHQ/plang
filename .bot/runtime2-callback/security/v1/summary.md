# security — runtime2-callback — v1

## What this is

Independent security review of `runtime2-callback` (Stages 1–4: snapshot,
callstack frames, lazy signing, callback records + verbs). Auditor v1
(PASS — F1/F2 closed in `3fbc7cfa`) and tester v1 (PASS — 16 quality
findings, 2720+192 green) already cleared the branch. This is the third
gate: threat-model the wire surface and look for what static review and
test coverage don't catch.

Subject diff: `origin/runtime2-callstack..HEAD` (everything Stages 1–4
add). Ad-hoc consumer is the `output.ask` + `AskCallback` resume path;
no real HTTP/channel ingest of callbacks is wired yet — *that's the
load-bearing fact for severity.*

## Verdict

**pass with one Medium and three Lows.** Zero High. The Medium is a
design-pattern bypass that becomes exploitable the moment a real channel
wires a callback wire into `callback.run`; the Lows compound with it.

| # | Sev | Title |
|---|---|---|
| F1 | **Medium** | `callback.run` skips `signing.verify` when `RawSignature == null` — wire-arriving callback with the signature stripped passes auth |
| F2 | Low | `Variables.@this.Restore` and `ErrorCallback` wire path don't filter `!`-prefix names — mirror of AskCallback F2 fix, missing on the Error path |
| F3 | Low | `JsonSerializer.Deserialize` on AskCallback / ErrorCallback wires has no length / depth caps — DoS via large body if the channel doesn't bound it |
| F4 | Low | Callback wire serializers (`PlangDataSerializer`, `ErrorCallback.SerializeSnapshot`, `AskCallback.Wire`) don't apply `SensitivePropertyFilter` — `[Sensitive]` Variables values flow through unmasked |

| Auditor v1 finding | Status |
|---|---|
| F1 — `PositionWire.Resolve` index bounds | **closed** (commit `3fbc7cfa`, mirrored in AskCallback) |
| F2 — AskCallback `!`-prefix on Run | **closed** (commit `3fbc7cfa`) |
| N1 — `ErrorCallback.Position` always null pre-Run | open (note, not a security finding) |
| N2 — `SnapshotAt` drops Type/Properties | open (note, not a security finding) |
| N3 — Restore hard errors as CLR exceptions | open (note — also surfaces in F1 below; see "Adjacent risks") |

## Findings

### F1 — Medium: `callback.run` skips signature verify when no signature is present

**File:** `PLang/App/modules/callback/run.cs:27-35`

```csharp
if (Callback.RawSignature != null)
{
    var verifyResult = await Context.App.RunAction<verify>(...);
    if (!verifyResult.Success)
        return Data.FromError(...CallbackSignatureMismatch...);
}
return await cb.Run(Context);
```

Comment says: *"skip when the callback was constructed in-process and isn't
sealed."* That's a correct rationale **for in-process callbacks** (e.g.
`output.ask` returns Data&lt;AskCallback&gt; into `%cb%`, the next step does
`- run %cb%`). But the same gate also fires for **wire-arriving callbacks**:
`PlangDataSerializer.FromEnvelope` populates `d.Signature = env.Signature`
straight off the JSON envelope — if the attacker sends `{type, value}` with
no `signature` field, `d.Signature` is null, `RawSignature` is null, verify
is skipped.

Today the practical impact is bounded: no HTTP/channel handler currently
deserializes a wire into `Data<ICallback>` — `AskCallback.Deserialize` and
`ErrorCallback.Deserialize` are only called from tests. **That bound
disappears the moment Stage 5 (or whoever wires HTTP receive of callbacks)
lands.** The architectural gate as written cements the bypass.

What the bypass enables (assuming a future channel call path):

- For AskCallback: attacker chooses any `(GoalPrPath, GoalHash, StepIndex,
  ActionIndex)` triple referring to a real goal in the live registry;
  `vars` payload of arbitrary `(name, value)` pairs (filtered to non-`!`
  per F2 fix); `Answer` field is *not* in the wire shape, so they can't
  short-circuit the resumed `ask` directly via the wire — but see F2:
  ErrorCallback path lets them inject `!ask.answer`.
- For ErrorCallback: full position + arbitrary variables (no `!`-prefix
  filter on this path — F2). Restore + dispatch from wire-controlled
  position.

The signature pipeline itself (Ed25519, nonce-replay cache, expiry, hash
match in `Ed25519Provider.VerifyAsync`) is solid — the bypass isn't the
cryptography; it's that `callback.run`'s gate treats absence-of-signature
as "trust" rather than "reject". v1 = identity *encryption* doesn't excuse
this — *signing* is real in v1 (`EnsureSigned` runs `signing.sign` for
ICallback values via Ed25519Provider).

**Fix (sketch):**

The handler already has all the structure it needs — what's missing is a
distinction between "in-process" and "off-the-wire". Three workable shapes:

1. **Reject null on the wire path.** Move the verify gate up: deserializing
   from `application/plang+data` always sets a deserialized-from-wire flag
   on the Data; `callback.run` requires `RawSignature != null` if that flag
   is set, otherwise allows skip.
2. **Always require a signature.** Auto-call `EnsureSigned` for in-process
   callback values before dispatch — symmetrical with how `Data.Signature`
   already lazy-populates on read for ICallback. Then the gate becomes
   `if (RawSignature == null) Reject`. Less surface area; in-process and
   wire paths look the same to the gate.
3. **Move the gate into the channel.** Don't trust `callback.run` to
   distinguish; require channels (HTTP receive, etc.) to call
   `signing.verify` themselves before constructing the typed
   `Data<ICallback>` they hand to PLang. `callback.run` becomes a pure
   dispatch.

Recommend (2): symmetric, simplest mental model, smallest surface.

**Why Medium not High:** not exploitable today (no wire ingest). Becomes
High the moment a channel feeds `callback.run` from outside.

---

### F2 — Low: `Variables.Restore` and `ErrorCallback` wire path don't filter `!`-prefix names

**Files:** `PLang/App/Variables/this.Snapshot.cs:35-46`,
`PLang/App/Callback/ErrorCallback.cs:104-109`

`Variables.Capture` correctly filters `!`-prefix names (system/infra
variables: `!app`, `!fileSystem`, `!error`, `!ask.answer`, …):

```csharp
foreach (var kvp in _variables)
{
    if (kvp.Key.StartsWith("!")) continue;   // outbound filter
    ...
}
```

`Variables.Restore` does **not**:

```csharp
foreach (var data in captured)
    target.Set(data.Name, data.Clone());     // no inbound filter
```

`ErrorCallback.DeserializeSnapshot` reads VarWires straight off the wire and
writes them under `varsSection.Write("variables", captured)`. On `Run`,
`App.Restore` calls `Variables.@this.Restore` which then `Set`s them all
unconditionally — **including any `!`-prefixed names the attacker chose.**

This is the same vector auditor's F2 closed for AskCallback (commit
`3fbc7cfa` added a `!`-prefix skip in `AskCallback.Run`). The fix didn't
get applied to:

- The ErrorCallback wire path (which goes through `Variables.@this.Restore`
  rather than its own bind loop).
- `Variables.@this.Restore` itself (filter at the source covers both
  callbacks and any future Snapshot consumer).

Compounded with F1: an attacker submitting an unsigned ErrorCallback can
inject `!ask.answer` to short-circuit a downstream `ask` to their chosen
value, set `!error.*` fields read by error handlers, and so on.

**Fix:** mirror auditor's F2 fix in `Variables.@this.Restore` (one line):

```csharp
foreach (var data in captured)
{
    if (!string.IsNullOrEmpty(data.Name) && !data.Name.StartsWith("!")) continue;
    target.Set(data.Name, data.Clone());
}
```

(Or, equivalently, filter at the wire `VarWire` level inside
`ErrorCallback.DeserializeSnapshot` so the section never gets dirty entries —
but the Variables-level fix is cheaper and protects every future Snapshot
consumer.)

**Why Low not Medium:** independent of F1 the variables in question are
mostly informational (`!callStack`, `!error`); the most attack-amplifying
ones (`!ask.answer`) compound with F1. Fix it together with F1.

---

### F3 — Low: JSON deserialization of callback wires has no size/depth caps

**Files:** `PLang/App/Callback/AskCallback.cs:47`,
`PLang/App/Callback/ErrorCallback.cs:86-87`

```csharp
var wire = JsonSerializer.Deserialize<Wire>(plain, _options) ?? throw ...;
```

`_options` has CamelCase / case-insensitive / ignore-null — no `MaxDepth`
override (default 64; OK), no length cap. Variables list is unbounded.
`crypto.decrypt` is identity, so the byte buffer arrives unchanged.

`Data.@this.GZipDecompress` already enforces `MaxDecompressedSize = 100MB`
(zip-bomb defense). The callback wire path bypasses gzip entirely, so that
cap doesn't help. A multi-GB wire body fed straight into
`JsonSerializer.Deserialize` causes the pipeline to buffer + parse the
whole thing.

Today bounded by the same fact as F1 — no real channel feeds wires in.
When HTTP wiring lands, **the HTTP module must enforce a size cap before
it hands bytes to the callback deserializer**, and the callback layer
should still defense-in-depth a sane upper bound (1MB? 4MB?) so a
mis-configured channel doesn't break it.

**Fix:** add `MaxBytes` / `MaxVariables` constants and reject wires that
exceed. Mirrors the http module's existing size-limits pattern (memory:
`pattern_http_module_security.md`).

**Why Low:** defense-in-depth; channel-layer bound is the primary control,
and channels haven't wired callbacks yet.

---

### F4 — Low: Callback wire serializers don't honor `[Sensitive]`

**Files:** `PLang/App/Callback/AskCallback.cs:91-96`,
`PLang/App/Callback/ErrorCallback.cs:149-154`,
`PLang/App/Channels/Serializers/Serializer/PlangDataSerializer.cs:22-29`

`Data.@this`'s envelope JSON options apply `SensitivePropertyFilter.Strip`:

```csharp
private static readonly JsonSerializerOptions _envelopeJsonOptions = new()
{
    ...
    Modifiers = { SensitivePropertyFilter.Strip }
};
```

The three callback-wire serializer option blocks above do **not**. Variables
captured into a callback include the full Data envelope (Name, Value, Type,
Properties — `Variables.Capture` clones the Data shape, not just a
key/value). When a Variable's Value is an object with `[Sensitive]`-marked
properties, those properties cross the wire un-redacted.

Practical exposure today is narrow: `Identity.PrivateKey` is the only
shipping `[Sensitive]` property. An app would have to put an `Identity`
object into a user-namespace variable for it to be captured. Default
identity flows store it in the Settings store, which Variables.Capture
already excludes (`SettingsVariable` skip).

But the architectural pattern teaches the wrong thing: as users mark more
provider/result types `[Sensitive]`, callbacks become a side-channel that
bypasses the redaction layer. Same risk shape as the standing memory
finding `Variables.Snapshot()` in the test module (`feedback_secrets_in_test_artefacts.md`).

**Fix:** add the `SensitivePropertyFilter.Strip` modifier to each of the
three callback-wire option blocks. One-line each.

**Why Low:** narrow practical scope today; flagged to keep the architectural
contract intact.

## Re-verifying auditor's closures

Both auditor F1 and F2 fixes were re-walked:

- **F1 (PositionWire bounds):** `AskCallback.cs:128-132` now has both
  `stepIndex` and `actionIndex` guards mirroring `CallStack.this.Snapshot.cs:131-136`.
  Throws `CallbackGoalNotFound` with descriptive context (`stepIndex N out of range`
  / `actionIndex N out of range at step M`). Closed cleanly.
- **F2 (AskCallback `!`-prefix on Run):** `AskCallback.cs:73-77` now skips
  `!`-prefix names during the bind loop. Closed cleanly **for the AskCallback
  path** — see F2 above for the missing mirror on the ErrorCallback path /
  `Variables.Restore`.

## Adjacent risks (notes, not findings)

- Auditor's N3 (`CallbackGoalNotFound` / `CallbackGoalHashMismatch` propagate
  as CLR exceptions from `CallStack.Restore` and `PositionWire.Resolve`):
  combined with F1, an attacker who satisfies the signature gate but sends
  a bad position throws an unhandled CLR exception out of `cb.Run` → the
  caller's framing decides whether this becomes a 500 or a controlled error.
  Worth threading through the standard `Data.FromError(IError)` shape when
  the channel wiring lands. Not a security finding standalone.
- `Providers.@this.Restore` does `Assembly.LoadFrom(reg.Source)` — read it
  carefully. The Providers section is **not** populated by either
  AskCallback wire or ErrorCallback wire (`AskCallback.Wire` doesn't carry
  it; `ErrorCallback.DeserializeSnapshot` only fills CallStack + Variables
  sections). So the wire cannot reach the `Assembly.LoadFrom` path **in
  the current shape**. Standing memory rule already covers
  `Module.add`/`provider.load` as accepted-trust on `.pr`; same applies
  here. **If** ErrorCallback's wire format is ever extended to round-trip
  Providers, this becomes an immediate Critical — the fix at that point is
  to make `Providers.@this.Restore` reject sources that don't pass a
  signature/whitelist gate. Pre-emptive note for the architect for v2.
- ActorName, ActionModule, ActionName, Id wire fields: carried but not
  consumed by Restore. Inert today. Worth dropping to shrink the
  attack surface and the wire size.

## What was done

- Pulled `origin/runtime2-callback`. Diff stat against the merge base
  (`f8a0d641`): 186 files, ~11k lines.
- Read auditor v1 (`3fbc7cfa` PASS) and tester v1 (`f627becc` PASS)
  reports — context for what's already covered, not re-rolled.
- Walked each callback-wire entry point: `callback/run.cs`,
  `Callback/AskCallback.cs`, `Callback/ErrorCallback.cs`,
  `Variables/this.Snapshot.cs`, `Variables/this.cs` (Set, Restore),
  `CallStack/this.Snapshot.cs` (auditor F1 mirror site),
  `Providers/this.Snapshot.cs` (Assembly.LoadFrom path),
  `Channels/Serializers/Serializer/PlangDataSerializer.cs`,
  `modules/signing/providers/Ed25519Provider.cs` (verify pipeline),
  `modules/signing/Signature.cs`, `modules/crypto/{encrypt,decrypt}.cs`,
  `Data/this.Envelope.cs` (lazy-Sign + RawSignature peek).
- Cross-checked F1 against `CallbackRunActionTests.cs:22-30` — the test
  `CallbackRun_VerifiesSignature_BeforeDispatch` actually pins the bypass
  behaviour in: it sets no signature and asserts Success. (Tester report
  flagged this in #3 as a name/assertion mismatch; the security read is
  that the test pins the bypass as intended behavior — that's the gate
  worth changing.)
- Clean rebuild: `rm -rf {PlangConsole,PLang,PLang.Tests,PLang.Generators}/{bin,obj}`
  → `dotnet build PlangConsole`. Build clean (0 errors, 423 warnings — same
  baseline tester reported).
- Tests: `PLang.Tests/bin/Debug/net10.0/PLang.Tests` → **2720/2720**.
  PLang test suite: `cd Tests && plang --test` → **192 entries**, 6
  intentional fixture `[Fail]`s under `_fixtures_sensitive/` and
  `_fixtures_fail/` (consumed by meta-tests, not real failures, exactly
  matches tester's accounting).

## Recommendation

`pass` — branch is mergeable. The Medium **must** be addressed before any
HTTP/channel wiring lands callbacks (Stage 5+). F2 + F4 are one-line each
and worth bundling into the same fix pass. F3 is defense-in-depth, fine to
defer to when channel wiring lands as long as the channel-layer bound is
firmly in place by then.

For the architect: F1's resolution shape (require-signature for ICallback
dispatch) is a small but load-bearing decision — picking option (2) above
(`EnsureSigned` symmetric in-process) means in-process and wire callbacks
never diverge in their auth posture, which is the right invariant for a
runtime that's about to add many channels.

## Files written

- `.bot/runtime2-callback/security-report.json` (machine-readable)
- `.bot/runtime2-callback/security/v1/plan.md`
- `.bot/runtime2-callback/security/v1/verdict.json`
- `.bot/runtime2-callback/security/v1/summary.md`
- `.bot/runtime2-callback/security/summary.md` (top-level pointer)
