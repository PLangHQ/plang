# builder-formal — the decider picks, the LLM fills a formal, two readers must agree

Branch `builder-formal` off `get-builder-running` at `92fcae51f` (prompt B, the 5-goal golden set, the eval harness). With Ingi, 2026-09-25. **Approved by Ingi, all five open questions answered as proposed (see "Open" — now rulings).**

> **Coder, you own this.** The notation, the parser and the prompts below are sketches; the rulings are fixed, the shape is yours. Bring back anything that doesn't hold up when you build it.

## Why

1. **Stage 3 answers in verbose JSON.** Every property row names its type (`{"name":"Path","type":{"name":"path"},"value":"info.txt"}`). nano's answers ran twice mini's length (run 4: median 258 vs 123 tokens), and a whole class of misses was the LLM naming a type wrong (`true`, `null`, `["a","b"]` written as text).
2. **The LLM re-decides what the decider already decided.** Stages 1–2 (the typesafe decider) choose each step's actions; stage 3 then chooses again from the list, and nothing compares the two. mini's silent miss "`write to %n%` lost its variable.set" was a decider-certain action the LLM dropped without anyone noticing.
3. **One reader per step.** When only the LLM reads a step, its slips are silent. Ingi: "then we have double validation, which is very good. two llm are reading over the code."

**The idea (Ingi):** the decider picks the actions, with scores; stage 3 receives each step with its picks as a pre-filled **formal** — a compact call notation (`file.read(Path="info.txt")`) — and answers in formal. The LLM fills the values and must agree with the picks; a disagreement is loud. Formal is shorter, readable at a glance, and the form models know best (a function call). The builder turns the formal into the `.pr`, typing each value from its literal and the property's declared type.

## The shape (python first — that is where the eval runs)

```
stage 1  decider, one request per goal (WINDOW 15 steps, harness.py:190)
         noul per (step, module)                       today: harness.py:239-258
       + noul per (step, common action)                NEW — replaces today's single "@store" question (harness.py:249-252),
                                                       which IS "variable.set?" under another name
         → per step: [file.read 0.99, variable.set 0.99, db.store 0.67]   (scores kept, not only ≥ 0.5)

stage 2  decider, choice per (step, module)            today: harness.py:260-281
         only for a module picked in stage 1 whose action is NOT already a near-certain common-action answer

stage 3  LLM, one request per goal — prompt C = prompt B's shape, plus per step:
           [0] - read 'info.txt', write to %content%
               ← file.read 0.99, variable.set 0.99
               formal: file.read(Path=?); variable.set(Name=?, Value=?)        (the picks, pre-filled; order is the LLM's)
         answer: one formal line per step
           [0] file.read(Path="info.txt"); variable.set(Name=%content%, Value=%!data%)

parse    formal → the .pr action rows (the same shape Apply grafts today)   NEW
check    the LLM's actions vs the decider's picks                            NEW
         a near-certain pick missing, or an action that wasn't picked → refused, back to the LLM (FixProperties-style), with the reason
```

### The formal notation (sketch — coder settles it)

```
[i] action; action; …                                  one line per step, [i] = the step's index
action  = module.name(Prop=value, …)                   only properties the step gives; defaults apply
value   = "text" | 5 | 2.5 | true | false | null | %var% | %!data% | [v, …] | {k: v, …} | action
body    = action { action; … }                         a condition's body = its child
          condition.if(Left=%n%, Operator="<", Right=5) { goal.return() }; condition.else() { output.write(Data="ok") }
modifier= action | error.handle(RetryCount=2, Order="GoalFirst") { recovery actions }
          goal.call(Name="Compile") | error.handle(RetryCount=2, Order="GoalFirst") { goal.call(Name="FixProperties") }
```

