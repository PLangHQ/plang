# Piece 1: Identity Module — v1 Summary

## What this is

Key pair management module for runtime2. Every PLang app has identities (Ed25519 key pairs). The System actor owns stored identities with auto-creation on first access. User and Service actors receive identities from external sources (HTTP/signing layer).

## What was done

Architectural design for the identity module, collaboratively designed with Ingi. Key decisions:

- **Storage**: System.DataSource (table `"identity"`), not raw SQLite — follows settings module pattern, respects testing context
- **One type**: `IdentityVariable` with `[Sensitive]` on PrivateKey, `ToString()` returns PublicKey
- **Per-actor variables**: `%MyIdentity%` (System, stored, lazy auto-create), `%Identity%` (User, external), `%ServiceIdentity%` (Service, external)
- **`[Sensitive]` attribute**: New infrastructure — included in DataSource storage, excluded from output serialization via `SensitivePropertyFilter`
- **Key generation**: Ed25519 via NSec, internal to identity module (no signing module dependency)
- **Archive auto-promotes** next identity as default

## Files

- `.bot/runtime2-builder-v2-identity/architect/v1/plan.md` — full architecture plan

## Next step

Hand off to **test-designer** to write failing tests on this branch.
