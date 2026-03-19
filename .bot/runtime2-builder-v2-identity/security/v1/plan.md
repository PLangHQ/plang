# Security Audit Plan — Identity Module (v1)

## Scope

All code in `PLang/Runtime2/modules/identity/`, plus supporting infrastructure:
- `Engine/Context/Actor.cs` — identity lifecycle, `%MyIdentity%` resolver
- `Engine/View.cs` — `[Sensitive]` attribute
- `Serializers/SensitivePropertyFilter.cs` — output filtering
- `Serializers/Serializer/JsonStreamSerializer.cs` — filter integration

## Phase 1: Blue Team (Defensive Audit)

Map attack surface across these areas:

1. **Private key generation** — entropy source, key material handling, memory lifetime
2. **Private key storage** — SQLite persistence, file permissions, encryption at rest
3. **Private key leakage** — `[Sensitive]` filter coverage, serialization paths, `ToString()`, error messages, logging
4. **Deserialization** — `IdentityVariable.Deserialize()` from DataSource, type confusion
5. **Identity name validation** — injection via names, case sensitivity edge cases
6. **Default identity resolution** — race conditions in `GetOrCreateDefaultAsync`, sync-over-async
7. **DataSource interaction** — SQL injection (already checked: parameterized), table name safety
8. **Export handler** — private key returned in `Data.Value`, downstream exposure

## Phase 2: Red Team (Offensive Testing)

For each finding: attack vector, feasibility, severity, exploit sketch, proposed fix.

## Deliverables

- `v1/result.md` — detailed findings
- `v1/verdict.json` — pass/fail
- `security-report.json` — structured findings
- `v1/summary.md` — session summary
