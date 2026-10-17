# docs — kind-is-type

**Version:** v1 · **Branch:** kind-is-type (parent runtime2) · **Reviewed tip:** e96c7d4cb

## What this is

The docs pass for kind-is-type — the last bot in the review line (codeanalyzer v3, tester v2, security v2, auditor PASS all landed). The job: write up the kind-is-type model (stages 0–6) and the three locked-core additions the auditor named, and clean what the branch left stale in the docs.

## What was done

All edits are **`Documentation/v0.2/` only** — no `os/system` teaching markdown, so the teaching-edit rule does not trigger and no decider/page re-pins are needed. **No production C# edited.**

1. **`type-system.md` — new section "A kind is a type (kind-is-type, 2026-10)".** The authoritative model, each claim anchored to source I verified on this branch:
   - A kind IS a type under its parent: `kind.@this : app.type.@this`; `kind<T> : kind : type` names the parent and is the one seam where an unanswered type question passes up to `T`.
   - Names on two levels (a kind found only through its parent; no app-wide kind-by-name search; MIME/extension stay app-wide).
   - Written form `{name, kind}` nests; the `name<kind>` face (F4.2).
   - `[Kinds]` stays on value-subclass parents (input/screen/browser/query/text-for-separator); only its codec-family parent-finder role went.
   - Named values aren't kinds; separator is text's kind.
   - A format owns its bytes until first use; json parses once into a plang `dict`/`list` via `data.Open()` (no `JsonElement`, no navigator).
   - Settings as their own top-level types, read back by their type (789/796); no reader for an old stored shape (790; loud SettingsUnreadable 799).
   - The three locked-core adds: `OwnInternal` (831), `file.read`'s `Type` slot (832), the `name<kind>` face (834).
2. **`type-system.md` — callouts** on the three sections kind-is-type supersedes (`Typed values`, `Strict kind`, `Reader registry`), each pointing to the new section and naming what changed. The Strict callout records the **F-SC-1 carry** (kind/strict ×4 still red at merge; same on runtime2, not a regression).
3. **`type-reference-generation.md`** — retired the orphaned `Golden output — separator` block (decision 830): separator is now a kind of text, `TheSeparatorPage` golden retired.
4. **`good_to_know.md`** — index pointer for the new section.

## Findings (see docs-report.json)

- **orphaned generated page** (builder/coder): `os/system/type/separator/` still holds a committed generated page though separator is now text's kind; `Types.goal` only emits the text page today, so it won't auto-clean. Not test-enforced. Not docs' lane to delete.
- **no CHANGELOG convention** (Ingi/host, OPEN): the repo has none; `decisions.md` is a guarded-check file, not a log; the changelog-equivalent is the architect summary. Did not fabricate one — OPEN for a decision on whether/where.
- **XML docs current** (info): the coder's XML/comments on the changed surfaces are accurate and kind-is-type-aware; nothing stale to fix.
- **F-SC-1 carried** (branch A / architect): documented as carried in the Strict callout.

## Merge

**DO NOT MERGE.** Committed and pushed to kind-is-type; the merge into runtime2 waits for Ingi's go, relayed by the host.
