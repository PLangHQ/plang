# security — runtime2-cleanup

**Version:** v1
**Verdict:** PASS

## What this is

First security review of the runtime2-cleanup branch. The branch is the
OBP cleanup line of work — 107 commits, 27 stages of structural refactor
(file moves, namespace shuffles, "wrong owner" relocations, rule-A/B/C
sweeps). Coder, codeanalyzer, and architect have all completed; security
is the last review before auditor.

## What was done

Walked `origin/runtime2..HEAD` for every security-relevant area and asked
the question: "is this a rename, or is this a real semantic change?"

Areas verified rename-only or arithmetic-equivalent:

- `PLang/App/modules/signing/**` — EdDSA Verify() pipeline byte-identical.
  `ExpiresInMs(int) → Expires(TimeSpan)` is arithmetic-equivalent.
- `PLang/App/Channels/Serializers/Plang/Data.cs` + `Filters/Sensitive.cs`
  — namespace relocation only; `_envelopeJsonOptions` static→instance is
  scoping change, still read-only.
- `PLang/App/Data/JsonString.cs` (new) — System.Text.Json with constrained
  enum converter; no polymorphic / type-by-name surface.
- `PLang/App/Settings/Sqlite.cs` — pure rename; parameterised queries
  unchanged.
- `PLang/App/Types/this.cs` — 927-line consolidation of TypeMapping +
  TypeConverter + MimeTypes + PlangTypeIndex. Resolution order preserved,
  generic depth guard retained.
- `PLang/App/Variables/Reserved.cs` — list relocation, no narrowing.
- `PLang/App/CallStack/Call/this.cs` + `Call/Position.cs` — RestoredFrame
  rename + new `ExecuteAsync` wrapper that adds error-stamping and
  cancellation containment without loosening frame validation.

Two carry-over low items from codeanalyzer v3 (`Registry.Assemblies`
public mutable; two residual `Console.Out` writes) are accepted-risk —
not externally reachable, scheduled as cleanup follow-ups.

## Output

- `.bot/runtime2-cleanup/security-report.json` — structured findings
- `.bot/runtime2-cleanup/security/v1/result.md` — narrative + per-area table
- `.bot/runtime2-cleanup/security/v1/plan.md` — scope and method
- `.bot/runtime2-cleanup/security/v1/verdict.json` — `pass`

## Code example — what "rename only" looks like

Before (stage-13 tip):
```csharp
public sealed class SqliteSettingsStore : ISettingsStore
```
After (stage-27 tip):
```csharp
public sealed class @this : IStore // namespace App.Settings.Sqlite
```
Class body, query bodies, parameter binding — line-for-line identical.
This is the shape of every "real" change in this branch.

## Next bot

`auditor` — final cross-bot sign-off.
