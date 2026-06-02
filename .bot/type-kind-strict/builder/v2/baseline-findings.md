# v2 — Real baseline (one correctly-invoked self-build) + root cause

## Correct invocation (was getting this wrong)
Per building-the-builder.md: cwd = **`os/`** (NOT os/system), and every `files` entry
MUST start with **`system/builder/`** (a `builder/`-prefixed or bare filter silently pulls
in dozens of unrelated goals → phantom failures). My first baseline violated both →
invalid. Harness now fixed (selfbuild.sh).

## Baseline result (cache:false, correct invocation)
- **Self-build FAILS** (exit 1): aborts compiling `BuildStep/Start` — steps 0
  (`set %step% = %goal.Steps[planStep.index]%`) and 1 (`call Compile`) get **no actions**.
- Before aborting: 101/104 steps matched the oracle; **3 spurious-extra divergences**:
  - `Build[10]` `save … to file` → `file.save, +variable.get` (phantom peer)
  - `Plan[1]` `render template …` → `+file.read, ui.render, variable.set` (Plan.llm:77 over-emit leaking through)
  - `QueryAndValidatePlan[0]` `llm.query …, cache=…` → `+cache.wrap` (cache= param read as a modifier)
- Partial-build cost: 80 calls, 21.5k output tokens, $0.064.

## ROOT CAUSE of the empty-actions (the blocking failure)
Traced via `--debug llm.response` + trace stepPasses:
- Planner is **correct** — returns `["variable.set"]` / `["goal.call"]`, VeryHigh.
- Compiler's recorded response for both trivial steps is **entirely null**
  (`formal=null, actions=null, errors=null`). nano returned an empty/blank object.
- **Why:** the compile call for a *trivial* `set %x% = %y%` step ships a **~8 KB user
  message** (full variable.set notes incl. the 8-row write-to producer table + the entire
  type-reference + kind vocabulary) on top of a **16 KB `Compile.llm` system prompt** —
  ~24 KB of mostly-irrelevant context. nano drowns and returns nothing.

This is direct evidence for the "reduce context + lean on formal" fix:
- `Compile.llm` = 16 KB, on EVERY compile call, heavily self-repetitive (modifier-vs-peer
  ~3×, write-to 2×, %!data% 2×). #1 lever.
- Per-action notes bloat (chars): condition/if 4692, error/handle 2933, goal/call 2718,
  variable/set 1938(+1117 examples), output/write 1543.
- `CompileUser.llm` type-reference block = 4796, rendered every call.

