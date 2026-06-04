# Formal PLang — the builder's intermediate notation

`formal` is a one-line, canonical render of a step's action chain that the
builder's **compiler** writes *before* it emits the `actions[]` JSON. It is the
syntax the LLM "thinks in" to commit to module/action selection and chain shape;
the JSON then just mirrors that decision. Every step in a `.pr` file carries its
`formal` line, which makes it the most compact human-readable view of what the
builder decided.

This document is reverse-engineered from two sources: the rules in
`os/system/builder/llm/Compile.llm` (the authority on what formal *should* be),
and the ~600 distinct `formal` strings actually present across `Tests/**/.build`
and `os/system/builder/**/.build`. Where the corpus diverges from the rules, that
divergence is called out in [§7](#7-observed-variance--known-rough-edges) — it is
itself a finding about builder reliability.

See also: [`understanding-the-builder.md`](understanding-the-builder.md) (how the
two-phase Plan→Compile pipeline works), [`build_process.md`](build_process.md)
(the full `.pr` format).

> ⚠️ **The `type` slot is mid-migration on this branch (`type-kind-strict`).**
> The type model is becoming `{name, kind, strict}` (e.g. `{name:"number",
> kind:"int"}`), but most `.pr` files in the corpus were built *before* that
> change and still carry the older `{name:"..."}` / `Type=string` forms — they
> have **not** been rebuilt. Treat every `type=`/`Type=`/`type:` you see below as
> **illustrative of formal's *syntax*, not authoritative on the type vocabulary or
> its encoding.** The kind-aware shape is what fresh builds emit; expect the
> `type` examples here to change once the corpus is rebuilt.

---

## 1. Why formal exists

Three jobs:

1. **A think-before-you-serialize scaffold.** Writing the chain on one line forces
   the compiler to commit to *which* actions, in *what* order, with *which*
   parameters — before producing the verbose nested JSON. The JSON can then
   mirror a decision already made, rather than being where the decision happens.
2. **A 1:1 contract with `actions[]`.** `formal` and `actions[]` must describe
   exactly the same chain (see [§6](#6-the-two-hard-rules)). Because they're
   redundant, the validator and a human reader can cross-check one against the
   other.
3. **An audit line.** In a `.pr`, the `formal` field is the at-a-glance answer to
   "what did the builder map this step to?" — far easier to scan than the
   `actions[]` tree.

---

## 2. The shape of one action

```
module.action(ParamName=value, ParamName=value, ...)
```

- **Module + action** are the catalog identifiers (`variable.set`, `goal.call`,
  `output.write`, …). The module is always a single identifier (no dots inside).
- **Parameter names** are the action's real schema names (`Name`, `Value`,
  `Collection`, `GoalName`, `Path`, `Data`, …).
- **Optional parameters the step text didn't supply are omitted entirely** — no
  `Param=null`, no stub. If it's not in the step, it's not in `formal`.

### Values (the right-hand side of `Param=`)

| Kind | Form | Example |
|---|---|---|
| String literal | double-quoted | `Message="boom"` |
| Variable | `%name%`, kept verbatim | `Value=%message%` |
| System variable | `%!...%` | `Value=%!data%`, `Right=%!build.cache%` |
| Number | unquoted | `Right=5`, `Value=3.5` |
| Boolean | unquoted | `Right=true`, `Force=true` |
| List | `[...]` | `Value=[1, 2, 3]` |
| Nested object | `{key:value, ...}` | `GoalName={name:"Greet"}` |
| Inline message list | `[{role:"system", content:"..."}]` | see `llm.query` below |

`%!data%` is special: it means **"the prior action's result"** — the value
flowing out of the previous action in the chain. It is *not* a placeholder for
"unfilled"; if the previous action didn't produce something meant for this slot,
the parameter should be omitted, not set to `%!data%`.

---

## 3. Composing a chain

Three connectors join actions within one step:

### ` | ` — pipe / modifier attachment

Attaches a **modifier** to its host, or pipes the host's result forward:

```
output.write(Data="hi", channel="logger") | error.handle(Actions=[variable.set(Name=%writeFailed%, Value=true)])
error.throw(Message="temporary failure", StatusCode=500) | error.handle(RetryCount=2, IgnoreError=true)
llm.query(...) | cache.wrap(DurationMs=0, Sliding=false) , variable.set(Name=%first%, Value=%!data%)
math.add(A=%total%, B=%item%) | variable.set(Name=%total%, Value=%!data%)
```

The modifier families are `error.handle`, `cache.wrap`, `timeout.after`. In the
JSON these go in the host action's `modifiers` array — **not** as peers in the
top-level `actions`.

### `, ` — peer action

A second independent action in the same step's chain:

```
condition.if(Left=%total%, Operator=">", Right=5), goal.call(GoalName={name:"MarkBig"})
file.read(Path="start-eq.txt") , variable.set(Name=%pr%, Value=%!data%, Type=object)
loop.foreach(Collection=%items%), goal.call(GoalName={name:"AddItem", parameters:[{name:"item", value:%item%, type:"string"}]})
```

### `[...]` — nested action list

Action values that themselves contain actions (`error.handle.Actions`,
`goal.call.GoalName.parameters`) use bracketed lists with the same sub-syntax:

```
goal.call(GoalName={name:"ChainOuter"}) | error.handle(Actions=[variable.set(Name=%caught%, Value=true)])
error.throw(...) | error.handle(Actions=[goal.call(GoalName={name:"HandleRetryError"})])
```

---

## 4. Pattern catalog (real `.pr` examples)

### Variable assignment
```
variable.set(Name=%greeting%, Value="hello", Type=string)
variable.set(Name=%items%, Value=[1, 2, 3], Type=json)
```

### Capture a result — `write to %x%` becomes a peer `variable.set`
```
file.read(Path="config.json") , variable.set(Name=%pr%, Value=%!data%, Type=object)
math.add(A=%x%, B=%z%) | variable.set(Name=%b%, Value=%!data%)
```
(`write to` is a *peer* per the rules — see [§7](#7-observed-variance--known-rough-edges) on the `|` vs `,` inconsistency.)

### Output
```
output.write(Data=%message%)
output.write(Data="hello from channel accessor", channel="log")
```

### Conditionals — `if` / `else` / `elseif` are each their own action
```
condition.if(Left=%flag%, Operator="==", Right=true) , condition.else() , goal.call(GoalName={name:"HandleTrue"}) , goal.call(GoalName={name:"HandleFalse"})
condition.if(Left=%x%, Operator=">", Right=10) , condition.elseif(Left=%x%, Operator=">", Right=5) , variable.set(Name=%c%, Value=3, Type="int")
```

### foreach + call (the canonical loop shape)
```
loop.foreach(Collection=%items%), goal.call(GoalName={name:"AddItem", parameters:[{name:"item", value:%item%, type:"string"}]})
loop.foreach(Collection=%dict%), goal.call(GoalName={name:"ProcessEntry", parameters:[{name:"item", value:%entry%, type:"string"}, {name:"key", value:%key%, type:"string"}]})
```
The named args (`item=%item%`) live **inside** `GoalName.parameters`, never as
top-level `goal.call` params.

### Goal call with error recovery
```
goal.call(GoalName={name:"Recurse"}) | error.handle(Actions=[goal.call(GoalName={name:"HandleRecursionError"})])
error.throw(Message="boom", StatusCode=500) | error.handle(Actions=[variable.set(Name=%recovered%, Value=true)])
```

### Error-handle knobs (no recovery verb)
```
identity.archive(Name="roundtripSigner", Force=true) | error.handle(IgnoreError=true)
error.throw(Message="temporary failure", StatusCode=500) | error.handle(RetryCount=2, IgnoreError=true)
```

### Cache wrap
```
llm.query(Messages=[...], Cache=true) | cache.wrap(DurationMs=0, Sliding=false) , variable.set(Name=%second%, Value=%!data%)
```

### LLM query with an inline message list
```
llm.query(Messages=[{role:"system", content:"You are a helpful assistant"},{role:"user", content:"My favorite color is blue."}]) | variable.set(Name=%first%, Value=%!data%)
llm.query(Messages=[{role:"system",content:"..."},{role:"user",content:"What is my favorite color?"}], ContinuePreviousConversation=true) | variable.set(Name=%second%, Value=%!data%)
```

### Assertions
```
assert.equals(Expected=4.5, Actual=%b%)
assert.equals(Expected="big-and-done", Actual=%label%)
assert.isNotNull(Value=%result%)
assert.isTrue(Value=%flag%)
```

---

## 5. An informal grammar

```
formal      := chain
chain       := segment ( (" | " | " , ") segment )*
segment     := module "." action "(" params? ")"
params      := param ( ", " param )*
param       := Name "=" value | value
value       := text | number | bool | list | object | segment | ...
text        := '"' chars '"'
list        := "[" (value ("," value)*)? "]"
object      := "{" (key ":" value ("," key ":" value)*)? "}"
```

Notes:
- The `value` alternatives above are **not a closed set** — `text`, `number`,
  `bool`, `list`, `object` are the common primitives, but the full value
  vocabulary comes from the **PLang type system (the types list)**, and more are
  added there as needed. The grammar's job is the *structure* (how values are
  written and composed); *which* types exist is owned by the type list (currently
  migrating to `{name, kind, strict}` — see the warning at the top).
- A **variable reference** (`%name%`, member access `%name.x%`, or system
  `%!data%`) appears anywhere a value does, written verbatim and resolved at
  runtime. It is not a distinct grammar atom — it's just a value token.
- A **nested action** is a value: `error.handle(Actions=[goal.call(...)])` is a
  `list` whose elements are `segment`s. There is no separate "action-list"
  production — a list of actions is just a `list` of `value`s.
- Modifiers (`error.handle`, `cache.wrap`, `timeout.after`) are segments
  introduced by ` | `; peers are segments introduced by ` , `.

---

## 6. The two hard rules

From `Compile.llm`, non-negotiable:

1. **`formal` must mirror `actions[]` exactly.** Every `Param=value` in the formal
   line corresponds to a real entry in that action's `parameters` array, and every
   emitted parameter appears in formal. No extras in either direction. An optional
   parameter the step text didn't name is omitted from **both**.
2. **If you can't write a coherent `formal` line, the action set is wrong.** Don't
   guess a partial chain — emit the `missing-actions` error so the planner expands
   the set. A formal you can't complete is the signal that the planner under-picked.

---

## 7. Observed variance — known rough edges

The corpus is *not* perfectly consistent. These variations appear across real
`.pr` files and are worth knowing — both to read existing files and because the
inconsistency is part of why self-rebuild is non-deterministic (the builder's own
reliability problem). They are reported here, not endorsed.

- **`|` vs `,` for a captured result.** `write to %x%` is documented as a *peer*
  (`,`), and `file.read(...) , variable.set(...)` follows that — but
  `math.add(...) | variable.set(...)` uses the pipe for the same capture. The
  separator drifts between steps.
- **Value quoting of variables.** Both `value:%item%` and `value:"%item%"` occur
  inside `parameters` lists for the same construct.
- **`type` key casing/quoting — and the slot itself is changing.** `Type=string`,
  `type=string`, `Type="int"`, `type:"string"`, `type=object` all appear —
  different case, different quoting, different key (`Type=` vs `type:`). On top of
  that string-level drift, the **type model itself changed on this branch** to
  `{name, kind, strict}` and the corpus mostly predates the rebuild (see the
  warning at the top). So the `type` slot has two layers of churn right now —
  don't treat any single form as the standard until the corpus is rebuilt and the
  kind-aware shape is the only one present.
- **Whitespace around connectors.** `...) , goal.call` vs `...), goal.call`.
- **`goal.call` shorthand.** Mostly `goal.call(GoalName={name:"X"})`, but the
  short `goal.call(HandleRetryError)` and `goal.call({name:"X"})` also occur.
- **`Actor=%!data%` artifact.** Some `goal.call`s carry `Actor=%!data%` — a known
  LLM mis-binding (it parks the foreach item or prior result in the `Actor` slot).
  `Actor` should be **omitted** unless the step text explicitly names a
  cross-actor delegation; see `os/system/modules/goal/call.notes.md`.

When tightening builder prompts or the validator, this list is the menu of
compound-value ambiguities to nail down: pin the separator, the quoting, and the
`type` spelling so the LLM stops extrapolating one example's convention onto
another.

---

## 8. How to read formal in practice

- Pull every formal line from the corpus:
  ```bash
  python3 -c "import json,glob;[print(s.get('formal')) for fp in glob.glob('Tests/**/.build/*.pr',recursive=True) for d in [json.load(open(fp))] for s in d.get('steps',[]) if s.get('formal')]"
  ```
- Read it top-to-bottom: leading segment is the main action; ` | ` hangs a
  modifier; ` , ` adds a peer; `[...]` opens a nested action (recovery body, call
  parameters).
- Cross-check against `actions[]` in the same step — they must say the same thing.
