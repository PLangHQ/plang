# Auditor v1 — result

**Branch:** `compile-llm-notes-per-action`
**HEAD:** `c1657d396`
**Pipeline before me:** architect v1 → test-designer v1 → coder v1..v3 →
tester v1 (NEEDS-FIXES) → tester v2 (PASS) → security v1 (PASS).
**Build:** clean rebuild from zero this pass — `dotnet build PlangConsole`
0 errors, `dotnet run --project PLang.Tests` 2945/2945 pass.

**Verdict: PASS.** Both architect verification checks reproduce
independently. Pipeline holds. **One new** low/informational finding
(F1) — non-blocking, surfaced because it falls in the seam between
bots. A second finding (F2 — CLAUDE.md `[Provider]` references) was
**retracted on rebase**: docs commit `65538f9dc` landed concurrently
and closed it.

---

## Stage-by-stage delivery (architect plan §"Order of work")

| Stage | What was specified | Status |
|---|---|---|
| 0 | Rename legacy PascalCase `*Module/` folders under `os/system/modules/` to lowercase | **Done.** `ls os/system/modules/` is fully lowercase; no `*Module` folders remain. The folder `module/` is the `module` action-module (with `add`, `remove` actions), not a collision with the `module.` reserved stem. |
| 1 | Loader: `Describe()` reads markdown per action; attach text fields to catalog | **Done.** `PLang/app/modules/MarkdownTeaching.cs` (140 LOC) implements `Load`, `MergeLayers`, `ScanOrphans`. Called from `PLang/app/modules/this.cs:393` inside `Describe()`. Catalog entry exposes `Notes`/`ModuleNotes`/`Description`/`ModuleDescription`/`ExamplesMd`/`ModuleExamplesMd` (lines 139-184 of `app/goals/.../action/this.cs`). Two layers kept separate, merged at render time per spec. |
| 2 | Renderer: per-action blocks in `stepActionDetails.template` | **Done.** Template gates each block on `planStep.actions` (planner's set); renders `DescriptionRendered`, `NotesRendered`, then `Examples` + `ExamplesMdRendered`. Modifiers render through the same path (no special-case). |
| 3 | Migration script: extract `[Example]`/`[Description]` → markdown; delete attributes | **Done.** No class-level `[Description]` remains on action handlers (one property-level on `goal/return.cs:14` is unused — the catalog only reads class-level — not a bug, latent dead code). All `[Example(...)]` removed. Attribute *definitions* kept (`Attributes.cs:97`) per architect's "defined-but-unused for other call sites" option. |
| 4 | Author Notes files (incl. new `assert/module.notes.md`) | **Done.** 184 teaching files under `os/system/modules/**/*.{notes,examples,description}.md`. `assert/module.notes.md` present and contains the `Message`-omission rule. |
| 5 | Delete corresponding sections from `Compile.llm` | **Done.** `wc -c os/system/builder/llm/Compile.llm` = 14905 bytes (~14.5 KB), down from the ~20.8 KB baseline. |
| 6 | Run the two verification checks | **Done.** See "Verification reproduction" below. |
| 7 | Orphan-file validation at catalog load | **Half-shipped — see F1.** Mechanism exists (`MarkdownTeaching.ScanOrphans`, `Modules.WarnOrphansAsync`); never invoked from production. |
| 8 | Rename `[Provider]` → `[Code]` (attribute + generator + PLNG001 + CLAUDE.md) | **Mostly done — see F2.** Source-side complete: `ProviderAttribute` gone, `CodeAttribute` present, generator folder `Emission/Property/Provider/` renamed to `Code/`, regression test pins. CLAUDE.md not updated. |

## Verification reproduction (independent, fresh build)

**Check 1 — system prompt size.** `Compile.llm` static size 14905
bytes < 16 KB bound. The architect target was "~15 KB"; actual is
~14.5 KB. Pinned by `StepActionDetailsRenderTests.cs:154-155`.

**Check 2 — drift cases across 3 fresh-cache rounds.** Ran:

```
for i in 1 2 3; do
  rm -rf Tests/Simple/.build
  plang build '--build={"files":["Tests/Simple/Start.goal"],"cache":false}'
  cd Tests && plang --test Builder/CompileLlmNotes
done
```

| Round | Build time | output-write-no-channel | assert-equals-no-message |
|-------|------------|--------------------------|---------------------------|
| 1     | 9.6s       | Pass (65ms)              | Pass (6ms)                |
| 2     | 10.4s      | Pass (11ms)              | Pass (4ms)                |
| 3     | 9.7s       | Pass (42ms)              | Pass (11ms)               |

Both drift cases pass on every fresh-cache round. No `Stale`.

**C# suite.** 2945/2945 pass on clean rebuild — matches tester v2.

**Broad plang suite.** 210 pass + 6 `_fixtures_fail` / `_fixtures_sensitive`
intentional-fail fixtures (test-runner's own negative-path tests) + 0
stale. The 6 are the test module's designed-to-fail negative fixtures;
they fail on `main` too. Not branch-introduced.

---

## New findings

### F1 (low, informational) — orphan-scan never fires in production

**Where.** `PLang/app/modules/MarkdownTeaching.cs:68` (`ScanOrphans`) and
`PLang/app/modules/this.cs:252` (`WarnOrphansAsync`).

**Surface.** Architect Stage 7 specified: "At catalog load, fail loud on
**orphan markdown files** … One clear warning per orphan at startup. Don't
crash — orphans should not block builds — but make them impossible to
miss." Coder shipped the mechanism (`ScanOrphans` enumerates, `WarnOrphansAsync`
writes to the actor's `Output` channel — correctly avoiding `Console.*`
per CLAUDE.md). But no production caller invokes it:

```
$ grep -rn "WarnOrphansAsync\|ScanOrphans" PLang/ PlangConsole/ --include="*.cs"
PLang/app/modules/MarkdownTeaching.cs:14:   /// <see cref="ScanOrphans"/> ...
PLang/app/modules/MarkdownTeaching.cs:68:   public static IEnumerable<Orphan> ScanOrphans(...)
PLang/app/modules/this.cs:252:              public async Task<...> WarnOrphansAsync(...)
PLang/app/modules/this.cs:257:                  var orphans = MarkdownTeaching.ScanOrphans(...)
```

The three other hits are all in `PLang.Tests/App/Modules/CatalogTests/
MarkdownTeachingOrphanTests.cs`. **No production code path triggers
`WarnOrphansAsync`** — not at `PlangConsole` bootstrap, not at builder
start, not lazily inside `Describe()`.

**Observable impact today: zero.** I scanned the 184 teaching files
under `os/system/modules/**`; every stem maps to a registered action.
There are no orphans to warn about *right now*.

**Risk.** Latent. The next time a developer renames `output.write` →
`output.send`, renames a markdown file with a typo (`module.examplesm.md`),
or drops a stale teaching file when removing an action, no warning will
fire. The whole point of Stage 7 — "the replacement for 'the C# compiler
catches typos in attribute argument strings'" — is missed.

**Fix.** ~5 lines. Either call `await Modules.WarnOrphansAsync(actor)`
once during builder startup (after modules are registered, before
`Describe()` is first invoked) or fold the scan into the first
`Describe()` call with a once-per-process guard.

**Missed by:**
- **tester** verified `WarnOrphansAsync` by direct call from
  `MarkdownTeachingOrphanTests.cs` — the function works, but tests
  don't observe whether anyone calls it on a real run.
- **security** audited the mechanism (path traversal, symlink escape,
  unbounded read — all not exploitable) and the orphan-scan *output
  surface* (actor `Output` channel — trusted local). Both verdicts
  correct *for the mechanism*; neither verifies invocation.

**Severity rationale.** Low/informational, not Med. Today's tree has
no orphans, and the next orphan (when it eventually appears) is a
typo in repo-tracked content that a developer can also catch by
diffing the build prompt or reading `os/system/modules/`. But it's a
stage the architect explicitly called out, and "low" rather than
"info" reflects that a future developer is one rename away from
encountering it.

---

### F2 — RETRACTED on rebase

Initially filed as: CLAUDE.md lines 38 + 42 still reference `[Provider]`
/ `Emission/Property/Provider/` after Stage 8 source-side rename.

**Closed concurrently by `65538f9dc` (docs pass), landed while this
audit was running.** On rebase, CLAUDE.md lines 39 + 43 now read
`Emission/Property/{Data,Code}/` and `[Code] T`. The docs pass picked
up the rename from the docs queue without my proposal needing to be
filed.

Mea culpa: the auditor should `git fetch` before reporting. I committed
on top of a stale local branch and only discovered the resolution when
the push was rejected. Filing this as the auditor's own process gap
rather than silently dropping the finding.

---

## Carry-forwards (unchanged at HEAD)

These are noted because the security v1 verdict cited them; auditor
confirms none crept into the branch diff.

- **HttpPath F1 (latent, from path-polymorphism auditor v1)** —
  `PLang/app/types/path/http/this.cs:94` still initialises `Raw` from
  the original `rawPath` (userinfo-preserving). File not in this
  branch's diff. Latent leak unchanged; still informational.
- **F1 / F2 / F4 (Med / Low / Low, from filesystem-permission)** —
  `signing.verify` integrity-only, unsigned persisted-row auto-trust,
  Regex/Glob without `MatchTimeout`. Files not in this branch's diff.
- **O5 / O6 (info, from path-polymorphism security v3)** —
  `[PathScheme]` attribute decorated-but-unconsumed, `_uri.IdnHost`
  IPv6 returns bare hex.

---

## Confidence

High on the verification reproduction (both architect checks held on a
clean rebuild I drove independently) and on the stage-by-stage delivery
(traced each stage to files on disk).

The two new findings are the kind that fall through the seam between
axes — coder marked Stage 7 "done" because the mechanism existed; tester
verified the mechanism worked when called; security audited the
mechanism's safety. None of the three asked "but does anything actually
call it?" Same shape for the CLAUDE.md miss — every per-axis bot's scope
ended at the source tree.

Both findings are non-blocking, but I'm filing now so they're adjudicated
rather than silently inherited into the next branch.
