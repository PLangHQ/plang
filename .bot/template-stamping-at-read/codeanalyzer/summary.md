# codeanalyzer summary — `template-stamping-at-read`

**Version:** v1

## What this is
First codeanalyzer review on this branch. The branch is large: it carries the
**entire `scalars-as-native` refactor** (527 commits off `runtime2`; 361
production C# files; +10359/-5157) *plus* the recent template-stamping work the
coder summarized in `coder/codeanalyzer-handoff.md` as B1–B5 (datetime navigable
members, `Data.Clr<T>(fallback)` lift, system-var typed reads, `Variables.GetValue`
removal, the bracket-resolution latent-bug fix).

## What was done
A **risk-prioritized** review (exhaustive line-by-line over 361 files in one pass
would be theatre). Reviewed deeply: handoff B1–B5 incl. security-critical Ed25519
signing; the `item.@this` apex; the comparison collapse (`Comparison` enum +
`app.type.compare` mediator); the condition `Operator`; the two `fix(...)` commits.
All four mandated mechanical passes (System.IO, Console.*, OBP Rule 9 courier
`.Value`, provenance comments) run across the whole changed production set — all
clean. `dotnet build PlangConsole` → 0 errors.

### Verdict: PASS (on the reviewed surface)
Findings (full detail + line cites in `v1/report.md`):
- **F1 (LOW):** dead `BothPresent` in `condition/Operator.cs:87` — zero callers,
  stale premise (says ordering ops return false; they now throw). Delete.
- **F2 (LOW):** `data/Comparison.cs:3–20` — the `Comparison` enum's summary is
  orphaned onto `IncomparableException` (double `<summary>` on the exception; enum
  undocumented). Move the block down.
- **F3 (LOW, readability):** `item.@this` has two `Write` methods (set-child vs
  serialize-to-wire) distinguished only by arg type — rename one.
- **F4 (MEDIUM, systemic/pre-existing):** `Normalize` silently reflects an unknown
  raw-CLR leaf to a `{"valuekind":...}` bag instead of failing loud (the foot-gun
  the cache fix `c27c37c5a` worked around). Recommend a loud throw — belongs on
  `obp-cleanup.md`, not a branch blocker.
- **F5 (LOW):** B5 `ResolveVariablesInPath` fix is sound; the sync probe mirrors
  only the top call frame, not `Get`'s full overlay→caller cascade.

Notes (reviewed, no action): N1 Ed25519 clock-fallback asymmetry (justified),
N2 compare mediator mirrors convert pattern, N3 inherited provenance comment in
`compress.cs`, N4 Fluid boundary adapter, N5 item apex OBP-clean.

## What is NOT covered
This is **not** a certification of the full 361-file scalars-as-native body — that
breadth rests on the prior architect → test-designer → coder review rounds and the
green C# suites. A dedicated exhaustive pass over `app/type/**` (natural
multi-agent/workflow candidate given file count) is the follow-up if wanted.

## Example of the finding pattern (F1, deletion test)
```csharp
// condition/Operator.cs:86–88  — zero callers, premise stale → delete
private static bool BothPresent(data.@this? left, data.@this? right)
    => left?.HasValue == true && right?.HasValue == true;
```
