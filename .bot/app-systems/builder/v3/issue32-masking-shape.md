# Issue 32 — opaque-variable masking — shape (builder → architect, 2026-10-02)

Requested by the architect. Issue 32 is one root in two shapes:
- **(a)** `call goal Page module=%!app.module.condition%` → the **decider** reads `condition` inside
  the variable path as a step word → junk `condition.compare` (measured 1/2 earlier).
- **(b)** `call goal Page module=%!app.module.file%` → the **writer** reads the dotted variable as the
  goal name, `goal.call(Name="%!app.module.file%")`, `Page` lost (measured **5/5** this session; the
  writer receives a clean `=> formal: goal.call(Name)` slot and fills it with the variable anyway).

Both: a variable's **internal name** is read as a word of the step. Fix: the step's variables reach
the decider and the writer as **opaque placeholders**, and the answer maps back.

## What the decider and the writer should see
Each distinct `%…%` in the step becomes `%v1%`, `%v2%`, … in first-appearance order; repeats reuse
their number; **non-variable text is untouched** (that's the point — `Page`, quoted text and numbers
stay). So both models see:
- (a) `call goal Page module=%v1%` — no `condition` to pull in `condition.compare`.
- (b) `call goal Page module=%v1%` — `Page` is unmistakably the name, `%v1%` the argument value.
- issue 2: `foreach %v1% as %v2% with key %v3%, call ShowEntry field=%v3% value=%v2%`.

A placeholder is a plain variable token `%vN%` regardless of the original's shape (a bare `%x%`, a
`%!a.b.c%`, a member path) — the model never needs the real name to map a step to actions. Use a
prefix that cannot collide with a real step variable (e.g. reserve `%v<N>%` and, if the step already
contains a `%vN%`, shift to a guaranteed-free scheme such as `%⁣v<N>%` / a GUID-salted stem —
coder's call).

## Where the masking happens
One masked rendering of the step text, read by **every LLM-facing template**, nowhere else:
- `decider1.template:13` `assign text = s.Text`
- `decider2.template:10` `assign text = s.Text`
- `properties.template:37` `{{ s.Text }}` (the `[i] - <text>` line)
- `decider.state.template` (wherever it prints step text, if any)

All switch from `s.Text` to a new **`s.Masked`**. **Nothing else changes its source:** the pick's own
structural reads (`pick/list/this.cs` `Destination()`, `First()`, `As`, `Assigned()`, `Writes()`,
`Placed()`) keep reading the **real** `_step.Text` — they map structure for the builder, not for the
LLM, and the real variables are what the `.pr` must carry.

> **Member name (architect, confirmed):** the masked text is **`s.Mask.Text`** (a `Mask` member on
> the step exposing `.Text` + the reverse map), not the `s.Masked` / `question.Variable` names this
> draft sketched below. The templates read `s.Mask.Text`.

## Core (coder) — the member and the reverse map
`step` (`PLang/app/goal/step/this.cs`) exposes, driven by the same `type/item/variable/parser` the
pick uses:
- **`Masked`** (string): the step text with each distinct parser variable replaced by `%vN%` in
  first-appearance order.
- **A reverse map** `vN → original variable text` (e.g. `IReadOnlyDictionary<string,string>` or a
  small typed list), owned by the step so the answer reader can restore. (Build it once; a member on
  the step, not a stray helper.)

## How the answer maps back
The LLM answers in masked terms (`goal.call(Name="Page", Parameter=[{module: %v1%}])`). **Before the
formal is parsed into actions**, each `%vN%` in the answer is substituted back to the original
variable via the step's reverse map. The natural seam is the formal reader
(`PLang/app/goal/step/action/formal/Reader.cs`) or `build.match` — restore, then parse/bind, so the
`.pr` carries the real `%!app.module.file%`. Restoring **before** type/variable binding is essential
(a `%vN%` must never reach runtime).

## Interaction with Option v2 (issue 2) — important
Option v2 offers "the step's own variables" for a non-choice option (`Item`/`Key`/`Conversation`).
With masking, those offers must be the **masked** variables (`%v2%`, `%v3%`), and the pick's chosen
value maps back with the same reverse map. So the two shapes must share **one** masking +
reverse-map on the step — build masking first (or together), and have Option v2's offer read the
masked set. Otherwise the decider would be offered real names while seeing masked text.

## Prefill / `=> formal:` consistency
`Prefill` and the Known code (`pick/list`) build the `=> formal:` starting line from real structure
(e.g. `goal.call(Name)`, `loop.foreach(Collection=%person%, Item=%value%)`). Since the writer now
reads **masked** step text, the `=> formal:` line it is handed should be masked too, so the one line
the writer copies and the step text it reads agree. Simplest: render `=> formal:` through the same
mask (the pick holds the map). Flag: decide whether Prefill masks its own output or the template
masks on render — coder + I settle it when the core lands.

## Risk & the control measurement (architect flagged earlier)
Masking hides a variable name that legitimately signals a module (rare, e.g. a step whose only clue
is a variable's name). Before committing: measure 32(a)/(b) and issue 2 **and** a control set where a
variable's name is the only module signal, to confirm masking doesn't regress those. I have the
decider key; I run these once the core exists.

## Split
- **Core (coder, after architect review):** `step.Masked` + reverse map; answer-side restore in the
  formal reader / `build.match`; Option-v2 offers read the masked set.
- **Builder (mine):** the four templates read `s.Masked`; `=> formal:` masking consistency; the
  measurements (32a/b, issue 2, the control).
