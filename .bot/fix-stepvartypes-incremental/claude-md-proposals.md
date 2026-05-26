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

---

## codeanalyzer — v3 — 2026-05-26
**Target:** /workspace/plang/CLAUDE.md (section: `## OBP Shape Smells`)
**Why:** Ingi flagged on this branch that `app.tester.File` declares a `Goal? Goal { get; init; }` reference **plus** flat scalar fields `Path`, `PrPath`, `EntryGoalName`, `GoalHash`, `BuilderVersion`, `Directory` — every one of those reachable through the `Goal` reference (or derivable from `Goal.Path`). The flat copy costs more memory than the 8-byte reference *and* makes the two views drift-able: if `Goal` is rebuilt or mutated after `File` construction, the flat fields silently stale because nobody updates them. The cost is paid twice — at construction (developer must remember to populate both) and at consumption (callers reading `file.GoalHash` vs `file.Goal.Hash` get different answers in the drift case). I missed this in v1/v2/v3 because Pass 1b runs the 4-item checklist mechanically; "denormalized mirror of reachable properties" isn't one of the 4. Codeanalyzer is supposed to catch this; the rule has to exist for the bot to apply it. Filed on explicit user request — same reviewer-bot exception path as the v2 proposal.

**Proposed change:** add a 6th item to the numbered list in `## OBP Shape Smells` (`/workspace/plang/CLAUDE.md`); mirror in `Documentation/v0.2/good_to_know.md` "OBP Smell Checklist".

In `CLAUDE.md` `## OBP Shape Smells`, after the (yet-to-be-merged) item 5:

```markdown
6. **Holds a reference AND a flat copy of properties reachable through it.** A class declares `Foo Foo { get; }` (or `Foo? Foo`) and *also* scalar fields whose values are all reachable as `Foo.X`, `Foo.Y`, `Foo.Z`. The flat copy costs more memory than the 8-byte reference and creates two views of the same data that can silently drift: when the underlying `Foo` is rebuilt or mutated, the flat fields stale because no one updates them. Construction sites also double-pay — every place that builds the outer class has to remember to populate both the reference and every flat field, and forgetting one is a subtle bug that compiles. Detection: read every scalar property on a class that has a reference field, and ask *"is this `Foo.X`?"* If yes for three or more fields, the flat fields are the smell. Fix by deleting the flat fields and routing consumers through the reference (`file.Goal?.Path` instead of `file.Path`). When the outer class needs to survive `Foo` being null (the .pr is missing, the discovery failed) keep a *single* "summary" field that captures only what's needed for the failure path — never a parallel mirror of everything `Foo` exposes.
```

In `Documentation/v0.2/good_to_know.md` "OBP Smell Checklist", mirror with worked example:

```markdown
6. **Holds a reference AND a flat copy of properties reachable through it.** A class with `Foo Foo` and N scalar fields all reachable through `Foo` is paying double — once in memory, once in drift risk.

   *Worked example (this branch):* `app.tester.File` declared `Goal? Goal` *plus* `Path`, `PrPath`, `EntryGoalName`, `GoalHash`, `BuilderVersion`. Every one reachable through `Goal` when `Goal != null`. The flat copy was paid *for every discovered test file* — and silently staled if anyone rebuilt the Goal in place. Fix: delete the flat fields, route consumers through `file.Goal?.Path` etc. Keep one summary field (e.g. `StatusReason`) for the case where `Goal` is null (.pr missing / corrupt) — that's the legitimate carve-out, because it describes a state the reference can't.

   *When the class IS a value-snapshot on purpose:* a serialization DTO or a thread-safe-snapshot record holding flat copies is fine — the point of the type is to be detached from the live graph. Document the intent in the class XML doc ("snapshot of Foo at time T; not refreshed when Foo changes") so future readers don't merge the two roles.
```

**Detection heuristic for bots (codeanalyzer Pass 1b, security Pass 1, etc.):** for any class with a reference field, list every other public property and check whether each one is `<reference>.<name>` or derivable from `<reference>.<name>`. Three or more hits = smell #6 candidate; one or two = examine intent (sometimes a class legitimately denormalizes one or two for fast access, but that should be a comment-justified carve-out, not the default).

**Followup:** once approved this is detectable without re-grep — it's a structural read of class shape. Same propagation path as smell #5: codeanalyzer's Pass 1b dereferences to CLAUDE.md (separate character proposal already filed) so the list grows transparently.
