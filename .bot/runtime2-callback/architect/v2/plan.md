# Callback v2 — Phase 2: Signing, Storage, and Resume for Error-Retry

This is a delta on `v1/plan.md`. v1 settled the snapshot/restore design (what gets captured, the islands rule, the per-actor Variables, the Providers registry-layer snapshot, etc.). v2 covers the **wire format, signing, persistence, and resume mechanics** — and stages the work so error-retry can ship without ask-user/encryption.

Read v1 first for the underlying state model. v2 is the layer above it.

## What changed since v1

Three substantive insights from the v2 conversation:

1. **Signing is transparent at the Data IO boundary.** Any `Data.@this` written to a stream gets its `Signature` property populated automatically by the serializer (via the `signing` module). Default signature has *no expiry* — it's an integrity guarantee, not a validity gate. Developers who want time-bounded validity call `- sign %callback% expires in 24h` explicitly, which overwrites the default Signature.
2. **Goal already has a `Hash` property** at `PLang/App/Goals/Goal/this.cs:121` — SHA-256 of `Name + concatenated step text`. We use it directly for `Callback.GoalHash`. No new hashing infrastructure.
3. **Encryption belongs to the Callback class, not the Data layer.** Most Data writes don't need encryption (logs, files, debug output). Only Callback-in-ask-user-mode does. So encryption lives as a Callback-internal serialization concern and is a Phase 3 capability — Phase 2 ships without it.

These three combined mean Phase 2 ships entirely on existing infrastructure: the `signing` module, `Goal.Hash`, and developer-written PLang storage (`- write %callback% to file ...`). No new crypto, no new modules, no new core abstractions beyond what v1 already designed.

## The phase split

| Phase | Scope | New infra required |
|---|---|---|
| **Phase 1** | In-process error → callback → resume test, no signing, no storage | None — pure v1 mechanics |
| **Phase 2** *(this doc)* | Error-retry: signed envelope, developer-chosen storage, full resume cycle | None — uses existing `signing` module |
| **Phase 3** *(future v3 doc)* | Ask-user: HTTP wire transport, encryption | Extends `ICryptoProvider` with `EncryptAsync`/`DecryptAsync` |

Phase 2 captures most of the durable-execution value (any error becomes durably retriable). Phase 3 is the wire-bound special case.

## Schema corrections to `Callback`

From v1's draft schema, three changes:

```csharp
record Callback(
    string GoalPrPath,                              // NEW — relative path to .pr file (App.Goals loads goals lazily by path)
    string GoalHash,                                // = goal.Hash (SHA-256 of name + step text); drift = hard error on resume
    int StepIndex,
    int ActionIndex,
    string ActorName,                               // System / Service / User
    Dictionary<string, object?> VariablesByActor,
    Dictionary<string, object?> Selections,         // App.Providers + identity, by name
    Dictionary<string, object?> Statics,            // App._statics until that TODO closes
    List<IError> ErrorTrail,
    bool BuildEnabled,
    bool TestingEnabled
    // REMOVED: Expiry — now lives in Data.Signature (set by explicit `- sign expires in N` if wanted)
);
```

- **Added** `GoalPrPath` because `App.Goals` loads goals lazily by path. Without the path the resumer can't find the goal to load.
- **Removed** `Expiry`. It was duplicating what `Data.Signature` already carries (`SignedData.Expires`). Default integrity-only signing has no expiry; explicit time-bounded signing populates `Data.Signature.Expires`. One source of truth.

## Transparent Data signing — the IO hook

The `signing` module already has the high-level pipeline (`Ed25519Provider.SignAsync` at `PLang/App/modules/signing/providers/Ed25519Provider.cs:23`). What's missing is the *automatic invocation* on Data serialization.

Where the hook lives: `App.Channels.Serializers` is the IO boundary for Data. The serializer's Data-specific path needs one addition before writing:

```
if (data is Data.@this d && d.Signature == null)
    d.Signature = await signing.SignAsync(d, expiresInMs: null);  // default: no expiry
```

