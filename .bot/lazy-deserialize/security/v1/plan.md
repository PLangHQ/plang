# security v1 — lazy-deserialize

**Branch:** `lazy-deserialize` (162 commits off `runtime2` merge-base `d96ec269f`).
**Predecessor verdicts:** codeanalyzer v2 PASS, tester v3 PASS.

## What this branch changes (security-relevant surface)

Lazy Data: `Data { name, type, kind, value }` materializes its value on first
touch from a stored raw source form. The mechanism is concentrated in three
seams:

1. **`Data._raw` + `Materialize()`** (`PLang/app/data/this.cs`). `FromRaw(raw, type, ctx)`
   stores `raw` verbatim; `.Value` getter calls `Materialize()` on first touch,
   which dispatches through `_context.App.Type.Readers.Of(typeName, kind)`. Errors
   are caught and turned into an Error on the Data (never escape as throws).
2. **`Wire.Read` deferral** (`PLang/app/data/Wire.cs:280`). For shape-typed slots
   (`object/<kind>`, `table/<kind>` with a non-empty kind), the value slot is
   captured as raw text (`GetRawText()` for non-string tokens, `GetString()` for
   a string token) and built into a `FromRaw` Data carrying the wire Signature.
   No automatic parse, no auto-verify.
3. **`Wire.Write` verbatim passthrough** (`PLang/app/data/Wire.cs:496` +
   `EmitRawVerbatim`). A `RawUntouched` Data re-emits its raw bytes byte-identical;
   this is the leg that makes the canonical hash recomputed by `crypto.Hash`
   match the original wire bytes for verifying a relayed lazy Data.

Other touched-but-secondary surface: `file.read` / `http.get` become thin
source-providers routed through `channel.read` (the single boundary);
`StampReadAsync` / `StampValue` in `app/channel/this.cs` is the new stamping
seam; `set.cs` preserves laziness across binding when type+kind match;
`AsCanonical` skips materialization for `RawUntouched` so couriers don't trip
the parse.

## Security threat model for this work

Untrusted external Data arrives over a channel as wire bytes. The contract:
the signature gates trust; receivers MUST call `signing.verify` before treating
the value as authentic. The lazy mechanism must not (a) bypass that gate,
(b) cause receiver-side parsing on un-verified bytes that wouldn't have happened
eagerly anyway, (c) allow signature/value de-sync (sign one payload, deliver
a different parse), or (d) break the canonical hash recomputation that verify
depends on.

## Audit plan

1. **Bootstrap** — write session entry in `.bot/<branch>/report.json` with
   `before` state. *(done.)*
2. **Semgrep baseline** — run `scripts/semgrep-scan.sh`; confirm hits are at
   parity with the runtime2 base (no new architectural-invariant regressions).
3. **Read the lazy seam** end-to-end — `Data.FromRaw`/`Materialize`, `Wire.Read`
   deferred-raw branch, `Wire.Write`/`EmitRawVerbatim`, `crypto.Hash`
   `MarkOuterForHash` interaction.
4. **Specific questions to answer:**
   - Does Wire.Read ever materialize the lazy value before signature verify?
   - Does `crypto.Hash` recompute give byte-identical output for a relayed
     RawUntouched Data (so verify passes for legitimate relay)?
   - Can a wire-attached `type.kind` redirect a value's bytes to a different
     parser than the signer intended? (Parser-confusion class.)
   - Does the lazy path widen the parse-time DoS attack surface vs. the prior
     eager path?
   - Is the raw size bounded anywhere upstream, or could a malicious channel
     hand StampReadAsync an unbounded byte[]?
   - Does the `Materialize` catch-all silently absorb security exceptions?
5. **Write findings** — `security-report.json`, `v1/result.md`, `v1/verdict.json`,
   `summary.md`. Cross-bot commenting via `security_comment` field on prior
   sessions in `report.json`.
6. **Commit + push.** Verdict: PASS or FAIL. Next bot: auditor on PASS.

## Carry-forward open items (memory-tracked, not lazy-deserialize-specific)

- `Variables.Snapshot()` test-module `[Sensitive]` leak.
- `OpenAiProvider` image `ReadAllBytes` no size cap.
- `Channel.Stream.ReadAllBytesAsync` ignores `Channel.Buffer` (becomes relevant
  the moment a non-stdin Stream channel ships).
- `callback.run` skip-verify when `RawSignature==null` (latent; flips on first
  wire-channel callback ingest).
- `MigrationEnvelope.Signature` keyless hash with PKI-shape field names
  (latent; flips when receive-side ships).

None of these are new on this branch.
