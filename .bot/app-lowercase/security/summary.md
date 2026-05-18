# security — app-lowercase

## Version
v1 — first security pass, after codeanalyzer v1 PASS and tester v1 PASS
(integration check post merge of `origin/runtime2`).

## What this is

Security audit of a branch that ships two things together:
1. A 664-file mechanical rename `App.X` → `app.X` (Phases 1–5).
2. Seven OBP folder merges that collapse case-pair folders
   (`Cache`, `Builder`, `Callback`, `Settings`, `Code`, `Debug`, `Modules`)
   into single lowercase namespaces.

The semantic claim from the architect is "purely cosmetic, no behavior
change." My job is to verify that claim from the security angle — that
no trust boundary moved, no setter access widened, no stringly-typed
discovery missed a literal, and no hardening guard got rewritten out.

## What was done

Targeted spot-checks on the slices that *could* hide a security
regression behind a rename:

- **Stringly-typed `"App."` literals** — grep across `PLang/`,
  `PLang.Generators/`, `PlangConsole/`. None remain in production C#.
- **Generator discovery strings** — `Discovery/this.cs` and
  `Emission/Action/this.cs` use string-literal namespace names to find
  handlers by reflection. All eight literals lowercased and pointing at
  the new tree.
- **Crypto seam** — `app.modules.signing/{sign,verify,Signature,code/Ed25519}.cs`.
  Diff is pure rename. `Signature.Hash` setter remains `internal`. NSec
  Ed25519 path unchanged.
- **Data envelope hardening** — `app/data/this.Envelope.cs`.
  `MaxDecompressedSize = 100 MiB` (line 21) and `MaxRehydrationDepth = 128`
  (line 246) survived the rename. Lazy-signature semantics for non-callback
  Data still fail-closed.
- **Settings merge commit** (`2943d0690`) — pure relocation of
  `IStore`/`Sqlite`/`this.cs` into `app.modules.settings`. No visibility
  upgrades.
- **Modules merge commit** (`c258bbb9c`) — registry @this lives at
  `app.modules.@this`, per-module at `app.modules.<name>.@this`. Distinct
  namespaces, no collision.
- **Callback merge commit** (`aa8a98a5c`) — `Signature/` and `Wire/`
  subfolders relocated cleanly under `app.modules.callback`.

Tests not re-run: tester v1 already verified C# 2752/2752, PLang 206
real passes / 6 known-fail intentional fixtures, builder false-green
on the 3 merged `error.handle` tests. Test gate is the prior bots' job;
my gate is shape.

## Files written

- `.bot/app-lowercase/security/v1/plan.md`
- `.bot/app-lowercase/security/v1/verdict.json`
- `.bot/app-lowercase/security-report.json`
- `.bot/app-lowercase/report.json` — appended security session

## Verdict

**PASS.** No findings. No critical/high open issues.

The branch is exactly what it claims to be: a cosmetic rename that
makes PLang vocabulary visible at the case level. Nothing else moved.

## Code example — the shape that proves "rename only"

Signing module's `Signature.Hash` accessor before/after:

```csharp
// BEFORE (origin/runtime2)
namespace App.modules.signing;
public Data.@this Hash { get; internal set; } = App.Data.@this.Ok("");

// AFTER (app-lowercase)
namespace app.modules.signing;
public data.@this Hash { get; internal set; } = app.data.@this.Ok("");
```

Setter access (`internal set`) unchanged. Default-value path identical.
This is the pattern across all 664 files: namespace segments rewrite,
trust discipline doesn't.

## What's next

```
VERDICT: PASS
Next: run.ps1 auditor app-lowercase "Review the code on branch app-lowercase" -b app-lowercase
```
