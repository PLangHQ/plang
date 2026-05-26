# Docs v1 — fix-stepvartypes-incremental

## Context

Auditor v1: **PASS**. codeanalyzer v3 PASS (after late File.cs slim closing the proposed OBP-6 smell), tester v6 PASS (208/208 PLang + 3036/3036 C#), security v1 PASS. Branch is functionally clean and ready to merge.

The merge gate work for docs is therefore:
1. Decide on 2 CLAUDE.md proposals + 2 character proposals filed by codeanalyzer.
2. Apply approved ones.
3. Verify XML docs / architecture docs / module teaching the coder already wrote.
4. Emit verdict.

## CLAUDE.md proposals

### Proposal 1: codeanalyzer v2 — OBP Smell #5 (producer raw / consumers transform)

**Decision: APPLY.**

Evidence on this branch is strong (three call sites observed: `step.Goal?.Path?.ToString().TrimStart('/')`, `test.Path.TrimStart('/')`, `report.cs` `LastIndexOfAny(['/','\\'])`). The smell is structural and survives across branches — it's a canonical rule, not a branch-local note. The proposal is well-scoped (CLAUDE.md item + good_to_know.md worked example), keeps the existing 4 items intact, and the followup ("codeanalyzer Pass 1b dereferences to CLAUDE.md") is handled by the character proposal below.

### Proposal 2: codeanalyzer v3 — OBP Smell #6 (reference + flat copy)

**Decision: APPLY.**

The branch already demonstrated the cost: `app.tester.File` carried `Goal` *plus* 6 flat scalar fields (`Path`, `PrPath`, `EntryGoalName`, `Status`, `GoalHash`, `BuilderVersion`, `Directory`) — all reachable through `Goal`. Commit `1b1b226bb` collapsed that to `Goal` + 3 discovery-only fields (`Status`, `StatusReason`, `Tags`), and the diff didn't leak — tester suite stayed green. The fix is real, the smell is structural, and the heuristic ("3+ fields reachable through the reference") is mechanical enough that bots can apply it without judgement-creep. Worked example mirrors the actual collapse.

## Character proposals

### Proposal 3: codeanalyzer — Pass 4.5 (Root cause vs symptom)

**Decision: APPLY.**

Comprehensive checklist (14 tells across volume/shape/behavior/witness categories) with a clear "what a root-cause fix looks like" contrast and a structured report shape. This is genuine craft — sharpens an existing technique (codeanalyzer already does Pass 4 behavioral reasoning) rather than expanding scope. The trigger ("commit verbs: fix/handle/guard against/work around") is mechanical. No overlap with Pass 1-5.

### Proposal 4: codeanalyzer — Pass 1b dereference to CLAUDE.md

**Decision: APPLY.**

Critical follow-up: if we apply proposals 1+2 above, CLAUDE.md grows from 4 to 6 items but the character's Pass 1b inline list still says 4. The character would silently miss smells #5 and #6 until someone notices. The proposed rewrite preserves all Pass 1b behavior (yes/no per item, named missing type, call-site collapse list, "clean 1a ≠ clean 1b" sentence) — only the enumeration moves to CLAUDE.md as the single source of truth. Cost is one extra read at Pass 1b start; benefit is no drift.

## Documentation completeness check

- **`PLang/app/tester/Timing.cs`** (new) — has `<summary>` on the record. Good.
- **`PLang/app/tester/Timings.cs`** (new) — class summary + member docs. Good.
- **`PLang/app/tester/File.cs`** — XML doc updated to reflect the slim (identity via `Goal`, discovery-only state here). Good.
- **`PLang/app/tester/Run.cs`** — `Output` (renamed from `CapturedOutput`) and `Timings` have summaries. Good.
- **`Documentation/v0.2/build_process.md`** — fully rewritten for planner/compiler split + new path table. Good.
- **`Documentation/v0.2/building-the-builder.md`** — references updated to `os/system/modules/<m>/<action>.{notes,examples}.md` and `BuildStep/Start.goal` FixValidation path. Good.
- **`Documentation/v0.2/building_plang_tests.md`** — minor patch (3 lines). Good.
- **`os/system/modules/**.{notes,examples,description}.md`** — coder added per-action teaching (condition/if, list/add, variable/set, output/write, channel/set, error/handle, event/on). These are user-facing module docs; coder's responsibility, audited by tester (208/208 PLang green). Good.

No XML doc gaps to fill, no stale architecture references, no missing examples. Coder + tester closed the loop.

## CHANGELOG

No project-wide CHANGELOG file exists in this repo (verified — `ls /workspace/plang/CHANGELOG*` returns nothing). The convention is per-version `result.md` files under `.bot/<branch>/<bot>/v<N>/`. User-visible changes for this branch (token usage + cost UI, per-step timings, builder template restructure, builder.types/builder.actions split, OBP smells #5+#6 added) are captured across the auditor/codeanalyzer/tester result files. Nothing to add.

## Actions

1. Edit `/workspace/plang/CLAUDE.md` — add items 5 and 6 to `## OBP Shape Smells` numbered list.
2. Edit `/workspace/plang/Documentation/v0.2/good_to_know.md` — add items 6 and 7 to the existing 5-item checklist (the existing list there already has item 5 = "Helper that takes a domain object and returns a derived answer"; proposals 1+2 become items 6 and 7 there, not 5 and 6).
3. Edit `/workspace/plang/characters/codeanalyzer/character.md` — replace inline Pass 1b enumeration with dereferenced version; insert Pass 4.5 between Pass 4 and Pass 5.
4. Write `docs-report.json`, `verdict.json`, `summary.md`.
5. Commit, push.

## Verdict (expected)

**PASS** — ready to merge.
