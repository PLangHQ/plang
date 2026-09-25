# The story — how a goal becomes a program

With Ingi, 2026-09-25. Ingi told the start of it and handed the rest over ("I want you to figure the rest out"). What he said is marked **(Ingi)**; the rest is my design, marked **(architect)** — his to overrule.

## 1. The programmer writes intent

A programmer — someone who knows the basic syntax and reads the docs **(Ingi)** — writes a goal:

```
Checkout
- read 'orders/%orderId%.json', write to %order%
- if %order.email% does not contain "@", throw "Invalid email address"
- math.multiply A=%order.total%, B=%vat%, write to %vatAmount%
```

Each step is intent, in any human language — Icelandic as well as English **(Ingi)**. The programmer can be exact whenever they want, down to naming the action themselves (`math.multiply A=…`).

## 2. Every step becomes a list of module.action(parameters)

That is the whole contract **(Ingi)**: a step is a **list** of actions, each `{module, action, parameters}`. `write to %x%` is its own action, `variable.set` **(Ingi)**. Whatever language the step is in, the result is always `module.action(parameters)` **(Ingi)** — written in **formal**:

```
[0] file.read(Path="orders/%orderId%.json"); variable.set(Name=%order%, Value=%!data%)
[1] condition.if(Left=%order.email%, Operator="notcontains", Right="@") { error.throw(Message="Invalid email address") }
[2] math.multiply(A=%order.total%, B=%vat%); variable.set(Name=%vatAmount%, Value=%!data%)
```

A modifier (`on error …`, `cache …`, `timeout …`) wraps the action it applies to at run time, like try **(Ingi)**. In formal it **follows that action, in the step's written order** **(Ingi, 2026-09-25)**, and its recovery is a property:

```
[3] goal.call(Name="Compile"); error.handle(RetryCount=2, Order="GoalFirst", Recovery=[goal.call(Name="FixProperties")])
```

The parser attaches it, not the LLM's layout: an action declared `[Modifier]` modifies the action before it. With several, the first written is the innermost (`goal.call; timeout.after; error.handle`: the handler also catches the timeout). A modifier with no action before it is a parse error. Indentation in the answer is allowed for reading and ignored.

## 3. The builder: two readers who must agree **(Ingi: "double validation … two llm are reading over the code")**

For each goal, in one pass over the whole goal (the steps refer to each other):

1. **The decider** (typesafe) reads every step and picks its actions, with a score — the main module by one choice, the common actions (`variable.set`, `goal.call`, `output.write`, `error.handle`, `condition.if`, `file.read`) by yes/no, then the main module's action.
2. **The LLM** gets the goal as written, each step with the decider's picks and scores pre-filled in formal, and each action's definition once. It answers in formal: it fills the values, orders the actions, and builds the `{ }` bodies.
3. **The builder parses the formal** (our notation, a strict grammar) and **checks** it:
   - one entry per step, matching the `.goal` step for step;
   - the condition chain (a body inside its condition; only elseif/else after it);
   - the LLM agrees with the decider — a pick the decider is certain of (≥ 0.9) is present, and nothing unpicked appears;
   - each operator's operand is valid (`is` takes a type).
   A failed check goes back to the LLM with the reason; the LLM answers again.

**When they are unsure** — the step was built from a pick the decider was only 0.5–0.9 about, or the LLM and the decider disagreed and the retry settled it — the step is built with the likelier reading and **carries a warning** the programmer sees **(Ingi)**. **When they contradict** — the decider is certain and the LLM, after the retry, still leaves the action out — the build of that goal fails loudly, naming the step **(architect; ruling 5 of the plan)**: certainty against certainty is not something to guess.

**Indentation is the parser's**, never the LLM's: an indented step is the body of the condition above it, placed by the builder from the layout **(Ingi, earlier)**.

## 4. The `.pr` holds each step's code, in JSON

**Ingi, 2026-09-25:** "I want the builder to save the full execution path (json) into the pr file, under code"; on each step: "this is what c# code does".

