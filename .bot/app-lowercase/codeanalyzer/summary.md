# codeanalyzer — app-lowercase

## Version
v1 (initial review pass after coder finished all 18 commits).

## What this is
Code-simplicity / OBP audit of the `app-lowercase` branch before merge to `runtime2`.
The branch ships two distinct workstreams in one go:

1. A mechanical `App` → `app` namespace rename across ~618 .cs files (Phases 1–4 + Builder).
2. Seven OBP merges that collapse case-pair folders (`app/Cache/` + `app/modules/cache/`, etc.) into single namespaces.

The mechanical rename is compiler-enforced and hard to get wrong silently; the OBP merges are real shape changes and were the main focus of this review.

## What was done

Five passes per the character file:

- **Pass 1 (OBP)** — verified each of the 7 merged folders. All have single-owner shape, private mutation discipline, and resolve a prior smell-#3 ("same logical thing stored twice"). No new shape smells introduced. Public collections in the tree are DTO init slots or read-only projections; every `lock (...)` locks a private field of the locking class.
- **Pass 2–3 (simplification + readability)** — three minor findings (see below). The renames `app.run` → `environment.run` and `builder.app` → `builder.load` read acceptably; coder flagged them as placeholders but they're not bad enough to block.
- **Pass 4 (behavioral)** — generator string literals all lowercased correctly; no `"App."` literals in production code; no case-collision folders remain; case-insensitive FS clone-safe.
- **Pass 5 (deletion test)** — no empty files, no tombstones, no leftover top-level PascalCase folders.

Build clean: 0 errors, 447 (pre-existing nullable) warnings.
C# tests: 2752 pass / 0 fail.
PLang tests: 203 pass / 6 fail (all intentional negative fixtures, matches runtime2 baseline).

## Findings (all low severity, none blocking)

1. **`app/data/Code/` not lowercased** (Pass-4 miss). Every other code-provider folder is lowercase (`app/modules/<m>/code/`); this one is `Code`. One folder rename + 5 consumer-site updates.
2. **~8 stale `App.X` docstring/comment references** in production code. No runtime impact, just doc drift. One-line edits.
3. **`app/filesystem/Default/` carve-out** is honest (C# keyword `default`) but undocumented in CLAUDE.md. Extend the existing claude-md-proposal.

## Code example — the OBP merge pattern, verified

Verified that each of the seven merged folders has the shape coder described.
Example for `cache`:

```csharp
// PLang/app/this.cs:141 — single source of cache truth on the app
public ICache Cache { get; set; } = new global::app.modules.cache.Memory();

// PLang/app/modules/cache/  — single home
//   ICache.cs    interface (seam for redis.dll override)
//   Memory.cs    default impl
//   wrap.cs      [Action("wrap")] handler
```

No `@this` registry needed because there's no choice between cache impls per
app — one `ICache` slot is enough. Cleaner than the pre-merge state.

## Verdict
**PASS.** Three small follow-ups, none blocking the merge.

## What's next
```
VERDICT: PASS
Next: run.ps1 tester app-lowercase "Review the code on branch app-lowercase" -b app-lowercase
```
