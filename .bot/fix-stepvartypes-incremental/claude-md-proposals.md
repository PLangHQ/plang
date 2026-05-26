## codeanalyzer — v2 — 2026-05-26
**Target:** /workspace/plang/CLAUDE.md (section: `## OBP Shape Smells`)
**Why:** A common shape violation isn't covered by the existing 4 items: a producer hands back a property *raw*, and every consumer applies the same transform to make it usable (`obj.Path + "/"`, `obj.Path.TrimStart('/')`, `obj.Name.ToLowerInvariant()`, `Path.GetDirectoryName(obj.Path)`, …). Each call site looks defensible on its own, but the discipline (separator, case, trimming, parent-derivation) belongs on the owner — exposing the property without it means every consumer either remembers to apply the transform, or quietly diverges. We saw this three times on this branch alone (`step.Goal?.Path?.ToString().TrimStart('/')`, `test.Path.TrimStart('/')`, `LastIndexOfAny(['/','\\\\'])` slicing inside a `report.cs` GroupBy where `File.DirectoryName` would belong). Proposing as a 5th item in the canonical OBP Shape Smells list so all bots (coder, codeanalyzer, tester, security, auditor) scan for it — not just one. **Filed on explicit user request after Ingi flagged the pattern in chat; no incident, no review trigger — this is the reviewer-bot exception path.**

**Proposed change:** append a new item 5 to the numbered list in `## OBP Shape Smells` (`/workspace/plang/CLAUDE.md`), and append a matching bullet to `Documentation/v0.2/good_to_know.md` "OBP Smell Checklist" so the worked-example doc stays in sync.

In `CLAUDE.md` `## OBP Shape Smells`, after the existing item 4:

```markdown
5. **Producer hands back raw; consumers transform identically.** A property is exposed in one shape and most callers immediately apply the same operation to make it usable — `obj.Path + "/"`, `obj.Path.TrimStart('/')`, `obj.Name.ToLowerInvariant()`, `Path.GetDirectoryName(obj.Path)`, `obj.Url.Trim().TrimEnd('/')`. Every fix to that transform now has N call sites; one consumer forgetting it produces a subtle divergence bug. The discipline (separator, case, trimming, parent-derivation, whatever it is) belongs on the owner: rename the existing property or add a sibling that returns the form callers actually use (`Goal.RelativePath` instead of every site calling `.Path.TrimStart('/')`; `File.DirectoryName` instead of every site doing `LastIndexOfAny`). Grep for the literal transform on the property name — `\.{PropertyName}\.(TrimStart|TrimEnd|ToLower|ToUpper|Replace|GetDirectoryName|Substring|Split)` — three or more hits means the property is shaped wrong. Trivial single-char appends (`+ "/"`) count too.
```

Update the trailing sentence above the "Full checklist" pointer to match the new count (if it currently says "four-item checklist" anywhere, change to "five-item checklist"). I checked CLAUDE.md current text and it only says "this checklist" / "any 'yes'" — no explicit count, so no edit needed beyond the new bullet.

In `Documentation/v0.2/good_to_know.md` "OBP Smell Checklist", mirror the same item with a worked example:

```markdown
5. **Producer hands back raw; consumers transform identically.** Same property, same suffix/prefix/case-fold/slice repeated at three or more call sites — the discipline belongs on the owner.

   *Worked example (this branch):* `test/run.cs` had `step.Goal?.Path?.ToString().TrimStart('/')` paired with `test.Path.TrimStart('/')`. The leading slash comes from `.pr` deserialization — fixing it at the producer (`Goal.RelativePath` returning the trimmed form, computed once) would collapse both call sites and prevent the next consumer from forgetting the trim. The grep pattern `\.Path\.TrimStart\(` lights up across `modules/test/run.cs`, `modules/cache/wrap.cs`, etc. when this is wrong.

   *When the property IS the raw form on purpose:* keep both. `Goal.Path` (raw, source of truth) plus `Goal.RelativePath` (trimmed) is fine — consumers pick the one that matches intent and no transform is repeated at call sites.
```

**Followup:** if approved, codeanalyzer's Pass 1b "shape smells" yes/no list grows from 4 to 5 questions automatically (it already cites the CLAUDE.md list as the source of truth). No character file edit required.
