# Security v1 — runtime2-channels

**Verdict: PASS — 1 Medium, 1 Low, 3 Notes.**

Threat-model walk over the per-actor channel architecture (Stages 1–9).
codeanalyzer v4 / tester v7 already closed the in-process correctness
findings; this pass focuses on the new wire-shaped surfaces (`Channel.Stream`
read path, `Channel.Goal.Migrate` payload, `MigrationEnvelope.Signature`)
and on `[Sensitive]` propagation through the new serializer routes.

No critical or high findings. The Medium is a missing size cap on
`Channel.Stream.ReadAllBytesAsync` — the channel module's core
responsibility is bounded I/O, and that's what's missing. Today's untrusted
input is mostly stdin (limited blast radius), but the Stream channel is
also the abstraction that any future HTTP-body / socket / pipe ingest will
flow through, where the gap turns into the same shape as the runtime2-
builder-v2-http "no response body size limit" finding.

The Low is a Stage-9 stub-quality issue: `MigrationEnvelope.Signature` has
PKI-shaped fields (`IdentityName`, `PublicKey`, `Bytes`) but its `Bytes` are
a keyless `SHA256(name|direction|identity)` — anyone can forge a verifying
envelope. Real wire egress today goes through `PlangDataSerializer` /
`JsonStreamSerializer` (both apply `SensitivePropertyFilter.Strip`), and the
**outer** `Data` envelope returned by `Migrate()` is genuinely Ed25519-signed
on `PlangDataSerializer` egress via lazy `EnsureSigned`. The risk is
therefore latent: a future maintainer who lands `FromMigration` and uses
`Channel.VerifyEnvelope` as the trust gate gets a complete bypass. Names
matter — fix posture: rename the inner field to `IntegrityHash` and drop
the identity fields, **or** rewire the stub through `Data.EnsureSigned` so
the inner shape *is* a real signature.

---

## F1 — `Channel.Stream.ReadAllBytesAsync`: no size cap, ignores `Channel.Buffer`

**Severity: Medium** (responsibility-relative; today bounded by stdin trust)

**File:** `PLang/App/Channels/Channel/Stream/this.cs:135-146`

```csharp
public async Task<byte[]> ReadAllBytesAsync(CancellationToken cancellationToken = default)
{
    if (!CanRead) throw new InvalidOperationException(...);

    if (Stream is MemoryStream ms) return ms.ToArray();

    using var buffer = new MemoryStream();
    await Stream.CopyToAsync(buffer, cancellationToken);
    return buffer.ToArray();
}
```

`Channel.@this.Buffer` is documented (`PLang/App/Channels/Channel/this.cs:31`)
as *"Buffer size in bytes. Stream-backed channels honour"*. The
implementation does not honour it: `Stream.CopyToAsync` reads to EOF, and
the `MemoryStream` grows without bound. `ReadCore` (line 80) and
`AskCore`'s `ReadLineAsync` (line 118) also bypass `Buffer`.

**Why this matters (responsibility framing):** Per
`discipline.md` "Severity by module responsibility": the Stream channel's
core job is bounded transport over a stream. Missing the cap is a gap in
its primary job, not a hardening opportunity. Today's untrusted Stream-
backed channel is `Console.OpenStandardInput` (stdin) — local DoS at worst —
so the realised blast radius is small. But `Channel.Stream` is positioned
as the abstraction the runtime hands an HTTP response body, a socket, or a
named-pipe stream when those ingest paths land. The architect docs
(`stage-2-stream-channel.md`) explicitly call out HTTP / network use cases.
The day a Stream channel wraps an HTTP response body without honouring
`Buffer`, this becomes the same finding as the previously-reported HTTP
response body cap (rated Medium when it shipped, then required to land as
config-driven `MaxResponseSize`).

**Fix posture (small, local):**

```csharp
public async Task<byte[]> ReadAllBytesAsync(CancellationToken ct = default)
{
    if (!CanRead) throw new InvalidOperationException(...);
    if (Stream is MemoryStream ms) return ms.ToArray();

    using var buffer = new MemoryStream();
    var max = Buffer > 0 ? Buffer : long.MaxValue;
    var chunk = new byte[Math.Min(81920, max)];
    long total = 0;
    int read;
    while ((read = await Stream.ReadAsync(chunk, ct)) > 0)
    {
        total += read;
        if (total > max)
            throw new InvalidOperationException(
                $"Channel '{Name}' read exceeded buffer cap of {max} bytes.");
        buffer.Write(chunk, 0, read);
    }
    return buffer.ToArray();
}
```

