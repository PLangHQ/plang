## architect — v1 — 2026-05-29
**Target:** /CLAUDE.md
**Why:** This branch renames `app/**` plural/PascalCase folders to singular+lowercase and reshapes `app.X` into a collection-node accessor. The repo CLAUDE.md's Runtime2 Conventions block names several plural namespaces by name (`app.channels.@this.Output`, `Data<app.variables.Variable>`, `app.types.path`, etc.) and the lowercase-vocabulary list enumerates the plural folder names — these become stale references on merge. Also, the accessor convention is new canonical guidance future `app/` work must follow.
**Proposed change:**

Reference-token updates (apply on merge, after the rename lands):
- `app.channels.@this.Output` → `app.channel.@this.Output`
- `Data<app.variables.Variable>` → `Data<app.variable.Variable>`
- `app.types.path...` → `app.type.path...` (the System.IO-ban rule's verb-surface references)
- The lowercase-vocabulary list — update the enumerated folder names to singular: `actor, goals→goal, variables→variable, channels→channel, errors→error, events→event, filesystem, formats→format, keepalive, snapshot, tester, types→type, config, callstack, data`.

New convention to add to the Runtime2 Conventions block:

- **`app.X` is the collection node, not a wrapper.** Each concept `X` exposes its collection at `app.X` (type `X.list.@this`, folder `X/list/this.cs`), owned once by the singleton app (or `actor` for channel). Select with `app.X["name"]`, enumerate with `app.X.list`. A concept that execution flows *through* also has `app.X.current` (e.g. `app.goal.current` reads `CallStack.Current.Action.Step.Goal`); a concept nothing is ever *inside* (type, channel, event, module, format) has no `.current`. There are no "entities vs services" — only collections, some with a current. The collection never lives on the element and is never a flat `App<Plural>` property (the deleted `AppGoals`/`AppChannels`/`AppEvents`/`AppModules` aliases were that smell). **Registry = selection + lifecycle; all behavior lives on the element** — a type-switch (`is X.subtype`) inside a registry is misplaced behavior, push it onto the element as a virtual member. `module` is a no-`.current` service (action modules are dispatched, not navigated): `module/this.cs` = `module.@this` is the action registry reached at `app.module`, with no `app.module.current`.

## auditor — v1 — 2026-05-30
**Target:** characters/auditor/character.md
**Why:** On this branch I (the auditor) ran without checking that all upstream bots had pushed. Security had completed locally but hadn't pushed; I reported "no security review on branch" as a finding when in fact security existed. Ingi: every upstream bot (coder, codeanalyzer, tester, security) must contribute before the auditor runs — if any are missing, stop and report. The character already has "Your Process" step 1 ("Read ALL previous bot reports first"), but it doesn't say what to do when a report is *missing* or *stale*, and it doesn't say to fetch first.
**Proposed change:** Replace the "Your Process" section with:

```markdown
## Your Process

0. **Verify the pipeline is complete (do this FIRST, before reading any code).**
   - `git fetch origin <branch> && git pull --ff-only` — other bots may have pushed work your local clone doesn't have.
   - Check `.bot/<branch>/<bot>/summary.md` exists for each of: `coder`, `codeanalyzer`, `tester`, `security`. The order isn't fixed (security can run in parallel with tester); what matters is that all four have contributed to the *current* version.
   - Confirm each bot's latest version aligns with what you're auditing. If `coder/` is at v3 but `tester/` is at v1, the tester hasn't reviewed v3 — that's a missing review, not a passing one.
   - **If any bot is missing or stale:** write a brief `v<N>/plan.md` naming who's missing, write `verdict.json` with `{"status":"blocked","summary":"<which bot> has not run on the current version"}`, commit + push, and stop. This is a pipeline-state problem, not the coder's bug — do not write `fail`.

1. **Read ALL previous bot reports.** Codeanalyzer verdict, tester's test-report.json, security's security-report.json. Understand what they checked and what they concluded.
2. **Don't re-check what they already covered.** If codeanalyzer did a thorough OBP pass on a file and found it clean, don't redo that pass. Trust their work unless something smells off.
3. **Focus on the gaps between reviewers.** The space between file-level analysis and test-level analysis is where bugs live. Cross-file contracts, architectural fit, assumptions that one bot made but another didn't verify.
4. **Challenge the other bots.** If the tester approved but you think their tests are weak, say so. If codeanalyzer missed something, call it out. You are not here to rubber-stamp.
5. **Review the code changes.** Use `git diff runtime2..HEAD -- ':(exclude).bot'`. Read full files for context.
```

Also add a `blocked` verdict shape to the verdict.json section (currently only documents pass/fail):

```markdown
## verdict.json

Write to `.bot/<branch>/auditor/v<N>/verdict.json`:
- **pass** (no critical/major findings): `{ "status": "pass", "summary": "<one-line>" }`
- **fail** (any critical/major): `{ "status": "fail", "summary": "<one-line>" }`
- **blocked** (upstream bot missing or stale, per Process step 0): `{ "status": "blocked", "summary": "<which bot> has not run on the current version" }`
```
