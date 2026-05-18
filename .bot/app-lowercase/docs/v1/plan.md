# docs v1 — plan for app-lowercase

## Context

Branch is a 12-commit + 7-OBP-merge rename of root `App` → `app`. Codeanalyzer, tester,
and security all PASS. Coder already swept `Documentation/v0.2/` + `Documentation/Runtime2/data-spec.md`
in commit `a146aa9d9` and filed a CLAUDE.md proposal.

I am the final gate. My job: apply the proposal, scrub remaining docstring drift,
verify nothing else stale, and call the verdict.

## Inputs read

- `.bot/app-lowercase/claude-md-proposals.md` — coder v3 proposal: 3 stale-line fixes + new app-lowercase convention block.
- `.bot/app-lowercase/character-proposals.md` — does not exist.
- `.bot/app-lowercase/codeanalyzer/v1/report.md` — Finding R1 (8 stale `App.X` docstrings), R2 (1 typo in `PLang.Tests/GlobalUsings.cs:64`), S2 (document `Default` carve-out under lowercase rule).
- Coder summary, tester report, security report.

## Decisions on proposals

### CLAUDE.md proposal — coder v3 (target `/CLAUDE.md`)

**Decision: apply.**

The three stale-line edits (lines 18, 39, 41) all reference paths that no longer exist
post-rename. The new "Runtime2 Conventions" block draws the property-access vs type-reference
distinction that's the single most error-prone thing about the rename — it's canonical and
applies to all future work in this subtree. S2 (Default carve-out under `app/filesystem/`)
will be folded into the same block.

### character-proposals.md

None filed.

## Work items

1. **Apply CLAUDE.md proposal** to `/CLAUDE.md`:
   - Line 18: `global::App.Channels.@this.Output` → `global::app.channels.@this.Output`
   - Line 39: `Data<App.Variables.Variable>` → `Data<app.variables.Variable>`
   - Line 41: `PLang/App/Data/` → `PLang/app/data/`; clarify test folders stay PascalCase
   - Add new Runtime2 Conventions bullet covering: app/ lowercase rule, 7 merged engine concepts under `app/modules/`, property-name PascalCase carve-out, `Default` keyword carve-out under FileSystem.

2. **Scrub R1 + R2 docstrings** — 8 sites in codeanalyzer report + `PLang.Tests/GlobalUsings.cs:58-59`:
   - `PLang/app/data/this.cs:554`
   - `PLang/app/GlobalUsings.cs:64`
   - `PLang/app/channels/channel/events/this.cs:10`
   - `PLang/app/types/Registry.cs:39`
   - `PLang/app/callstack/call/Position.cs:8`
   - `PLang/app/modules/settings/IStore.cs:63`
   - `PLang/app/errors/CallbackGoalErrors.cs:27`
   - `PLang.Generators/Discovery/this.cs:41`
   - `PLang.Tests/GlobalUsings.cs:58-59` (R2)

3. **Sweep `PLang/app/GlobalUsings.cs` comment block** (lines 19, 20, 64-79) — many other stale
   `App.X` namespace-reference comments that codeanalyzer's R1 list did not enumerate but
   clearly drift in the same way. Scope: comments only; preserve property-access references
   (`ctx.App.X`).

4. **Audit per-folder CLAUDE.md files** — none exist (`./CLAUDE.md` is the only one), so no
   per-folder work.

5. **Verify Documentation/v0.2/ + Runtime2 sweep is complete** — coder already did the bulk;
   spot-check for stragglers.

6. **CHANGELOG entry** — write to `v1/result.md` so the next aggregated release notes pick it up.

## Out of scope

- S1 (`app/data/Code/` → `app/data/code/`) — folder rename, coder/architect work, not docs.
- S3 (`environment.run` / `builder.load` deliberate naming pass) — Ingi's call.
- Editing `characters/*/character.md` — no proposals filed.

## Verdict criteria

PASS if: proposal applied, R1+R2 sites scrubbed, no remaining stale `App.X` type-position
references in production C# docstrings, build still clean. Coder's S1 folder rename and S3
naming pass are flagged but non-blocking.
