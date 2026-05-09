# Security review — runtime2-cleanup v1

**Verdict: PASS** (no critical/high open findings)

**Scope:** `origin/runtime2..HEAD` — 107 commits, ~218 production C# files.
This is the OBP cleanup branch (stages 1–27): renames, namespace moves,
"wrong owner" relocations.

## Method

Walked the diff against origin/runtime2 for each security-relevant area.
Looked for real semantic changes vs. pure renames. Quoted before/after
lines wherever a real change was suspected.

## Areas audited & outcome

| Area | Change kind | Risk |
|---|---|---|
| `signing/{sign,verify}.cs` + `code/Ed25519` | Rename + `ExpiresInMs:int → Expires:TimeSpan`. EdDSA pipeline untouched. Expiry still enforced in Verify. | None |
| `Channels/Serializers/Plang/Data.cs` + `Filters/Sensitive.cs` | Namespace relocation. `_envelopeJsonOptions` static→instance (read-only after init). | None |
| `Data/JsonString.cs` (new) | System.Text.Json parsing helper. No type-by-name reflection, no polymorphic surface. `EmptyStringToNullEnumConverter<T>` constrained to enum types. | None |
| `Settings/Sqlite.cs` | Pure rename of `SqliteSettingsStore`. Parameterised queries unchanged. | None |
| `Types/this.cs` (927-line consolidation of TypeMapping + TypeConverter + MimeTypes + PlangTypeIndex, stages 18/26/27) | Resolution order preserved (primitives → domain registry → MIME → null). Generic depth guard `MaxGenericDepth = 20` retained. No reflection-by-name from untrusted input added. | None |
| `Variables/Reserved.cs` (replaces `Utils/ReservedKeywords.cs`) | List relocation. Centralised via `IsReserved()`. | None |
| `CallStack/Call/this.cs`, `Call/Position.cs` | `RestoredFrame → Call/Position` rename (stage 23). New `ExecuteAsync` adds error stamping + cancellation containment. Frame validation unchanged. | None |

## Open low-severity items (carried over from codeanalyzer v3)

1. **`Registry.Assemblies` public mutable collection** — latent OBP smell;
   not externally reachable. Status: accepted-risk, scheduled cleanup.
2. **Two residual `Console.Out` writes** in `test/report.cs` and
   `Channels.WireDefaultConsoleChannels` — bypass channel filters. Test
   harness only. Status: accepted-risk, simple follow-up.

Neither is a security blocker.

## Why this is PASS, not "PASS with conditions"

The refactor is structurally invasive (file moves, namespace shuffles,
927-line `Types/this.cs`) but every security-load-bearing piece — the
EdDSA verify pipeline, sensitive-data filtering, parameterised settings
queries, reserved-name guards, restored-frame trust path — is line-for-line
equivalent post-refactor. The one arithmetic change (Expires) is
equivalent: `now.AddMilliseconds(int)` → `now.Add(TimeSpan)`. No new
attack surface, no loosened invariant.

## Next bot

`auditor` — final pre-merge sign-off across architect / coder / codeanalyzer /
security on this branch.
