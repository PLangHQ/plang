# Plan stage 3 — prompt C (formal, the decider's picks) vs prompt B, and the double check

Branch `builder-formal`, python. The same 5 golden goals (58 steps), both models, 3 rounds. Each round runs the decider (v5) once per goal, and **its real picks feed both prompts** (no hand menus). The golden's hand menus are used only as the expected answers.

- **Code:**
  - `tools/decider/prompt_c.py`: C's user message and the double check;
  - `os/system/builder/llm/PropertiesC.llm`: C's system message;
  - `tools/decider/c_eval.py`: the eval.
- **Raw:** `tools/decider/runs/c_eval_round{1,2,3}_*`.
- **Rendered requests (what Ingi reads):**
  - `/shared/coder/llm/plang/builder-formal/prompt-c-round{1,2,3}/<goal>/{system,user}.txt`;
  - `prompt-b-round{1,2,3}/<goal>/…` for B;
  - the decider requests of each round in `decider-round{1,2,3}/`.

## What each prompt gets

- **B:** unchanged prompt B, with its menu = the decider's picks ≥ 0.5 (names only). Answers in JSON. Checked by `build_pr.match`: steps line up, the chain rule, fold.
- **C:** B's shape (the goal as written, Types, each action once), with under each step the decider's picks and a pre-filled formal line:

```
  [0] - read 'orders/%orderId%.json', write to %order%
        decider: file.read 0.98, variable.set 0.79 (write to)
        formal:  file.read(Path=?); variable.set(Name=%order%, Value=%!data%)
  [3] - call Compile, on error call HandleBuildFailure
        decider: goal.call 0.95, error.handle 0.97
        formal:  error.handle(Recovery=[goal.call(Name=?)]) { goal.call(Name=?) }
```

- The pre-filled line:
  - picks ≥ 0.9 pre-filled with `?`, and 0.5–0.9 listed as "possible";
  - known values filled: `write to %x%` lists and fills variable.set whatever its score, and `on error call X` fills `Recovery=[goal.call(Name=?)]`;
  - goal.call's definition is added when a listed action holds actions;
  - a modifier is shown wrapping the step's action.
- C answers in formal without types; `formal.parse_answer` types it.
- **The double check** (`prompt_c.check`):
  - fold: a copied body over indented steps is dropped, and any other body there is refused;
  - `build_pr.match`: steps line up, and the chain rule;
  - agreement: a pick ≥ 0.9 left out → refused; an action the decider didn't list → refused; an action held as a value other than goal.call and not listed → refused; a Recovery that runs the action it wraps → refused;
  - a step built from a 0.5–0.9 pick, or a disagreement settled on retry → a warning on the step.
