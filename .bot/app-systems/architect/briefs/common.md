# app-systems review: what every reviewer reads first

Traces to: the conversation with Ingi logged in `.bot/app-systems/architect/summary.md` as decisions 665 (the bar, the reviewers, the freeze), 670 (plan tests move when done), 689 (events locked), 717 (the final baseline) and 718 (C# is the coder's; the readiness check). Chain: idea → conversation → summary (this text) → code (the plang in each brief).

## Why

Ingi (2026-10-12): "I would like to merge all the way back to runtime2 … only coder has been doing the work, and we havent been going throught the right process, e.g. coder -> tester -> security -> audit … is there anything critical left that we should fix before we merge into parent branch." No branch on the line since runtime2 (`0ea5a4b94`, 2026-06-05) has had a tester, security or auditor pass. You are that pass, for the whole line, reviewed on `app-systems`, its tip.

## The line

runtime2 `0ea5a4b94` → scalars-as-native → ireader-read-path → settings-config-unification → cli-app-property-override → module-discovery → get-builder-running → builder-formal → app-systems. Each parent has nothing of its own since, so each merge up is a fast-forward. `git diff 0ea5a4b94..app-systems -- PLang/` is 977 files, +54k −32k.

## The bar

Ingi (decision 665): **every known failure owned, not every test green.** A red with an owner and a reason is not a finding. A red without one is.

## The baseline (gate 185, decision 717; checked on two machines, 718)

- **C#:** only 6 fail, the snapshot family, tagged `[Property("carried","test/plan/snapshot")]`; they wait on the snapshot plan (`test/plan/snapshot/`).
- **plang:** 276 tests: **245 pass, 16 fail, 12 stale, 3 skipped.** Each failure and stale test has an owner and reason in `.bot/app-systems/builder/baseline.md`.

## Known and carried: cite, don't re-raise

Before you report something, check these. If it's there, write "carried: <file> <entry>" and move on; your report stays signal.

- `.bot/app-systems/architect/next-branch.md`: everything Ingi moved to the next branch while this one is frozen (reflection and one door to a type's members; the pick list's name strings; one Sensitive mechanism; the channel Timeout; the `{item, json}` door, where parsed json lands as a C# `JsonElement` carrier; a serializer Reader throwing its refusal; kinded Type choices; the os merge's core bends; and more).
- `Documentation/Runtime2/obp-cleanup.md`: the parked OBP list.
- `.bot/app-systems/architect/summary.md`: the decision log, newest first (718 at the top). A behaviour that looks wrong may be a ruling; search the log before calling it a bug.

## The locked core

Read "The locked core" in `CLAUDE.md`. Its surface (app's members, the type system, the serializer, data, channel and stream, call and variable memory, events, the module registry and catalog, the core value types) changes only with Ingi's yes. A finding whose fix changes that surface is a **proposal** in your report, never a fix. Inside it, behaviour fixes and private refactors are fine.

## How to run things

- Build: `./dev.sh build` (incremental; builds the test projects and PlangConsole).
- C#: `./dev.sh test` for the full sweep, `./dev.sh test <ClassName>` for one class. **Not** `dotnet run --project PLang.Tests`, which some character files still say; the aggregate project isn't runnable.
- plang: `cd test && ../PlangConsole/bin/Debug/net10.0/plang --test`, always from `test/`, after `./dev.sh build` (the binary isn't rebuilt per session; a stale one makes phantom failures).
- Never delete `.build/` folders; never hand-edit a `.pr`; never print a secret value (keys, tokens): counts and yes/no only.

## Feature freeze

app-systems is frozen: cleanup only (Ingi, 665). Bigger work goes to the next branch. So a finding is either **blocks the merge** (say why) or **carry** (say where it belongs). Mark each one.

## Messages to the architect

When you message the architect, use this shape (Ingi, 2026-10-13):

```
<subject: what was done>
Done
- <what was done>
HASH: <commit>
CHECK: <the result that proves it>
OPEN: <what couldn't be done or needs a decision; "none">
```

## Where your output goes

`.bot/app-systems/<you>/` as your character says, with your report json at `.bot/app-systems/`. The architect reads it, verifies each finding against the code, and brings the merge-blocking ones to Ingi.