Or: keep `ReadAllBytesAsync` permissive but make `ReadCore` enforce
`Buffer` as a "sane default" when the underlying stream isn't trusted. The
key contract change is: somewhere on the public read path of a Stream
channel, exceeding `Buffer` is a hard fail, not silent unbounded growth.

`AskCore` already respects `Timeout`; the same shape (cancellation /
exception on cap) should apply to `Buffer`.

---

## F2 — `MigrationEnvelope.Signature` has PKI-shape fields but is a keyless integrity hash

**Severity: Low** (latent — flips Critical the moment `FromMigration` ships
and someone reads `VerifyEnvelope` as the trust gate)

**Files:**
- `PLang/App/Channels/Channel/MigrationEnvelope.cs:35-43`
- `PLang/App/Channels/Channel/this.cs:295-329`

```csharp
// MigrationEnvelope.cs
public sealed class Signature
{
    public required string IdentityName { get; init; }
    public required string PublicKey { get; init; }
    /// <summary>Signature bytes over (Name, Direction, Config, Payload).</summary>
    public required byte[] Bytes { get; init; }
}

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

**Three problems with this stub:**

1. **No key.** `ComputeSignature` is `SHA256(name|direction|identity)`.
   Anyone who reads the envelope can recompute the same bytes — there is no
   asymmetric primitive, no key material involved. `VerifyEnvelope` returns
   true for *any* envelope a third party constructs.

2. **Doc/impl mismatch.** The XML comment on `Signature.Bytes` says
   *"Signature bytes over (Name, Direction, Config, Payload)"*. The actual
   hash covers only `(Name, Direction, IdentityName)`. Even *as* an
   integrity hash the `Config` and `Payload` fields are not covered — they
   can be flipped silently. (Architect's Stage-9 doc explicitly intends the
   envelope to be signed via the existing PLang Ed25519 chain — the stub
   diverged.)

3. **Misleading API surface.** The struct is named `Signature` and
   carries `IdentityName` and `PublicKey`, which signal a PKI signature.
   A future maintainer reading only the type signature could plausibly
   gate trust on `VerifyEnvelope` — that would be a complete bypass on
   the day `FromMigration` ships.

**Today's mitigation (why this stays Low):** `Migrate()` returns
`Data.@this.Ok(envelope)`. When that Data is shipped via
`PlangDataSerializer.SerializeAsync` the **outer** Data envelope's
`Signature` is lazily populated by `Data.@this.EnsureSigned` (`PLang/App/
Data/this.Envelope.cs:78`), which routes through the audited Ed25519
pipeline. The wire boundary is therefore protected as long as the
serializer route is `application/plang+data`. `JsonStreamSerializer` and
file-write paths do **not** call `EnsureSigned`, so any user code that
saves the envelope as plain JSON ships an unsigned blob whose only
"signature" is the keyless inner hash.

`FromMigration` throws `NotImplementedException`, so there is no current
ingest path that consumes this envelope.

**Fix posture (any of):**

- **Drop the misleading struct.** Rename `MigrationEnvelope.Signature` to
  `IntegrityHash` (just bytes, no identity fields), and remove
  `VerifyEnvelope`. The outer `Data.Signature` (real Ed25519 via the
  signing pipeline) is the trust gate.
- **Or wire the inner shape through real signing.** Replace
  `ComputeSignature` / `VerifyEnvelope` with calls into the existing
  `Ed25519Provider` — sign over a stable byte representation that includes
  Name, Direction, Config, Payload, **and** a fresh nonce + timestamp (to
  match the 9-step verify pipeline's replay/expiry checks).
- **Or fail-closed today.** `Migrate()` returns
  `MigrationNotImplemented` until the receive side ships, so no envelope
  with a misleading signature ever enters circulation.

Even option 1 (remove the struct) closes the latent risk. The architect
plan said *"signed (by the actor's System identity, current PLang signing
chain)"* — the outer `Data` already provides that; the inner struct is
redundant at best, foot-gun at worst.

---

## N1 — `Channel.Goal.Migrate` carries `Variables.Snapshot()`; standing leak applies

**Severity: Note** (this is the standing finding from MEMORY.md applied
at a new use site)

**File:** `PLang/App/Channels/Channel/Goal/this.cs:96-112`

```csharp
public override Task<Data.@this> Migrate()
{
    var payload = new GoalMigrationPayload
    {
        GoalName = Goal.Name ?? "",
        Variables = Actor.Context.Variables.Snapshot()
    };
    ...
}
```

`Variables.Snapshot()` (`PLang/App/Variables/this.cs:689-700`) skips
`!`-prefix infrastructure vars and `DynamicData` (so `MyIdentity` is not
included) but applies no `[Sensitive]` filter to value contents. Two
sub-cases:

- **Structured `[Sensitive]` properties** (e.g. an `Identity` placed into
  a Variables slot, with `[Sensitive] PrivateKey`): `JsonStreamSerializer`
  and `PlangDataSerializer` both apply
  `SensitivePropertyFilter.Strip` recursively via the
  `JsonTypeInfoResolver`. So the wire egress is protected for these.
- **Plaintext-string user variables** (`set %ApiKey% = "sk-…"`): not
  taggable — no attribute on a raw `string` value. `Snapshot()` captures
  them, the wire ships them unredacted, but this is a known limitation
  of the `[Sensitive]` mechanism (attribute is property-level), not a
  regression introduced by this branch.

This is **not a new finding** — `Variables.Snapshot()` doesn't honour
`[Sensitive]` is already tracked as a standing Medium in `MEMORY.md`
("not acceptable for sensitive to land in results.json"). Recording this
as a use-site under that finding so the standing tracker reflects it.

When `FromMigration` ships, the receive side will need to rate-limit
envelope sizes and cap deserialised state — an attacker-controlled
envelope can carry arbitrary `Dictionary<string, object?>` payloads.

---

## N2 — `MigrationEnvelope.Payload` is `object?`; future polymorphic deserialize

**Severity: Note** (FromMigration is `NotImplemented` today)

`MigrationEnvelope.Payload` is typed `object?` (`MigrationEnvelope.cs:17`).
At send time the runtime knows the concrete payload type
(`GoalMigrationPayload` or `byte[]` for memory streams). At receive time,
when `FromMigration` lands, the deserialiser will need to round-trip the
payload polymorphically.

System.Text.Json polymorphic deserialise of `object` is well-known unsafe:
gadget chains, type-confusion, and "deserialize whatever the wire says" are
the standard footguns. The receive-side implementation must:

1. Use a discriminator + a closed `[JsonDerivedType]` set, or
2. Treat `Payload` as an opaque `byte[]` and decode per `Channel`-kind
   after the envelope's outer signature is verified.

Flagging now so this doesn't surprise the cross-device transport author
when they pick up Stage-9. (Same theme as the F1 above: the trust gate
must precede the deserialise; deserialise-then-verify is the canonical
inversion that ships RCEs.)

---

## N3 — `Channel.Stream.ResolveEncoding` silently falls back to UTF-8 on unknown encoding name

**Severity: Note** (operational misconfig, not a security boundary)

`PLang/App/Channels/Channel/Stream/this.cs:174-180`. A typoed encoding
name (`"utf32"` instead of `"utf-32"`) silently produces UTF-8 output.
If two peers expect different encodings, this hides the error and may
produce mojibake at the wire. Not a security issue (no privilege break,
no data leak) but the UX is "fail silently". A logged warning on fall-back
would prevent the trap.

---

## Auditor / codeanalyzer findings re-verified

| Finding | Source | Status |
|---|---|---|
| B1 (Channel.Events `_active` static→instance) | codeanalyzer v3 | **closed**, re-verified at `Events/this.cs:22`. Coder v8 added `EventsActiveSet_IsInstanceScoped_NotShared`. |
| L1 (Enter copy-on-write + parent-restoring Releaser) | codeanalyzer v3 | **closed**, re-verified at `Events/this.cs:69-85`. Coder v8 added `Enter_FromConcurrentChild_DoesNotLeakChildIdToParentFlow`. |
| Tester v7 missing-coverage F1/F2 | tester v7 | **closed** by coder v8 probe tests. |

## Test verification (clean rebuild per CLAUDE.md protocol)

- **C# TUnit:** 2762 / 2762 pass.
- **PLang:** `cd Tests && ../PlangConsole/bin/Debug/net10.0/plang --test`
  → 205 pass, 6 fixture-fails (`_fixtures_fail/`, `_fixtures_sensitive/`)
  — matches tester v7 / coder v8 baseline.

## Recommendation

Branch is ready to merge. F1 (Stream `Buffer` cap) is worth landing before
the first real network-backed Stream channel ships; today's stdin-only use
keeps it Medium-bounded. F2 (MigrationEnvelope) can be deferred to the
cross-device transport branch *if and only if* a prominent comment at
`Channel/this.cs:295` flags that the inner `Signature` is **not** a
trust gate and `VerifyEnvelope` must not be relied upon. Cleanest is to
delete `VerifyEnvelope` and rename the inner field to `IntegrityHash` now,
while no caller exists.