- **Both prompts:** a refused answer is told why and answered once more (FixProperties' conversation). Still refused → the goal fails loudly.

## The running comparison

Round 1 ran prompt C v3; rounds 2 and 3 ran C v4 (below). B is the same in all three.

| round | prompt | model | first-attempt right | caught | silent | after retry: right | silent | failed (loud) | disagreements | warnings | seconds (sum; slowest goal) | tokens in / out | cost |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 (C v3) | B | nano | 45 | 6 | 7 | 39 | 7 | 12 | 0 | 0 | 66.4; 20.1 | 20,108 / 11,981 | $0.0163 |
| 1 (C v3) | B | mini | 49 | 0 | 9 | 49 | 9 | 0 | 0 | 0 | 25.7; 6.7 | 14,782 / 4,923 | $0.0260 |
| 1 (C v3) | C | nano | 45 | 12 | 1 | 57 | 1 | 0 | 0 | 14 | 17.2; 5.1 | 20,017 / 1,634 | $0.0055 |
| 1 (C v3) | C | mini | 38 | 18 | 2 | 48 | 10 | 0 | 0 | 14 | 15.2; 4.6 | 24,171 / 1,938 | $0.0224 |
| 2 (C v4) | B | nano | 41 | 6 | 11 | 35 | 11 | 12 | 0 | 0 | 71.7; 21.7 | 20,042 / 11,831 | $0.0164 |
| 2 (C v4) | B | mini | 50 | 0 | 8 | 50 | 8 | 0 | 0 | 0 | 24.8; 6.9 | 14,782 / 4,888 | $0.0246 |
| 2 (C v4) | C | nano | 34 | 23 | 1 | 45 | 1 | 12 | 0 | 14 | 15.8; 4.3 | 23,297 / 1,867 | $0.0060 |
| 2 (C v4) | C | mini | 57 | 0 | 1 | 57 | 1 | 0 | 0 | 14 | 9.6; 2.2 | 16,277 / 1,410 | $0.0186 |
| 3 (C v4) | B | nano | 46 | 6 | 6 | 49 | 9 | 0 | 0 | 0 | 79.6; 24.1 | 25,438 / 13,972 | $0.0190 |
| 3 (C v4) | B | mini | 53 | 0 | 5 | 53 | 5 | 0 | 0 | 0 | 23.4; 6.2 | 14,782 / 4,895 | $0.0262 |
| 3 (C v4) | C | nano | 30 | 26 | 2 | 44 | 2 | 12 | 2 | 14 | 18.2; 5.6 | 27,453 / 2,187 | $0.0065 |
| 3 (C v4) | C | mini | 50 | 0 | 8 | 50 | 8 | 0 | 0 | 14 | 10.0; 2.2 | 16,277 / 1,373 | $0.0184 |

**Totals over the 3 rounds** (174 steps each; decider not included: tokens only, no known price):

| prompt | model | first-attempt right (of 174) | silent, first | after retry right | silent, final | failed (loud) | seconds per goal | cost per goal | tokens out per goal |
|---|---|---|---|---|---|---|---|---|---|
| B | nano | 132 | 24 | 123 | 27 | 24 | 14.5 | $0.00344 | 2519 |
| B | mini | 152 | 22 | 152 | 22 | 0 | 4.9 | $0.00512 | 980 |
| C | nano | 109 | 4 | 146 | 4 | 24 | 3.4 | $0.00120 | 379 |
| C | mini | 145 | 11 | 155 | 19 | 0 | 2.3 | $0.00395 | 315 |

**Silent** means wrong in the .pr with no refusal: the one that matters.

## Recommendation: gpt-5.4-nano with prompt C

- **Silent misses: 4 of 174**, against 19 for C-mini, 22 for B-mini and 27 for B-nano. nano's wrong answers are refused almost every time: caught, then right after the retry, or failed loudly. That's the property the 0-silent bar needs; a loud failure costs a rebuild, and a silent one ships a wrong program.
- **Cheapest by far:** $0.0012 per goal, 4× under C-mini and 3× under B-nano. It writes 379 tokens out per goal against B-nano's 2,519: formal is 6–7× shorter than JSON.
- **Fast:** 3.4 s per goal against B-nano's 14.5 s. C-mini is faster still (2.3 s), but its silent misses are 5× nano's.
- **Its weak spot is loud:** 24 failed steps. That's weekly_report failing whole (12 steps) in rounds 2 and 3, from two causes with fixes below.

C-mini is the runner-up. If proposal 1 below removes its dropped-`Left` class (11 of its 19 silent misses), it's worth a re-measure, being faster and better on the first try (145 vs 109).

## Every silent miss and every loud failure, prompt-first

| model / rounds | what | cause | proposal |
|---|---|---|---|
| C-mini r1, r3 | condition.if without `Left` (6 steps in r3) | **the pre-filled line**: `condition.if(Operator=?)` shows only required properties, and `Left` is declared optional (`Left?: item`), so mini drops it | **1** |
| C-nano r2, r3 | `save %trace% to file …` → file.save without `Value` | the same: `Value?: item` is optional, so the pre-fill shows `file.save(Path=?)` | **1** |
| C-mini r2 | `set %modules% = %!app.module.list%` → `Value=%!data%` | model | — |
| C-mini r3 | `if %errors% is empty` → `isnotempty`; `is a list` without `Right` | model (the operator table is in the notes) | — |
| C-nano r3 | `foreach …` with `Item=%item%` | model (foreach's notes say leave Item out) | — |
| C-nano r2, r3 **failed** | weekly_report: `Operator=isempty`, `Right=list` unquoted; after the retry, the indented body copied into step 3's `{ }` | a choice value unquoted; the copied body only partly equal to the indented steps | **2**, **3** |
| B-nano r1, r2 **failed** | weekly_report: steps 4, 5 ("write out …", "return") left without actions: it nests them in step 3 | the indented body (as run 4 on get-builder-running) | — |
| B-mini all | goal.call arguments as `[{'kind': 'x'}]` / `["code=%x%"]`, a doubled variable.set | B's JSON shape | — (C doesn't have these) |

**Proposals:**
1. **Pre-fill what the step will give, not only what's required.** The two classes of silent miss are optional-by-declaration properties the step does give (condition.if's `Left`, file.save's `Value`), and the pre-fill hides them.
   - Options:
     - (a) pre-fill every property without a default as `?`; a `?` left unfilled fails the parse, so the LLM must fill it or delete the property;
     - (b) make `Left` required on condition.if/elseif and `Value` on file.save in C#, where they really are always given.
   - I'd measure (a). (b) is right on its own, and it's a C# change for stage 4.
2. **A bare choice value** (`Operator=isempty`) is exactly one of the property's options, so it isn't ambiguous. Either the parser accepts it, or the error says "quote it" (it does now, and nano didn't fix it on the retry). I'd accept it: the notation stays strict for text, and a choice option is a name, not a text.
3. **A partial copy of an indented body.** Fold drops a body over indented steps only when it equals them exactly. A body whose actions all come from the indented steps (a subset) is a copy too, and the builder places the steps anyway. Dropping it removes the rest of weekly_report's loud failures.

## What the double check did

- **Disagreements** (the LLM against the decider): only 2 flagged in 3 rounds, both nano, both settled on the retry. With the picks pre-filled, the LLM rarely leaves a certain action out or adds one.
- **Warnings:** 14 per model per round, all "built from a possible pick". Almost all are write-to → variable.set (0.5–0.9), which the known-value rule fills. Those are right, so a warning on them is noise. I'd suppress the warning when the known-value rule (write to) is what put the pick there.
- **The refusals that did the work** are the parser's own: fix-carrying errors that the retry usually fixed (`[i]`, `;`, `?` unfilled, arguments as `{k: v}`) and the chain rule.

## How C got here — four versions in round 1 (all measured, kept in runs/)

| C | first-attempt right (nano / mini) | what was wrong |
|---|---|---|
| v1 | 19 / 4 | the pre-filled line joined actions with ` · `, and the models copied it; arguments written `[k=v]`; bare text; `%?` unfilled — and the parser's errors didn't say how to fix them |
| v2 | 0 / 10 | "every line starts with the step's index" → the models wrote `0 …`, dropping the brackets |
| v3 | 45 / 38 | `[0]` shown in a two-line example; `;` in the pre-fill; the arguments as a rule with an example; fix-carrying errors; a modifier pre-filled wrapping the action (it was read as a catch block) |
| v4 | (rounds 2–3) | "the formal line names only the required properties — add the others"; `on error call X` pre-fills its Recovery; a Recovery that runs the action it wraps is refused |

A check bug of mine voided the first rounds 2 and 3 of v4: it refused a Recovery goal.call beside a wrapped goal.call (the same module and name, different values). Those runs are kept as `…_INVALID-check-bug`, the check now compares values, and rounds 2–3 were rerun.

**Notes files changed:** goal/call and ui/render no longer describe arguments as JSON rows (`{name, type, value}`), so they fit B and C alike.

## Stopping here

Stage 4 (the plang builder) waits for your plan. Ready for your call:
- proposals 1–3;
- the warning noise;
- nano or mini.

---

# Round 4 — C + gpt-5.4-nano only, after fixes 1–4 (architect)

Commits:
- `c1e78c076`: condition.if/elseif `Left` and file.save `Value` are required in C#; the six suites show no new failures against the baseline.
- `a306dec8d`: a bare choice option parses (`Operator=isempty`).
- `fdf746089`: fold drops a partial copy of an indented body.
- `23c70d5c2`: no warning on a write-to variable.set.

The pre-fill now shows `condition.if(Left=?, Operator=?)` and `file.save(Path=?, Value=?)`.

Raw: `tools/decider/runs/c_eval_round4_20260925_220431/`. Requests: `/shared/coder/llm/plang/builder-formal/prompt-c-round4/`, `decider-round4/`.

| | first-attempt right | caught | silent | after retry right | silent | failed (loud) | warnings | seconds (sum; slowest) | tokens in / out | cost |
|---|---|---|---|---|---|---|---|---|---|---|
| round 4, C nano | **46/58** | 11 | 1 | **57/58** | **1** | **0** | 0 | 14.7; 4.1 | 19,379 / 1,608 | $0.0054 |
| rounds 1–3, C nano (per round) | 45 / 34 / 30 | 12 / 23 / 26 | 1 / 1 / 2 | 57 / 45 / 44 | 1 / 1 / 2 | 0 / 12 / 12 | 14 / 14 / 14 | — | — | — |

**weekly_report no longer fails whole.** Both of its causes are gone: the bare `Operator=isempty` now parses, and a partial body copy is dropped. Warnings fell from 14 to 0.

## Every miss, prompt-first

1. **build, first attempt: caught, whole goal (11 steps), for one line.** `[0] variable.set(Name="path", Value="/", AsDefault=true)`: the name quoted without its % signs. It is right after the retry.
   - Two points:
     - (a) **One bad line refuses the whole answer**, so "first attempt" counts 11 steps caught when 10 were right. Parsing and judging per step would refuse only step 0 and retry only it, giving 56/58 right on the first attempt in this round.
     - (b) **`Name="path"` for a `variable` property is not ambiguous.** The runtime already reads both `"%x%"` and a bare `"x"` as the variable x (`Variable.Resolve`, CLAUDE.md: "both collapse to Variable { Name = "x" }"). The parser could do the same for a variable-typed property.
   - **Proposal:** both, (a) and (b).
2. **weekly 8, silent: `render template "report.html", errors=%errors%, write to %html%`.** It answered `ui.render(Template="report.html"); variable.set(Name=%html%, Value=%!data%, Type=null)`: Parameter missing, and an invented `Type=null`.
   - Cause (prompt): the pre-fill adds `Parameter={?}` only for a `call` with arguments (`prompt_c.ARGUMENTS`), so ui.render's arguments get no slot.
   - **Proposal:**
     - pre-fill `Parameter={?}` for any action that takes arguments (goal.call, ui.render) when the step writes `name=value`;
     - make the check refuse an explicit `null` for an optional property ("leave it out: its default applies"). That's the placeholder class.

With (1a) and (2), this round would read 57–58/58 on the first attempt, with no silent misses. It's one run: the bar needs repeats before it counts.

---

# Round 5 — C + nano, 3 runs: per-step retry, variable coverage, null dropping, common steps, the popular-action choice, noul criteria

Commits:
- `62a66fc39`: bare variable names, per-step parsing.
- `44f5f80d7`: per-step retry, variable coverage, nulls dropped, ui.render notes.
- `7ae0fd284`: the common steps in PropertiesC.llm.
- `de1bce22c`: the popular-action choice on unsure steps, and the noul criteria on the 6 common-action questions.

Raw: `tools/decider/runs/c_eval_round5_20260925_2212{23,29,34}/`. Requests with their decider requests: `/shared/coder/llm/plang/builder-formal/prompt-c-round5{,-run2,-run3}/<goal>/{system.txt,user.txt,decider/}`.

(An earlier 3-run round 5 used the first (b), the raw list of 15 signatures. It's kept as `c_eval_round5_*_b-v1`. The list was never shown: no step was unsure then.)

| run | first-attempt right | caught | silent | after retry right | silent | loud | warnings | seconds | cost |
|---|---|---|---|---|---|---|---|---|---|
| 1 | **57/58** | 0 | 1 | 57 | 1 | 0 | 5 | 16.9 | $0.0054 |
| 2 | 55/58 | 2 | 1 | 57 | 1 | 0 | 6 | 17.1 | $0.0057 |
| 3 | 54/58 | 2 | 2 | 56 | 2 | 0 | 5 | 16.3 | $0.0058 |
| **total** | **166/174 (95%)** | 4 | 4 | **170/174** | **4** | **0** | 16 | | |
| round 4 (1 run) | 46/58 | 11 | 1 | 57 | 1 | 0 | 0 | 14.7 | $0.0054 |

## The decider with criteria (common-action scores, round 4 → round 5)

| | round 4 / v5 | round 5 |
|---|---|---|
| write-to → variable.set (13 steps) | 0.53–0.86 | **0.79–0.96** |
| output.write on `build.load, write to %app%` | 0.38 | **0.08** |
| goal.call, start 0 (`call /system/builder/EmitBuildEvent …`) | 0.88 | 0.63–0.73 |
| goal.call, start 3 (`call Compile, on error call …`) | 0.95 | 0.73–0.78 |
| goal.call, weekly 10 (`call SendReport …, on error call …`) | 0.94 | 0.65–0.68 |

The criteria fixed write-to. They hurt goal.call on steps that also say `on error call X`: goal.call's `false` says "a goal named to run later — `…, on error call X` … — is not called by the step", and the model reads the whole step as that. **All 16 warnings are these goal.call picks** (0.63–0.89), which are right; the others were error.throw 0.87/0.88.

## The popular-action choice

Asked on **1 step per run**: start 0, unsure because of the goal.call drop above. It picked goal.call at 1.00 each time, which is right, and the LLM's answer was right. **It caused no wrong pick.** On these goals, the steps it exists for (a decider that doesn't know) never came up.

## Every miss, prompt-first

| run | step | what | cause | proposal |
|---|---|---|---|---|
| 1, 2, 3 | weekly 11 `write out "Report sent" to "log" channel` | **silent**: `channel` missing | prompt: the new common step `write out "Hello %name%" → output.write(Data=…)` shows output.write without a channel, and channel is optional, so the pre-fill doesn't show it either. Round 4 (without the common steps) had it right | add the common step `write out "Done" to "log" channel → output.write(Data="Done", channel="log")` |
| 3 | checkout 2 `if … else if … else …` | **silent**: an extra `output.write(Data="%itemCount%")` ahead of the if | model | — (the chain rule allows an action before the if) |
| 2, 3 | weekly 3, 6 | caught, right after the per-step retry: `condition.else() { }`, an empty else invented on a condition whose body is indented | model; the parse error said only "expected a name" | the parser says "`{ }` holds at least one action; a condition whose body is indented below it has no `{ }`" |
| all | start 0, 3, weekly 10 | warnings only (right) | goal.call's criteria (above) | rewrite goal.call's criteria: true gets "`call Compile, on error call Fix` calls Compile", and false drops the on-error example |

Also seen in round 5's b-v1 runs: foreach with `Item=%item%` spelled out, which is exactly the default binding. Declaring `[Default("item")]` on loop.foreach's Item would make it a default that the builder leaves out.

**Where C + nano is:** 95% first attempt and 170/174 after the retry over 3 runs, with no loud failures. The one silent miss that repeats (weekly 11) has a one-line fix. The bar (100% on every run) isn't met yet: 4 silent in 174.

---

# Round 6 — C + nano, 3 runs: literal coverage, the channel common step, the empty-{ } message, goal.call's criteria, foreach's default + S1

Commits:
- `bd44cc7f5`: literal coverage and the channel common step.
- `9fd335bf7`: the empty `{ }` message and prompt rule.
- `c732bf0a7`: goal.call's criteria.
- `b5388e447`: `[Default("item")]` on loop.foreach Item, plus build_pr's `[Default]` look-back fix. The six suites show no new failures.
- `4f0c3a2a2`: S1, the 2.0 layout, `readable.txt`, and the example-line guard.

**Round 6 ran on exactly the code these commits hold.** The hook blocked my first commit attempt (python in the message beside a `.cs` path), and I only noticed after the runs, then committed that same code.

Raw: `tools/decider/runs/c_eval_round6_20260925_2222{52}/`, `…_222301/`, `…_222308/`. Requests: `/shared/coder/2.0/<goal>/` (run 3) and `/shared/coder/2.0/rounds/round6-run{1,2,3}/`.

| run | first-attempt right | caught | silent | after retry right | silent | loud | warnings | disagreements | seconds | cost |
|---|---|---|---|---|---|---|---|---|---|---|
| 1 | 46/58 | 12 | 0 | **58/58** | **0** | 0 | 20 | 15 | 17.8 | $0.0062 |
| 2 | 56/58 | 2 | 0 | **58/58** | **0** | 0 | 4 | 0 | 12.9 | $0.0056 |
| 3 | 51/58 | 7 | 0 | **58/58** | **0** | 0 | 8 | 3 | 16.6 | $0.0068 |
| **total** | **153/174 (88%)** | 21 | **0** | **174/174** | **0** | **0** | 32 | 18 | | |
| round 5 total | 166/174 | 4 | 4 | 170/174 | 4 | 0 | 16 | | | |

**Every step of every run ends right, with no silent miss and no loud failure. That's the bar after the retry, three runs in a row.** The first attempt is lower than round 5 (153 vs 166). Every first-attempt miss is now caught, none silent, and each has a cause below.

## Every first-attempt miss, prompt-first

| runs | step | what | cause | proposal |
|---|---|---|---|---|
| 1, 2, 3 | start 3, 13 (`… , on error call HandleBuildFailure`) | caught: runs 2 and 3 left the pre-filled `Recovery=[goal.call(Name=?)]` unfilled; run 1 wrote `Name=HandleBuildFailure` unquoted (and `Name=BuildSubGoal` on step 6) | prompt: a `?` nested inside `Recovery=[…]` is the one the model skips, and the unquoted goal name is nano's habit (the error says "a text is quoted", and the retry fixes it) | pre-fill the recovery without its own `?`: `Recovery=[goal.call(…)]`, one level less to fill |
| 1, 3 | weekly_report 3–7 | caught, whole answer: the model put steps 4–5 (indented under step 3) inside step 3's `{ }`, dropped their own lines, and renumbered the rest | prompt: the indented body shows in "the goal as written" and the model folds it itself. Rule 6 says the body is the indented steps, but nano still nests them | measure a one-line hint under a step with indented steps ("steps 4–5 are its body: the builder places them; answer them on their own lines"). Ingi's earlier rule kept indentation away from the LLM, so this is your call |
| — | checkout 2 (the extra `output.write` before the if) | **did not recur** (0 of 3 runs) | — | nothing |

## Warnings and the decider

- **goal.call with the fixed criteria:** 0.80–0.89 on build 4/7, start 0 and decide 9 (round 5: 0.63–0.78; round 4 without criteria: 0.88–0.96). Better, still just under 0.9, so each is a warning on a right step.
- **error.throw:** 0.83–0.88 on checkout 3.
- **The popular-action choice was never asked** (no step under 0.8).
- **All 15 disagreements in run 1, and the "settled on retry" warnings, are weekly_report's renumbered answer:** the model placed actions on the wrong entries. The retry fixed it.
- **S1** (defaults dropped) removed nothing wrong; no step's expected answer holds a default.

## The example-shift report

It didn't reproduce. 85 decider state files are checked by `harness.misplaced_examples`:
- the 80 written this session;
- the 5 copies in `/shared/coder/2.0`.

Every `e.g.` line comes from its own entry. The write-to lines (`get 'https://…', write to %rates%` …) at line 140 of checkout's state are the **variable** module's own (from `variable/set.examples.md`). They sit under `- variable:`, the module above `- variable.set:` in the asked-by-name list. The guard now runs on every state written, so a real shift would fail the run.

---

# Decider v6 vs v5, and round 7

## Decider v6 (Ingi's design) — measured, and v5 stays

Commit `65f5c860c`. The design:
- **stage 1:** per step one `choice` over the modules and the 15 popular actions. Each option carries structured criteria (`what`, `not_for`, `examples` as step texts), and the state holds only the goal and the task.
- **stage 2:** every option with a share ≥ 0.15 gets a `score`: 0 not used / 1 only named to run later / 2 inside a condition, loop or error handler / 3 the step's own work. A pick is level ≥ 2.
- **stage 3:** the action of a picked module option, plus else/elseif by name.

Requests: `/shared/coder/2.0/rounds/decider-v6-run{1,2}/` and `decider-v5-run{1,2}/`, with `readable.txt`.

| | v6 run 1 | v6 run 2 | v5 run 1 | v5 run 2 |
|---|---|---|---|---|
| cut 0.5: steps exact | 33/58 | 30/58 | **58/58** | **58/58** |
| cut 0.5: missed / extra | 23 / 6 | 26 / 6 | 0 / 0 | 0 / 0 |
| cut 0.9: steps exact | 28/58 | 27/58 | **45/58** | **47/58** |
| stage 1: questions, KB | 58, 719 | 58, 719 | 406, ~158 | 406, ~158 |
| stage 1: tokens in / out | 227,768 / 19,206 | 227,768 / 19,206 | ~49k / 20k | ~49k / 20k |

**Why v6 loses (prompt-first):**
1. **One choice per step can't name a step's second action.** A choice's shares sum to 1, so on `build.load, write to %app%` the build module takes 0.99 and variable.set is never a candidate; the same goes for goal.call in `foreach … call X`. This is the "which one, not which ones" limit, and v5's yes/no per common action is what covers it.
2. **The score question reads the step's words, not its structure:**
   - `call EmitBuildEvent …` made `event` a 0.16 candidate that then scored level 2.9, so event.on was picked, an extra;
   - `call /system/builder/EmitBuildEvent …` scored goal.call at level 0.84, "not used".
3. **The criteria ride in every question:** stage 1 is 719 KB, 4.5× v5's.

A pick's score was P(level ≥ 2). The docs' normalized level (score / 3) would put a right "inside a condition" pick at 0.67, so it's kept beside the pick score, not used as it.

## The near-certain cut stays at 0.9

27 decider runs on the golden goals (v3–v5 evals, the v5 runs above, rounds 1–7's deciders), 10,676 scored picks:
- **every pick at or above 0.9 (1,976) was right;**
- in the 0.8–0.9 band, 157 were right and **8 were wrong**:
  - `build.actions` 0.80–0.85 on decide 9 (`call /system/builder/EmitBuildEvent …`), 7 times: the word "builder" in the path;
  - `output.write` 0.80 on build 5 (`build.load, write to %app%`), once (v3).

So 0.8 would have pre-filled a wrong action. The cut stays at 0.9.

## Round 7 — C + nano, 3 runs, decider v5

Changes (`3820e0ee5`), all with the indentation kept (Ingi):
- the Recovery pre-fill has no nested `?` (`Recovery=[goal.call(…)]`, and an unfilled `…` says so);
- a single unquoted word in a text property is that text (`Name=HandleBuildFailure`).

Raw: `tools/decider/runs/c_eval_round7_20260925_2228{19,35,43}/`. Requests: `/shared/coder/2.0/<goal>/` (run 3) and `rounds/round7-run{1,2,3}/`.

| run | first-attempt right | fixed | caught | silent | after retry right | silent | loud | warnings | seconds | cost |
|---|---|---|---|---|---|---|---|---|---|---|
| 1 | **58/58** | 0 | 0 | 0 | 58/58 | 0 | 0 | 4 | 13.1 | $0.0054 |
| 2 | 57/58 | 0 | 0 | 1 | 57/58 | **1** | 0 | 5 | 11.8 | $0.0054 |
| 3 | 55/58 | 2 | 1 | 0 | 58/58 | 0 | 0 | 5 | 13.8 | $0.0056 |
| **total** | **170/174 (98%)** | 2 | 1 | 1 | **173/174** | **1** | **0** | 14 | | |
| round 6 total | 153/174 | 0 | 21 | 0 | 174/174 | 0 | 0 | 32 | | |

**The first run with every step right on the first attempt: run 1, 58/58.** The first attempt goes from 88% (round 6) to 98%: the start 3/13 recovery misses and weekly_report's nesting are gone. Weekly_report didn't nest in any run, even with the indentation shown.

### Every miss, prompt-first

| run | step | what | cause | proposal |
|---|---|---|---|---|
| 2 | weekly 10 `call SendReport to=…, subject=…, on error call LogFailure` | **silent**: error.handle got `RetryCount=1, Order="GoalFirst"`, an invented retry | prompt: rule 8's example ("call X, then retry N times" → `Order="GoalFirst", RetryCount=N`) sits next to a step that has an on-error but no retry. No check sees an invented number | **reverse number coverage**: every number the answer writes must appear in the step's text (a number is plang's own literal, like a quoted text). It would refuse `RetryCount=1` here and pass every golden answer (all their numbers are in their steps); I'll verify that before building it. `Order="GoalFirst"` is only allowed with RetryCount, so it would go on the retry too |
| 3 | start 8, 9 | fixed: `Overflow="Promote"`, `Precision="Error"` spelled out | model; S1 drops them (the defaults) | — |
| 3 | weekly 8 `render template "report.html", errors=%errors%, …` | caught by variable coverage (`%errors%` missing), right after the per-step retry | model | — |

Warnings: goal.call 0.79–0.89 (build 4/7, start 0, decide 9) and error.throw 0.84–0.89 (checkout 3/5), all on right steps.
