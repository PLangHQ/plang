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

## architect — snapshot 253 — 2026-10-28
**Target:** /CLAUDE.md
**Why:** Ingi (snapshot 244, 253): `app.event` is the app's event system (`%!app.event%` answers its events), and the event in play is `%!app.event.current%`, reached as `%!event%` through `os/system/shortcut/event.goal`, as `%!error%` is. The Runtime2 conventions line lists `event` among the concepts with no `.current`, which is no longer true.
**Proposed change:** in "**`app.X` is the collection node, not a wrapper.**", replace "a concept nothing is ever *inside* (`type`, `channel`, `event`, `module`, `format`) has no `.current`" with:
```markdown
a concept nothing is ever *inside* (`type`, `channel`, `module`, `format`) has no `.current`; `app.event` is the app's events, and its `.current` is the event whose bound call is running (`%!app.event.current%`, `%!event%`)
```

## architect — snapshot 277 — 2026-10-28
**Target:** /CLAUDE.md
**Why:** The codeanalyzer bisected a +274 KB root-request regression on snapshot and found no commit: one binary measured 3566–3829 KB across runs, and the low runs were the slow ones. .NET's tiered PGO instruments and recompiles by timing, so bytes move by hundreds of KB between runs. With `DOTNET_TieredPGO=0`, 7 runs of 7 matched the parent exactly. `Tools/Bench/run.sh` now exports it, so a bot reading old rows needs to know that times since then aren't production times.
**Proposed change:** in "## Speed record", after the first bullet, add:
```markdown
- `run.sh` runs with `DOTNET_TieredPGO=0`: tiered PGO allocates by timing, so one binary's bytes move by ±250 KB between runs. Bytes are stable with it off; times are then not production times, so a row is compared on time only with rows measured the same way (`record.md` marks the change).
```
