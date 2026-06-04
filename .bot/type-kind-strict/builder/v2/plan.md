# v2 — Plan: make the self-build reliable on nano (prompt slimming + a competition harness)

**Status:** planning. No code/prompt edits made yet. This document is the contract so
we don't lose the thread — the harness alone is a lot of work.

## The problem (why we're here)

Building the **builder itself** (the bootstrap) is unreliable on the default model
`gpt-5.4-nano`. Ordinary user goals build fine; the builder's own goals fail
intermittently. Pinning to `gpt-5.4` traded mis-mappings for `JsonParseError` (the
bigger model returned output the builder couldn't parse), so we reverted to nano.

**Working theory (Ingi's, sharpened):** nano *can* do this. The prompts are too big and
grown by accretion; they say a lot that isn't needed and contain self-contradictions.
Slim them, lean harder on **formal plang**, and remove the prompt↔schema drift.

## Diagnosis

- **A. Always-on prompts are too big for nano.** `Plan.llm` = 111 lines, `Compile.llm`
  = 226. `Compile.llm` repeats "modifier vs peer" ~3×, `write to %var%` twice, `%!data%`
  twice. Harmless for a big model; competing/diluting signal for nano.
- **B. Much of that prose is misplaced.** Per-action guidance sits in the *always-on*
  system prompts when it belongs in the **lazy markdown teaching layer**
  (`os/system/modules/<module>/*.notes.md` / `*.description.md`), which is loaded into
  the compile call **only when the planner picked that action**. Relocating shrinks the
  stable prompt nano sees on every call AND scopes guidance to where it's relevant.
  Misplaced examples: `Plan.llm` 49–67 (`call X` vs `event.on`) → `event/on.description.md`
  + `goal/call.notes.md`; `Plan.llm` 76–80 (render/load/save over-emit) → per-action;
  `Compile.llm` write-to rule / `Actor=%!data%` artifact / modifier-nesting specifics →
  per-action notes.
- **C. Schema↔prompt contradiction (likely direct cause of parse failures).** The
  `QueryAndVerify` output schema in `os/system/builder/BuildStep/Start.goal` has **no
  `modifiers` field** and types `type?: string`, yet `Compile.llm` builds a whole
  modifier-nesting doctrine and `CompileUser.llm` demands `type` be a structured
  `{name, kind?, strict?}` dict. A model reconciling "emit `type:{name,kind}`" against a
  `string` slot is exactly what produces parse-breaking output — and explains why the
  *bigger* model (which follows the schema harder) hit `JsonParseError`. Confirm with
  `--debug={"llm":{"schema":true}}`.
- **D. "Lean on formal" — strongest form.** Today the model emits BOTH `formal` and
  `actions[]` and they "must mirror exactly" — double work + consistency burden, worst
  case for nano. End goal (#4): model emits **only** `formal`; derive `actions[]` by
  parsing it. Collapses the fragile nested JSON to one string, kills the mirror rule.
  Needs a C# formal parser → **coder handoff**, and requires pinning formal's grammar
  first (close `formal-plang.md` §7 variances).

## The levers, ordered by risk

1. **Relocate per-action prose** out of `Plan.llm`/`Compile.llm` into lazy
   `os/system/modules/*` markdown; leave only the cross-cutting kernel (formal grammar,
   modifier-vs-peer principle, output shape). Also **delete the `missingVariable` block**
   (`Compile.llm` ~22–31): we tell the model never to emit it, so the warning + its
   justification are pure dead weight. My layer, low risk.
2. **Fix the schema drift** (add `modifiers`, make `type` the structured dict) so prompt
   and schema stop contradicting. My layer, but lives in a `.goal` → **needs a bootstrap
   rebuild** to take effect. More delicate; I handle separately, NOT in the competition.
3. **Pin formal's grammar** in the kernel (one separator for captures, one `type`
   spelling, kill the `Actor` artifact). Closes §7 variances; prereq for #4.
4. **Formal-as-sole-output.** Biggest payoff. Coder handoff for the C# parser. After 1–3.

## Key enabling fact

`.llm`, `templates/`, and `os/system/modules/*` markdown are **read fresh from disk every
build**. So editing them takes effect immediately — **no bootstrap rebuild per candidate.**
A candidate only needs to edit prompt/markdown files and build a *target corpus*. This is
what makes a competition cheap enough to run. (The schema fix #2 is the exception — it's
in a `.goal`, so it needs the builder rebuilt; that's why it's out of competition scope.)

**Competition scope:** `Plan.llm`, `Compile.llm`, `CompileUser.llm`, `os/system/modules/*`.
**Out of scope (I do it):** schema drift (#2), formal grammar pinning (#3 feeds #4).

---

## The competition (Ingi's idea) — make subagents attack levers 1+3

Diverse independent attempts beat one person iterating, because "slim a prompt for a small
model" is empirical with a wide solution space. But the whole thing lives or dies on the
**eval harness**. Sequencing matters:

- "Working builder first" is **circular** — a reliable self-build IS the goal.
- "Competition first" **fails** — the self-build is non-deterministic; if baseline passes
  ~70%, variance swamps signal and you can't tell a better prompt from a lucky seed.
- **Correct answer: harness first.** Reliability is the *output metric*, not a precondition.

### Eval harness design

1. **Frozen target corpus.** The builder's own goals (they're what's flaky) PLUS a handful
   of ordinary user goals as a **held-out generalization set**. THIS IS THE CRITICAL GUARD:
   without it an agent overfits — special-cases the builder's goal/action names in prose
   (also auto-fails our "generic instructions only" rule) and regresses everyone else's
   builds. Score must reward generalization, not memorization.
2. **Runner.** Per candidate prompt-set: `cache:false` build of the corpus, **N times**
   (N≥3, nondeterminism), parse pass/fail + token usage + time straight from the trace
   `usage` fields. No new instrumentation — traces already carry model/tokens/cost/cached.
3. **Baseline.** Current prompts through the same harness → the number to beat.

### Scoring — 3 components, but success is a GATE not a peer

Trap: 3 equal points lets an agent strip the prompt to nothing, win tokens+time, fail half
its builds. So **lexicographic**:

1. **Gate — pass-rate** across N×corpus. Must meet-or-beat baseline to qualify at all.
   This is the actual problem.
2. **Among qualifiers — total output tokens** (cost axis; output tokens most expensive).
   Dominant driver is **# of LLM calls incl. retries** — every `LlmFixer`/`RefineActions`/
   `FixValidation` loop is another full call. Better prompt → fewer loops → fewer tokens +
   faster + more reliable. The three metrics are correlated and point the same way.
3. **Tie-break — wall-clock time.**

Lexicographic (gate → tokens → time) is cleaner and far less gameable than a weighted sum.

### Mechanics

Workflow tournament pattern: fan out M candidate agents, **each in its own git worktree**
(isolate prompt files / cache / `.pr` / traces), same brief + harness each, produce a
candidate prompt-set; scoring stage runs the harness on each; rank; keep winner, graft best
ideas from runners-up. **Cost flag:** every candidate runs N real builds × many LLM calls —
genuinely expensive. Cap M and N deliberately.

---

## Proposed execution sequence

1. **Build the harness** — corpus (+ held-out set) + runner + baseline number. (Big chunk.)
2. **I do levers 1+3 manually** → a second baseline. Proves the corpus is buildable and
   the harness scores sanely BEFORE spending tokens fanning out, and gives the competition
   a real target to beat.
3. **I do lever 2** (schema fix, bootstrap rebuild) on the side.
4. **Run the competition** against my baseline. Winner ships; if no one beats it, my
   baseline ships.
5. **Lever 4** (formal-as-sole-output) as follow-up with a coder handoff.

## Open decisions before we start
- Corpus exact contents + size of held-out set.
- N (trials per candidate) and M (number of competing agents).
- Whether the harness is a PLang goal, a shell script, or a Workflow script.

## Non-negotiable
- The **held-out generalization set** in scoring. Everything else is tunable; this is the
  guard that stops the competition from making the builder reliable while quietly breaking
  normal builds.