## Slim Compile.llm result + the Compile[1] correction
Slimming Compile.llm 16 KB → ~3.5 KB (formal-centric, dedup, "map clauses — nothing
more/less", missingVariable block deleted) made one correct build COMPLETE (was: abort),
103/104, all 3 phantom-extras gone. HUGE win.

The lone remaining divergence is `Compile[1]` (`builder.actions ..., write to %actions%`):
the slim prompt emits `builder.actions, variable.set` (its general write-to→variable.set
rule), but the committed oracle has bare `builder.actions`. I hand-patched the oracle to
ADD the variable.set — and it REGRESSED the self-build to the old fast-fail (Build step 0
empty, 3/3). Reverted. **Lesson: `builder.actions` populates `%actions%` itself; an explicit
`variable.set(Name=%actions%, Value=%!data%)` clobbers it.** So either the slim prompt
over-emits on this specific step, OR my hand-added variable.set was just mistyped
(`type:{name:list}`) and a correctly-typed one would be fine — OPEN QUESTION. Do NOT
re-commit a Compile[1] variable.set without proving the rebuilt builder still builds.

Better framing for a bootstrap: the real test is a FIXPOINT — build with the slim prompt,
use the OUTPUT as the running builder, build again; stable = it reproduces itself. The
fixed-oracle harness is a proxy.

## CORRECTION (Ingi was right): it was NOT nano — it was a POISONED CACHE
The "empty {} ~80%" conclusion was WRONG. A failing build showed **20 `[≡]` cached, 0 `[✓]`
fresh** — every step a cache hit. The `llmcache` table held a degenerate planner entry
(`"description":"No goal steps were…"`) and empty/bad compile entries that got **replayed**.
`--build cache:false` did NOT give clean builds: `%!build.cache%` correctly = False, but the
local llmcache was still being served (cache:false bypass is incomplete for the build path —
real bug to file). So every measurement above (1/5, 0/5, 0/3) was **cache replay, not the
model**. After `DELETE FROM llmcache`, the minimal set-default goal builds cleanly every run.

Cache DBs: `os/.db/system.sqlite`, `os/system/.db/system.sqlite`, `Tests/.db/system.sqlite`
(table `llmcache`, cols `key,data`). No `sqlite3` binary — use Python's `sqlite3` module.

**Consequences:**
- The harness MUST clear `llmcache` before measuring (cache:false is not enough). Added.
- The builder may be far healthier than the polluted baselines implied — re-measuring fresh.
- Two real bugs to track: (1) `cache:false` doesn't fully bypass the local llmcache on the
  build path; (2) degenerate/empty LLM responses get cached and replayed (no validation
  before caching) — a single bad response poisons all future builds until cleared.

## (SUPERSEDED) PIVOT: the dominant failure is nano returning empty `{}`, not prompt size
Measured `set default %x% = Y` compile reliability (minimal 3-step goal, cache:false, ×5):
- original Compile.llm (16 KB): **1/5**
- slim Compile.llm (3.5 KB): **1/5**
- isolated single run (with debug): occasionally succeeds with perfect JSON

So nano returns a **fully-blank** compile response (`formal=null, actions=null`, no throw →
builder.validate sees "no actions") ~80% of the time for a BASIC variable.set — independent
of prompt size. Prompt-slimming fixed phantoms + cut context (real wins) but CANNOT fix this.

**Hypothesis (strong): the compile OUTPUT SCHEMA complexity causes it.** The planner uses a
simpler schema (`{description, steps:[{index, actions:[string], confidence}]}`) and is
reliable. The compiler uses a deeply-nested schema (`actions:[{module, action,
parameters:[{name,value,type}], ...}]`) and returns empty ~80%. Same model, same context
size — the difference is schema shape. nano + json_schema constrained decoding appears to
emit `{}` when the schema is too nested.

**Implication:** this is exactly what lever #4 (formal-as-SOLE-output: nano emits one
`formal` STRING, C# parses it into actions[]) would fix — the deferred lever is likely THE
fix for the blocking failure, not just an optimization. A single string is robust where the
nested schema is not. (Caveat: the blank responses null the `formal` field too, so verify
nano reliably returns a formal STRING under a minimal `{formal:string}` schema before
committing to the parser — quick test: temporarily set QueryAndVerify Schema minimal, measure.)

Retry-on-empty alone can't rescue an 80% empty rate across 104 steps (0.8^104 ≈ 0).

## TRUE baseline (clean cache + slim prompts): 97 fresh, 1 real bug
Cleared llmcache, ran one fresh self-build (cwd=os/, 8 files):
- **97 `[✓]` fresh, 0 `[≡]` cached, ZERO "no actions"** — the empty-actions failure is GONE.
- Build still exits 1 on ONE real, non-cache bug at `BuildStep/Start.goal:25` (Compile):
  `DeserializationFailed: Failed to deserialize List\`1 to this: Expected string or object
  for app.type.@this, got StartArray`. A compile `type` field came back as a JSON ARRAY;
  the type-kind-strict deserializer wants string or object. This is the next real target
  (type-kind-strict territory) — NOT cache, NOT nano-empty, NOT prompt size.
  Likely fix: either the LLM emits a list type as an array (clarify in CompileUser/Type
  reference) OR `app.type.@this`'s JSON deserializer should handle/reject an array case
  (runtime — Ingi OK'd runtime changes for this). Capture the raw type value next.

Harness updated: clears llmcache before EACH trial (cache:false isn't enough). The restore
in selfbuild.sh was already correct (`git -C $REPO`); only my one-off manual command had the
cwd bug.

## TRUE failure map (clean cache + slim prompts + type-array fix, N=5)
Every trial now reaches 101–104/104 steps (was: cache-replay empties). 0/5 full pass, but
the failures are finally REAL and distinct (no longer cache artifacts):
1. **`llm.query` NullReferenceException at `BuildGoal/Plan.goal:26`** — the PLANNER call
   itself NREs (3/5), on the validation-retry path. RUNTIME bug, not cache/prompt/my-fix
   (trial 5 hit it at 104/104 correct). **Biggest remaining blocker.** Ingi OK'd runtime
   changes — next to investigate.
2. **`call …/EmitBuildEvent` → `event.on` + a HALLUCINATED action**
   (`validator.enforceIdentifierGoalName`, `validation.ensureIdentifier`) — 2/5. The old
   call→event.on mis-map, now also inventing non-existent `validator.*` actions → "Action
   not found". Prompt-layer (Plan/Compile + goal/call + event/on notes).
3. planner step-count mismatch, retry exhausted — 1/5.
4. occasional empty `[]` actions (QueryAndValidatePlan[0], BuildSubGoal[4]) — a couple steps.
5. minor over-emits: `Compile[1]` +variable.set (the builder.actions self-capture, 2/5),
   RefineActions[2] +cache.wrap (1/5).

The type-array DeserializationFailed is GONE from the failure list (the C# tolerance hack in
PLang/app/type/this.json.cs works — converts the crash into first-element/null).

**Bottom line:** the cache fix + slim + array-tolerance removed the dominant crashes; what
remains is genuine long-tail per-step flakiness (~97–100%/step, but ×104 steps → most full
builds trip on 1–3 bad steps) plus a real `llm.query` planner NRE. Stable full self-build
needs: (a) fix the planner llm.query NRE, (b) raise per-step mapping reliability
(call→event.on + hallucinated-action guard).

## Status of prompt-slimming (levers 1+3) — DONE, kept, non-regressing
- Compile.llm 16 KB → 3.5 KB (formal-centric, dedup, missingVariable removed, "map clauses —
  nothing more/less"). Fixed all 3 phantom-extra divergences; build completes when nano
  doesn't blank out. No set-default regression (1/5 == 1/5).
- variable/set.notes.md 1938 → ~600 chars; set.examples.md trimmed + added a set-default example.
- Plan.llm slim drafted (harness/Plan.llm.draft) — NOT applied (planner isn't the bottleneck).

## Fix plan (levers 1+3)
1. Slim `Compile.llm` → formal-centric, each rule once; add "emit exactly the actions the
   step's clauses name — no phantom peers/modifiers, never an empty list for a real clause"
   (hits both failure modes). Delete the dead `missingVariable` block (per Ingi).
2. Trim per-action notes (variable/set write-to table, condition/if, error/handle).
3. Plan.llm: drop the render→file.read over-emit (fixes Plan[1]); trim compiler-concern bloat.
4. Re-measure each change with ONE correct build; iterate to a few stable runs before any fan-out.
