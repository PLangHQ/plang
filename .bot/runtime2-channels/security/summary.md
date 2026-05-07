# security — runtime2-channels

Latest: **v1 — pass with 1 medium + 1 low + 3 notes.**

| Version | Verdict | Highlights |
|---|---|---|
| v1 | pass | F1 (medium): `Channel.Stream.ReadAllBytesAsync` ignores `Channel.Buffer`; unbounded read on any non-MemoryStream source. F2 (low, latent): `MigrationEnvelope.Signature` is a keyless `SHA256(name|direction|identity)` with PKI-shaped fields — `VerifyEnvelope` returns true for any forged envelope; turns Critical when `FromMigration` ships if used as a trust gate. N1: `Variables.Snapshot()` leak (standing finding) re-applies at `Channel.Goal.Migrate`. N2: `MigrationEnvelope.Payload` is `object?` — polymorphic-deserialize hazard for receive side. N3: `Channel.Stream.ResolveEncoding` silent UTF-8 fallback. codeanalyzer v3 / tester v7 closures re-verified. |

See `v1/summary.md` for the full walkthrough, `v1/plan.md` for the audit
checklist and verification, and `v1/verdict.json` for the structured verdict.
Top-level machine-readable report at `../security-report.json`.

The Medium is the channel module's core responsibility (bounded I/O over a
stream) — today's stdin-only use limits the realised blast radius, but the
gap is the same shape that's already shown up in the HTTP module audit
(`runtime2-builder-v2-http`: response body needed `MaxResponseSize`). Land
before any non-stdin Stream channel use lands.

The Low is a Stage-9 stub-quality concern. The architect plan said
"signed by the actor's System identity, current PLang signing chain" — and
the **outer** `Data` envelope returned by `Migrate()` is genuinely
Ed25519-signed when shipped via `PlangDataSerializer` (lazy
`EnsureSigned`). The **inner** `MigrationEnvelope.Signature` struct is the
problem: PKI-shape fields + keyless hash + doc/impl mismatch ("over Name,
Direction, Config, Payload" but actually only `name|direction|identity`).
Cleanest fix today is to delete `VerifyEnvelope` and rename the inner
field to `IntegrityHash` before any caller arrives.
