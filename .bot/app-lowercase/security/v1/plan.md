# security — app-lowercase v1 plan

## What this branch is

664 files changed vs `origin/runtime2`. Two distinct workstreams in one
branch:

1. **Mechanical namespace rename** `App.X` → `app.X` (Phases 1–5).
   Compile-enforced, hard to get wrong silently.
2. **Seven OBP folder merges** that collapse `app.Foo` + `app.modules.foo`
   case-pair folders into one (Cache, Builder, Callback, Settings, Code,
   Debug, Modules). Real shape changes, but structural — not behavioral.

Both prior reviewers (codeanalyzer v1, tester v1) returned PASS. C# 2752/2752,
PLang 203/203 (+3 merged from runtime2), 6 known-fail intentional fixtures.

## My security angle

A pure-rename branch is *normally* low risk for me — the compiler catches
breakage. The places where it could still hide a security issue:

1. **Stringly-typed `"App.X"` references the compiler can't catch** —
   generator discovery, reflection, attribute-name lookup, JSON
   discriminators. If one missed `"App."` literal stays, the generator
   could silently skip a type *or* a renamed type could deserialize into
   the wrong CLR class.

2. **Crypto signing pipeline (`Signature`, `verify`, `sign`, `Ed25519`,
   `ISigning`)** — structural shape must be unchanged. Setter access on
   trust-bearing fields (`Hash`, `Signature` on Data) must still be
   private/internal.

3. **Data envelope hardening** — zip-bomb guard (100MB cap), rehydration
   depth guard (128). These literals must survive the rename.

4. **Settings merge** (`app.Settings` + `app.modules.settings` →
   `app.modules.settings`) — the secrets/key store. Did the merge
   accidentally re-export anything internal as public, or collapse a
   permission boundary?

5. **Modules merge** (`app.Modules` registry + `app.modules.*` action
   tree → `app.modules.*`) — the action dispatcher. Type-resolution
   strings here are the gate that decides which CLR handler runs for a
   given `.pr.json` `module.action` pair. A wrong string here = wrong
   handler resolved = trust-boundary problem.

6. **Callback merge** (signed wire envelope lives here) — same shape
   concerns as the signing module.

## What I checked

| # | Check | Method | Result |
|---|-------|--------|--------|
| 1 | Stale `"App."` string literals in production C# | `grep -rn '"App\.' PLang PLang.Generators PlangConsole` | None |
| 2 | Generator discovery strings (`Discovery/this.cs`, `Emission/Action/this.cs`) | grep `"App\.` / `"app\.` | All lowercased; namespace strings match the new tree (`"app.modules"`, `"app.data"`, `"app.variables"`) |
| 3 | `Signature.Hash` setter access | read `signing/Signature.cs` | `{ get; internal set; }` preserved; ctor sets the default via `app.data.@this.Ok("")` |
| 4 | `Data.Signature` lazy-populate path | read `app/data/this.Envelope.cs` | Unchanged: setter writes backing field, getter triggers `EnsureSigned()` only when wrapped value is `ICallback`. Fail-closed semantics for non-callback Data preserved |
| 5 | Zip-bomb cap | `MaxDecompressedSize = 100 * 1024 * 1024` literal in `this.Envelope.cs:21` | Intact |
| 6 | Rehydration depth cap | `MaxRehydrationDepth = 128` literal in `this.Envelope.cs:246` | Intact |
| 7 | Signing module diff vs runtime2 | `git diff origin/runtime2..HEAD -- 'PLang/app/modules/signing/'` | Pure rename: `App.X` → `app.X`, `Data.@this` → `data.@this`, `App.Data.@this.Ok` → `app.data.@this.Ok`. No logic changes |
| 8 | Settings merge — visibility audit | `git show 2943d0690` (Settings merge commit) | Pure folder relocation. `IStore`/`Sqlite`/`this.cs` move from `app.Settings` namespace to `app.modules.settings`. No visibility upgrades. Get-ambiguity disambiguated via `global::app.modules.identity.Get` |
| 9 | Modules merge — registry vs action-folder collision | `git show c258bbb9c` | Registry sits at `app.modules.@this`, per-module at `app.modules.<name>.@this` — distinct namespaces, no collision. 4 internal `app.Modules.@this` refs globally-qualified |
| 10 | Callback merge — signing-relevant shape | `git show aa8a98a5c` | Pure relocate of registry, callback types, `Signature/`, `Wire/` into `app.modules.callback`. No setter access changes |
| 11 | Tester verified merge of runtime2 (5 commits) didn't drop tests | `.bot/app-lowercase/tester/summary.md` | 203→206 PLang pass, matching the 3 new `error.handle` recovery tests merged in |

## What I'm not re-running

The prior tester pass already verified:
- C# 2752/2752 and PLang 206/6-known-fail are real (clean rebuild).
- Builder false-green check on the three merged `error.handle` tests.
- Stderr deserializer warning's namespace string flipped lowercase — non-regression.

Re-running the test suites does not change my threat model — the security
question is whether the rename introduced new attack surface, not whether
the tests pass. They do.

## Finding

No security findings.

Three doc/cosmetic items codeanalyzer already flagged (`app/data/Code/`
case carve-out, ~8 stale `App.X` docstrings, `app/filesystem/Default/`
CLAUDE.md note). None are security issues — they are non-binding docs
drift.

## Verdict

PASS. Branch can proceed to auditor.
