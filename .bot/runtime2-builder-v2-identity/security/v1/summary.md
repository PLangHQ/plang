# Security Audit Summary — Identity Module (v1)

## What this is

Security review of the identity module added in `runtime2-builder-v2-identity`. The module implements Ed25519 key pair management with 8 CRUD actions, a `[Sensitive]` attribute for output filtering, and `%MyIdentity%` variable resolution.

## What was done

Blue team (defensive audit) + red team (offensive assessment) across all identity module code and supporting infrastructure.

### Key decisions

- **Threat model**: PLang is user-sovereign. The user owns their machine and their keys. Memory-resident key material on the user's own process is not the same threat as in a multi-tenant server. Severity ratings are calibrated accordingly.
- **Output serialization**: All channel paths (JsonStreamSerializer) correctly include `SensitivePropertyFilter` — private keys never leak to output. Verified through code tracing and test coverage.
- **SQL injection**: Parameterized queries with hardcoded table names. No injection surface.
- **Data.Envelope.Compress gap**: The only actionable finding. `_envelopeJsonOptions` doesn't include `SensitivePropertyFilter`, so if Compress is ever wired up, private keys could leak into compressed payloads. Currently zero call sites — theoretical only.

### Files reviewed

- `PLang/App/modules/identity/*.cs` (all 11 files)
- `PLang/App/Context/Actor.cs`
- `PLang/App/View.cs`
- `PLang/App/Channels/Serializers/SensitivePropertyFilter.cs`
- `PLang/App/Channels/Serializers/Serializer/JsonStreamSerializer.cs`
- `PLang/App/Memory/Data.Envelope.cs`
- `PLang/App/DataSource/SqliteDataSource.cs`
- All test files in `PLang.Tests/App/Modules/identity/`

### Findings

| # | Severity | Finding | Status |
|---|----------|---------|--------|
| 1 | Low | Key byte arrays not zeroed after base64 encoding | accepted-risk |
| 2 | Low | PrivateKey stored as immutable .NET string | accepted-risk |
| 3 | **Medium** | Data.Envelope.Compress missing SensitivePropertyFilter | open |
| 4 | Low | IdentityData cache holds private key for actor lifetime | accepted-risk |

## Verdict

**PASS** — no critical or high findings. Suggest running the auditor next.
