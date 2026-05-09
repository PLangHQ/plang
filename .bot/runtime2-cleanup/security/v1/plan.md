# Security audit plan — runtime2-cleanup v1

## Scope

`origin/runtime2..HEAD` on branch `runtime2-cleanup`. 107 commits, ~218
production C# files touched, ~2860 insertions / ~2877 deletions in `PLang/`.

The branch is the OBP cleanup branch: stages 1–27, mostly file moves,
namespace shuffles, and "wrong owner" relocations (KeepAlive owns its
collection, Modules owns Describe, Channels stops smuggling Console writes
through itself, etc.). No advertised behavior changes.

## Threat model recap

PLang is user-sovereign. Trust boundary = cryptographic signatures on Data.
The user's own actions (`%!fileSystem%`, `%!engine.fileSystem%`) are not
attacks. Defend against:

- Untrusted external data crossing transport channels
- Deserialization of `.pr` files / restored frames
- Type confusion on resolution from PLang names → CLR types
- Path traversal in goal / variable / reserved-name handling
- `Verified` getting a settable path that bypasses crypto

## Areas to audit

Picked because they touch real security-relevant code, not just renames:

1. **`PLang/App/modules/signing/**`** — sign.cs, verify.cs, Ed25519,
   KeyPair. Stage 14 changed `ExpiresInMs` → `Expires` (TimeSpan, ISO 8601).
2. **`PLang/App/Channels/Serializers/Plang/Data.cs` + Filters/Sensitive.cs**
   — transport boundary. Verified envelope round-trips through these.
3. **`PLang/App/Data/JsonString.cs`** — new file. Deserialization?
4. **`PLang/App/Settings/Sqlite.cs`** — renamed from SqliteSettingsStore.
5. **`PLang/App/Types/this.cs`** — 927-line change consolidating
   TypeMapping / TypeConverter / MimeTypes / PlangTypeIndex (stages 18, 26,
   27). Highest risk for a real semantics change since type resolution is
   on the critical path for action handler dispatch.
6. **`PLang/App/Variables/Reserved.cs`** (new) and the deleted
   `Utils/ReservedKeywords.cs` — guard list against reserved-name spoofing.
7. **`PLang/App/CallStack/Call/this.cs`** — restored frame / call frame.
8. Cross-check **codeanalyzer v3** (final cumulative pass) for any open
   issues bordering security.

## Method

Delegated the diff-walk to an Explore subagent (read-only, file-by-file
diffs against `origin/runtime2`). Agent reports back with explicit
quote-the-line evidence for any real semantic change.

## Decision rule

- Any open critical/high finding tied to crypto, deserialization, or
  type confusion → FAIL, route to coder.
- Renames + namespace moves with semantics-preserving consolidation, and
  the Expires conversion arithmetic-equivalent → PASS.