On read, the deserializer populates `d.Signature` from the serialized form as-is — *unverified*. Verification is the consumer's explicit step, not automatic on read (otherwise every Data read pays a crypto cost when most readers don't care about integrity).

Why default no-expiry: error callbacks may legitimately be inspected and re-run weeks or months later. Integrity ("this is the unmutated callback we issued") is the durable guarantee; validity ("still valid at time T") is opt-in via explicit `- sign expires in ...`.

**Performance.** Ed25519 signing is ~50µs; for the volume of Data IO PLang does, this is in the noise. If a future workload makes it a problem, the right fix is per-channel opt-out (debug/stderr channels skip signing), not removing the default.

**Open question for the coder:** does *every* Data write get signed, or only Data writes to "external" channels (file, http, named outputs) — debug/stderr/internal channels skip? Architectural answer: sign always. Implementation can add per-channel opt-out if a real perf case appears. Don't pre-optimize.

## Renames (OBP cleanup, coder task)

`SignedData` → `Signature` throughout `App.modules.signing`. The current name is a leftover from the early runtime2 rewrite before OBP shape was fully formed. The class represents *the signature*, not "signed data" — the data being signed is `Data.@this`, not the envelope. New canonical type: `App.modules.signing.Signature`.

Affected files (inventory, not exhaustive — coder verifies):
- `PLang/App/modules/signing/SignedData.cs` → rename file and class to `Signature.cs` / `Signature`.
- All references in `Ed25519Provider`, `sign.cs`, `verify.cs`.
- Any consumer that reads `Data.Signature` (which already exists as a property — check whether the property name needs adjusting; if it's typed `Signature`, the rename composes cleanly).

This is a mechanical refactor, low risk. Bundle into the same PR as v2 implementation or do it standalone first; coder's call.

## Encryption is Callback-internal — the layering decision

For Phase 3 (ask-user), the variable values transported over the wire need encryption (the user's browser shouldn't be able to read `%orderId%`). The encryption sits **inside Callback's own serialization**, *not* at the Data layer.

Layering for Phase 3 issuance:

1. Callback class encrypts its variable values internally — `Callback.EncryptInPlace(Context ctx)` walks `VariablesByActor`, calls `ctx.App.Modules.Get('crypto').EncryptAsync(value)` per entry.
2. The resulting `Data<Callback>` is serialized to bytes — Data layer signs the encrypted-variables-containing bytes (same transparent path as Phase 2).
3. Wire transport ships the signed envelope (base64 in an HTTP form field).

On Phase 3 resume:
1. Verify Data signature (existing `signing.verify`).
2. Callback class decrypts internally — `Callback.DecryptInPlace(Context ctx)`.
3. Hand off to `App.Run(callback)` as in Phase 2.

Why this layering:

- Most Data writes don't need encryption — keeping it out of the Data layer means logs, debug output, file writes, channel output stay unaffected.
- Encryption is Callback's discipline (OBP — class owns its own behavior). Other Data subtypes don't pay any cost.
- Signing wraps encryption naturally — the signature certifies the encrypted content as authentically issued.
- Phase 2 Callback ships *without* encryption methods; Phase 3 adds them. Forward-compatible since neither Phase has shipped yet.

## The Phase 2 movie

**Issue (developer's `on error` runs):**

```plang
- insert into users, name=%name%
   on error call goal HandleError

HandleError
- write %!error.callback% to file callbacks/%!error.id%.bin
```

Step-by-step:
1. `insert into users` blows up. Error is captured in `App.Errors`; handler dispatch begins.
2. `HandleError` runs as the recovery body.
3. `%!error.callback%` is read → lazy materialization triggers:
   - Build `Callback` record from the Errors trail's most recent error (action position, goal path, hash).
   - Populate `VariablesByActor` from each actor's Variables (with v1's exclusions).
   - Snapshot `App.Providers` registry-layer state, `App._statics`, `Errors.Trail`.
   - Apply throw-time vars correction via Diff reverse-apply (v1 mechanic).
   - Wrap in `Data<Callback>`. `Data.Signature` is *not yet populated* — it's a synthesized PLang property, not a serialized one.
4. `- write %!error.callback% to file ...` invokes file-channel write → serializer kicks in.
5. Serializer sees `Data<Callback>` with no Signature → calls `signing.SignAsync` with no expiry → populates `Data.Signature`.
6. Serialized bytes (Callback record + Signature) hit disk.

**Resume (developer triggers it):**

```plang
Recover
- read file callbacks/%id%.bin, write to %callback%
- run %callback%
```

Step-by-step:
1. `read file ...` reads bytes, deserializer reconstructs `Data<Callback>` with `Signature` populated (unverified).
2. `- run %callback%` invokes the callback module:
   - `signing.verify` checks signature integrity, expiry (no expiry by default → always passes), identity, contracts. Hard error on mismatch.
   - Look up goal: `app.Goals.LoadByPrPath(callback.GoalPrPath)`. Hard error if not found (referent integrity failure).
   - Compare `goal.Hash` against `callback.GoalHash`. Hard error on mismatch (signed but stale — redeployed goal).
   - Construct fresh `App` with the Callback as entry point.
   - `App.Run(callback)`:
     - `RegisterDefaults()` runs.
     - Replay runtime provider registrations + apply default selections from `Selections`.
     - Populate per-actor Variables from `VariablesByActor`.
     - Populate `Errors.Trail` from `ErrorTrail`.
     - Populate `App._statics` from `Statics`.
     - Apply mode flags (`BuildEnabled`, `TestingEnabled`).
     - Main loop's first tick lands at `(GoalPrPath → goal, StepIndex, ActionIndex)` → re-executes the failed action with the captured state.

That's the whole Phase 2 flow.

## Goal.Hash subtlety — known limitation

`Goal.Hash` covers the developer's prose (goal name + step text). It does **not** cover:

- The compiled `.pr.json` (so a recompile that doesn't change source produces the same hash — good, this is what we want).
- The behavior of modules referenced by steps. If `file.write`'s implementation changes between issue and resume in a way that changes step semantics without the prose changing, the hash stays the same and the resume runs against the new behavior.

For Phase 2, accept this. The dominant invalidation case is "developer changed the goal file," and `Goal.Hash` catches that. Module-behavior drift is a rare cross-cut concern. A future Phase could extend the hash to include module signatures (or the resolved `.pr.json` schema), but it's not warranted now.

Documented as a known limitation in the open threads, not blocking.

## Goal lookup on resume

`app.Goals.LoadByPrPath(prPath)` is the lookup. v1 noted that goals load lazily; the resumer needs the path to find one. If the file doesn't exist (developer deleted/moved it between issue and resume), the resume fails as a referent-integrity error — same shape as a deleted identity.

Actual API name (`LoadByPrPath` vs `GetByPath` vs whatever) is a coder lookup task. The architectural requirement: the App must be able to load a goal by its `PrPath` from the on-disk goals registry. Whether that involves filesystem scanning or an in-memory index is an implementation detail.

## Smallest meaningful first cut for Phase 2

Two-process simulation (one `App` instance per "process"):

```csharp
[Fact]
public async Task Phase2_signed_callback_round_trips_through_storage()
{
    // Issue
    var app = new App(/* goal: set %x%=1; throw "boom"; set %x%=2 */);
    await app.Run();  // throws

    var callback = /* read %!error.callback% from app.Context */;  // Data<Callback>

    // Storage round-trip — serializer auto-signs
    using var stream = new MemoryStream();
    await app.Channels.Serializers.SerializeAsync(new SerializeOptions {
        Stream = stream,
        Data = callback,
        ContentType = "application/json"
    });
    var bytes = stream.ToArray();

    // Fresh process — new App, deserialize, verify, run
    var resumer = new App();
    var deserialized = await resumer.Channels.ReadAsync<Data.@this<Callback>>(...);

    var verified = await resumer.Modules.Get<verify>().Run(deserialized);
    Assert.True(verified.Success);

    var goal = resumer.Goals.LoadByPrPath(deserialized.Value.GoalPrPath);
    Assert.NotNull(goal);
    Assert.Equal(deserialized.Value.GoalHash, goal.Hash);

    var resumed = new App();
    await resumed.Run(deserialized.Value);

    Assert.Equal(2, resumed.Variables["x"]);   // step after the throw ran
    Assert.Equal(1, deserialized.Value.VariablesByActor["x"]);  // throw-time view
}
```

If this passes, the whole Phase 2 stack — transparent signing, storage round-trip, signature verification, goal-hash gating, snapshot restore, position seek — is green. Phase 3 (encryption + wire transport) layers on top of this without changing any of it.

## Open threads — Phase 2 deferred

Items that don't block Phase 2 shipping:

1. **Per-channel signing opt-out.** Currently every Data IO signs. If a real perf case shows up (high-volume debug logs, stderr spam), add per-channel opt-out flags. Not now.
2. **Key rotation.** PLang's `IKeyProvider` has a single key today. If the System actor's signing key rotates, outstanding callbacks signed with the old key fail verification. Address with a key-ring (current key for new signing, recent keys accepted for verification). Phase 2 ships with single-key, single-version. Document as known limitation.
3. **Error code surface for `goal_hash` mismatch.** Hard error is settled; the actual error code (`CallbackGoalHashMismatch`?), error message format, and PLang `on error` handling shape are coder/test-designer-level decisions.
4. **`App._statics` TODO closure.** When the goal-backed dynamic property replacement lands, drop `Statics` from the `Callback` schema.
5. **Module-behavior drift not in `Goal.Hash`.** Documented as known limitation. Could extend hash in a future Phase if needed.
6. **`-run %callback%` action shape.** Lives in a `callback` module presumably (`callback.run` action). Builder annotation, parameter naming, return shape — coder/test-designer call.
7. **Loading a Callback from a non-Data wrapper.** If a developer reads from a backend that returns plain JSON (not Data-wrapped), how does `- run` reconstitute the `Data<Callback>` envelope and verify? Probably the deserializer auto-wraps when reading typed `Data<T>`, but worth confirming.

## Settled rejections (Phase 2-specific)

Adding to v1's list:

- **Explicit `- sign %callback%` at every issue site.** Rejected. Default signing is transparent at the IO boundary; developers only call `- sign` explicitly when they want to override (custom expiry, contracts, headers).
- **Auto-verification on Data read.** Rejected. Reads do not verify signatures automatically; the consumer that cares (`- run %callback%`, `- verify %x%`) explicitly invokes verify. Otherwise every Data read pays a crypto cost when most readers don't need integrity.
- **Default expiry on auto-signed Data.** Rejected. Default = no expiry. Integrity, not validity, is what the default protects. Developers add expiry explicitly when they want it.
- **`Expiry` field on `Callback` record.** Rejected. Lives in `Data.Signature` (the renamed `SignedData`). One source of truth.
- **Encryption at the Data layer.** Rejected. Belongs to the Callback class, invoked during its own serialization. Most Data writes don't need encryption.
- **Built-in `callback.store` / `callback.load` actions for storage ergonomics.** Rejected for Phase 2. Storage is the developer's concern via existing `file.*`, `db.*`, channel actions. If a real ergonomics case appears later, ship as a thin convenience module — not core infra.

## What carries over unchanged from v1

For completeness — the things v2 does NOT change:

- Three-bucket `ISnapshotted` model with the inventory.
- Names-vs-values synthesis principle.
- Throw-time variable capture via Diff reverse-apply.
- Diff auto-flips on error.
- Cache is not snapshotted.
- Resume position lands at the action (re-executes failed action).
- No `Seek` verb — `App.Run(callback)` shares the main loop.
- `App.Providers` two-layer snapshot (registry-layer state).
- Per-actor Variables snapshot.
- Two trust layers: signature integrity + referent integrity.
- Islands rule.
- No `CallbackOrigin`.

If a v1 decision ever conflicts with a v2 statement, v1 governs — this doc is purely additive on the wire/sign/storage/staging axis.