- **Types come from the literal and the declared property type**, not from the LLM: `"info.txt"` into `Path: path` is a path (the target owns convert-from); `true` is a bool; `%x%` is a variable reference whose type the property declares.
- **The parser is ours** — a small strict grammar for our notation, not the programmer's language, so it doesn't break "no syntax" (harness README rule 2). A parse failure goes back to the LLM with the position, loudly.
- **Old precedent:** old `.pr` files carried a `formal` field in exactly this spirit (`os/system/builder/.build/build.pr:16`: `"formal": "variable.set(Name=%path%, Value=\"/\", AsDefault=true, Type=string)"`).

## The story behind it

[vision.md](vision.md) — how a goal becomes a program, told with Ingi. It adds to the stages below: modifiers on the next line in formal; the action writes itself in formal and the `.pr` carries both the JSON and the formal line; a `.goal` step written in formal is parsed directly (no decider, no LLM); an unsure step is built with a warning, a certain contradiction fails loudly; the LLM never names types.

## Stages

1. **Decider with common actions and scores** (python). Stage 1 asks modules + the common actions; stage 2 only where needed. Measure the decider alone on the 5 goals: picks vs the golden's expected actions per step (hits, misses, extras, with scores), requests, seconds, cost. Which actions are "common" is counted, not guessed: frequency across the 5 goals, the builder's own goals and `tools/decider/labels/`.
2. **Formal notation + parser** (python). Round-trip test on the golden set: each expected answer → formal → parser → equal to the expected rows. No LLM calls.
3. **Prompt C + the double check** (python). B's shape + the per-step picks and pre-filled formal; the answer in formal; the check. Eval C vs B on the 5 goals, both models, 1 run (Ingi), 2–3 rounds, then pick one model. Numbers: first-attempt, caught, silent, disagreements flagged, latency, cost, tokens in/out.
4. **Into the plang builder** — only after C wins. Direction for the C# side, to design then: formal is a *format of the action* — the action writes itself in formal and reads itself from formal (a reader beside the `.pr` JSON reader), so the parser has an owner and no second structure exists. `Decide.goal` gets the common-action questions; `Properties.goal` asks in formal; Settle's check compares with the picks.

## Rulings (Ingi, 2026-09-25)

1. **Common actions:** start with the top 5–8 by count (likely `variable.set`, `output.write`, `goal.call`, `file.read`, `condition.if`, `list.add`) — "you will learn"; the list is adjusted from measurement.
2. **Near-certain = 0.9** to start (stage 2 skipped at or above it; a missing pick at or above it is refused); set from stage 1's measured calibration.
3. **The LLM sees the scores**, not only the picks.
4. **Decider misses:** picks ≥ 0.9 are pre-filled; 0.5–0.9 are listed as "possible" with their scores; below 0.5 can't be used — the build fails loudly naming the step, and the miss is the decider's to fix (its teaching: `<action>.examples.md` step texts in stages 1/2).
5. **A disagreement** is told back to the LLM (what it dropped or added) and answered again; if it insists, the build fails loudly. A targeted decider tie-breaker is a later refinement.

## Demolition (what must not survive, when C wins)

| Dies | When |
|---|---|
| harness `@store` question as a special case | stage 1 (it becomes the common-action `variable.set` question) |
| stage 3's JSON answer schema (`os/system/builder/llm/Properties.schema`) and the "type per value" rules in the prompt | stage 4 |
| the LLM re-choosing actions freely from the list, unchecked | stage 3 (python) / stage 4 (plang) |

**Stays:** the step-matching check (`goal.step.list.Match`), the chain rule (ElseWithoutIf / BodyBesideCondition / BodyMissing), `build.fold`, prompt B's user shape (goal as written, types, each action once).

## OBP validation

| Surface | Check | Result |
|---|---|---|
| formal (notation) | one word, names the thing | ok — and a format of the action, not a parallel structure (stage 4) |
| the parser | has an owner | stage 4: the action reads itself from formal; python is the stand-in |
| common-action questions | nothing named twice | `@store` folds into `variable.set` |
| the check | one door | the LLM's actions vs the picks, in one place beside the step-matching check |
