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