- The `.pr` is JSON: the goal's facts (name, path, hash, …) and its steps. **Each step holds its own `code`**: its actions as the JSON tree the action writes itself (`goal/step/action/this.Item.cs`). That covers module, name, typed property rows, frozen `default`, `modifier` (inside the action it modifies), a condition's body steps under `child`, and error.handle's `recovery`. The step's member is `step.Code`.
- **Formal is not stored.** It is the LLM's answer notation and a view: the builder parses the answer into actions, and the action renders itself in formal when a prompt or a person needs it.
- Read top to bottom, the program is the object walk: each step in order, then its `step.Code`.
- A key the reader doesn't know means another builder made the file: it is refused as `PrFormatOutdated`, naming the key.

```json
{"name": "Checkout", "path": "/Checkout.goal", "hash": "…",
 "step": [{"index": 0, "text": "read 'orders/%orderId%.json', write to %order%", "lineNumber": 4,
           "code": [{"module": "file", "name": "read",
                     "property": [{"name": "Path", "type": {"name": "path"}, "value": "orders/%orderId%.json"}]},
                    {"module": "variable", "name": "set",
                     "property": [{"name": "Name", "type": {"name": "variable"}, "value": "order"},
                                  {"name": "Value", "type": {"name": "item"}, "value": "%!data%"}]}]}]}
```

- **Types in formal**, when it is rendered: `Name: type = value`, and `Name: type ?= value` for a frozen default. The LLM's answer and a formal step in a `.goal` leave types out, and the parser adds them from the property's declared type and the literal.

## 4b. What the LLM is shown and answers (settled, architect)

- Each step lists the decider's picks **≥ 0.5 with their scores**. Picks **≥ 0.9 are pre-filled** in the formal line; 0.5–0.9 are listed as "possible" (the band holds most `write to` → `variable.set`, all right — hiding it would make every such step fail). Below 0.5 is not shown.
- The pre-filled line uses `?` for a value to fill (loud if left unfilled — `%Prop%` is real variable syntax and would parse silently) and **pre-fills what is already known**: `write to %x%` → `variable.set(Name=%x%, Value=%!data%)`.
- The LLM answers in formal **without types**; the parser types every value.

## 5. When plang gets a step wrong **(architect)**

The `.goal` stays the only source of truth; the `.pr` is never hand-edited (it is regenerated, and a hand edit would be overwritten or go stale). The programmer fixes a step in the `.goal`:

- reword it, more exactly — or
- **write the step in formal**:
  ```
  - file.read(Path="orders/%orderId%.json"); variable.set(Name=%order%, Value=%!data%)
  ```
  A step written in formal is **parsed directly — no decider, no LLM** — and taken as written. This is v0.1's rule, generalised: "A module the developer named is not a decision, so it is not worth asking about." It is also the escape hatch that always works.

## 6. Rebuilding

An unchanged step keeps its `.pr` actions (the builder already merges the previous `.pr`). A changed step is rebuilt with the whole goal as its context. Long goals may later go to the LLM in parallel parts **(Ingi: after the builder works)**.

## 7. Running

The runtime runs the JSON actions, step by step. The formal is for reading, and for writing a step exactly.

---

## What this adds to the plan

| | where |
|---|---|
| A modifier follows its action in formal; the parser attaches it by `[Modifier]`; recovery as a `Recovery=[…]` property | stage 4 (after 4b) |
| Types in the formal (`Name: type = value`, `?=` frozen default), written always, optional on input | plan stage 2b (writer + parser) |
| The `.pr` holds each step's `code` (JSON); formal is not stored | stage 4b |
| Picks ≥ 0.5 shown with scores, ≥ 0.9 pre-filled, `?` for unfilled, known values pre-filled | plan stage 3 (prompt C) |
| A `.goal` step written in formal is parsed directly, no decider/LLM | plan stage 2 (the parser), stage 4 (the builder) |
| Unsure → build + warning on the step; certain contradiction → loud failure | plan stage 3 (the check) |
| The LLM never names types | plan stage 3 (prompt C) |
