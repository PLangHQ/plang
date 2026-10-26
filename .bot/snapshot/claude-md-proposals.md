## architect — snapshot — 2026-10-26
**Target:** /CLAUDE.md (OBP Shape Smells section) and a new `Documentation/v0.2/obp-patterns.md`
**Why:** Ingi (snapshot 87): smells name what's wrong after it's written; patterns let a bot spot the OBP shape before writing it ("we need patterns, does this pattern match this obp pattern"). The catalog is drafted at `.bot/snapshot/architect/obp-patterns.md`, ten patterns, each from a case settled on this branch. Ingi: "yes live in repo".
**Proposed change:**
Place `.bot/snapshot/architect/obp-patterns.md` as `Documentation/v0.2/obp-patterns.md`. Then add, under the OBP Shape Smells heading in /CLAUDE.md:
```markdown
**Patterns before smells.** Before writing or proposing a shape, name the OBP pattern it follows (`Documentation/v0.2/obp-patterns.md`: saved data maps by a view and its tags; a type per kind, decided at birth; a choice belongs on the element; whoever numbers it finds it; a birth fact, not a search; a compound name is a dot path; one door; the value answers; a reader never answers another type; one concept, one birth method). Every proposal and review opens with it; a review's findings are numbered by pattern.
```

## architect — snapshot — 2026-10-26
**Target:** /CLAUDE.md (Runtime2 Conventions, "Goal properties")
**Why:** Ingi (snapshot 86): `PrPath` is a glued name. The goal's `.pr` is a file, so its path is `goal.pr.path`, and in a snapshot it writes `"pr": {"path": …}`.
**Proposed change:** replace "**Goal properties**: use `Path` and `PrPath` (relative), not `FilePath`/`PrFilePath`/`RelativePath`" with:
```markdown
- **Goal properties**: `Path` (its .goal file) and `Pr` (its .pr file, whose path is `goal.pr.path`), relative; never a glued `PrPath`/`FilePath`/`PrFilePath`/`RelativePath`.
```
