# Friction: reviews from plang's own users

The bots are the users of plang and its builder. We eat our own dog food (Ingi: "they are the user's og the plang builder, they should bring reviews … we can make it amazing with everybody's help"). So this is a user review as much as a bug list: where the builder misunderstood what you wrote, where an error didn't tell you what to do, what you had to work around, what you wished the language did, and anything that made building, debugging or tracing harder.

Collected by the architect from every bot, to go over with Ingi. Each entry: **who**, **what** (the flaw or weakness), **cost** (what it cost, with evidence), **if I had** (the wish: "if I had X, I could do Y"). Bots send entries to the architect or keep them in `.bot/app-systems/<bot>/friction.md`; the architect gathers them here and marks each **new**, **discussed** (with Ingi's answer) or **done** (with the commit).

## Building and the builder's LLM

- **architect · no single view of a step's pick.** Why a step got its actions is spread over the decider's stage-1 scores, stage 2's answers, the Near/Possible marks and Agree's refusals. **Cost:** issue 40 was pieced together from `%answer%` with jq (77984deec); a `goal.call` dropped 6/10 for a day. **If I had** `--debug={"pick":true}` printing one table per step (each candidate action: stage-1 score, stage-2 answer, final mark, why it was kept or dropped), a misbuild would explain itself. **new**
- **architect · a retry isn't marked as one.** FixSteps' re-ask and its answer print like any other exchange, and the error channel's display of a refusal reads like a failure. **Cost:** issue 39 was first diagnosed as "FixSteps' set fails" (5c8a4857b retracted it). **If I had** retry blocks labelled (`=== RETRY 1 of step 2: <reason> ===`), the first read would be right. **new**

## Debugging and tracing

- **architect · the debug watch misread shortcuts.** `%!error%` listed as `(undefined)` while the step read it. **Cost:** two wrong diagnoses (cache-off, issue 39). **Done:** bf8519ed5 (every variable read through its own door). Open question: what else in the debug output reads differently from the step?
- **architect · counting a `.pr` by text grep.** The `.pr` mixes indented and compact JSON. **Cost:** issue 39's "argument quietly lost" and the teaching's 0/3 before/after were a grep artifact (3d60d8f99). **If I had** one shared command that prints a `.pr` as a table per step (actions, options, values), every bot would count the same way. Meanwhile the rule: counts by jq, a surprising count checked against one raw `.pr` and one raw answer. **new**

## Testing and gating

- **architect · most plang tests don't run.** `plang --test`: 380 found, 57 run, 323 have no `.pr`. **Cost:** the plang suite says "0 fail" while 85% of it is silent. **If I had** a gate step that builds every test goal (or fails on a missing `.pr`), "0 fail" would mean the suite passed. **new**
- **architect · 18 known C# failures in every gate.** **Cost:** a new failure must be told apart from the 18 by name; a flake in the 18 hides. **If I had** them fixed or quarantined (marked, not run), a gate would be green or red. **new**
- **architect · gating a side branch disturbs my tree.** **Cost:** `app-systems-choices` was gated only by the coder; mine runs on app-systems' working tree. **If I had** the gate run in its own git worktree, any branch could be gated without touching the tree I review in. **new**
- **architect · stale binaries.** `./dev.sh test <Class>` builds only that suite, not PlangConsole (CLAUDE.md proposal v6); CLAUDE.md says `Tests/`, the folder is `test/` (proposal v7). **Cost:** a stale `plang --test`; a wrong path sent to the builder. **new**

## Secrets

- **architect · a key nearly went to the model.** A setting's default (the `OPENAI_API_KEY`) printed in the builder's prompt and a golden; caught before commit, fixed at the one door (a `[Sensitive]` option has no default shown). **Cost:** the key is in one session's transcript. **If I had** the gate count key-like strings in every log, golden and `.pr` (I do it by hand now), a leak would stop the gate. **new**

## Coordination between bots

- **architect · messages that may not arrive.** A send to a busy session can fail or time out; the os bot (Remote Control) gives no receipt; my session name changes on a restart. **Cost:** relays sent twice or not at all; a ruling waited on a bot that never read it. **If I had** a receipt (read, or held), I'd know which bots have which rulings. **new**
