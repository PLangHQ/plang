# Formal plang — the grammar of a compiled step

`formal` is the one-line render of what a step compiles to. Every step in a `.pr` carries one:

```
error.throw(Message="boom") @error.handle(Order=GoalFirst) { variable.set(Name=%content%, Value="from-recovery") }
```

It is the human- and LLM-readable face of `step.action[]`. This document defines it.

## Status: today it is a convention, not a language

Stating the gap first, because it shapes everything below.

- **Nothing produces `formal` from the action tree.** The LLM emits it as a free string during
  compile (`os/system/builder/llm/Compile.llm`, "Write `formal` first").
- **Nothing parses it.** `step.Formal` is read off the wire into a `string?` and written back out.
  Grep for consumers: the reader, the writer, and the item door. That is all.
- **Nothing checks it against `action[]`.** The only rule is a prose line in the prompt —
  *"`formal` and `actions[]` MUST match exactly — every `Param=value` in one appears in the other,
  no extras either way"* — enforced by nobody.
- **The grammar is five bullet points in a prompt**, plus per-module notes that restate parts of it
  (`assert/module.notes.md`, `output/write.notes.md` both re-teach "formal mirrors parameters").

So `formal` can drift from the actions it claims to describe, silently, and today sometimes does.
The fix is at the end of this document; the grammar comes first because the fix needs it.

## The grammar

```ebnf
formal      = chain ;

chain       = unit { " | " unit } ;             (* pipe — the left's result is %!data% for the right *)
unit        = call { " @" call } ;              (* a call and the modifiers wrapping it *)

call        = module "." action "(" [ args ] ")" [ body ] ;
body        = " { " chain " } " ;               (* the actions this call owns *)

args        = arg { ", " arg } ;                (* the ONLY use of , *)
arg         = ParamName "=" value ;

value       = string | number | bool | variable | list | object | null ;
string      = '"' … '"' ;                        (* quoted *)
number      = digits [ "." digits ] ;            (* unquoted *)
bool        = "true" | "false" ;                 (* unquoted *)
variable    = "%" name { "." name | "[" index "]" } "%" ;   (* verbatim, unquoted *)
list        = "[" [ value { ", " value } ] "]" ;
object      = "{" [ name ":" value { ", " name ":" value } ] "}" ;
```

`ParamName` is the schema parameter name as declared on the action handler — not an invented alias.

### The one rule that matters

> **`value` never produces a `call`.** An action is not data. It appears only where the grammar
> puts a `call`.

That is the whole answer to "where can an action be inserted".

## Three symbols, three relationships

Each symbol means exactly one thing, and each relationship has exactly one symbol.

| symbol | relationship | slot on the wire |
|---|---|---|
| ` \| ` | **sequence** — runs next; the left's result is the right's `%!data%` | `step.action[]` |
| ` @` | **wrap** — runs the call to its left, inside itself | `action.modifier[]` |
| `{ … }` | **own** — the call runs these when it decides to | the call's body slot |
| `, ` | *(not a relationship)* — next argument, inside parens only | `action.parameter[]` |

Those are the only three positions an action can appear in. There is no fourth position "inside a
parameter value", and the grammar cannot express one.

### Pipe — ` | `

```
file.read(Path="file.txt") | variable.set(Name=%content%, Value=%!data%)
```

Order is execution order, and `%!data%` is the previous call's result — which is true of every
adjacent pair, so the pipe is naming what already happens rather than adding a mechanism. Using it
is optional: `loop.foreach(…) | goal.call(…)` sequences without the body reading `%!data%`.

`,` is deliberately NOT this. It separates arguments and nothing else — one symbol, one job. (It
previously did both, so its meaning depended on whether you were inside parens.)

### Wrap — ` @`

Exactly three actions are modifiers: `error.handle`, `cache.wrap`, `timeout.after`. A modifier
never stands alone and never appears in a pipe; it wraps the call to its left, the way a decorator
wraps the thing under it.

```
http.request(Url=%url%) @timeout.after(Ms=5000) @error.handle(IgnoreError=true)
```

Right-to-left composition: the rightmost modifier is outermost.

A modifier is postfix rather than containing its host (`cache.wrap(…) { llm.query(…) }`) for one
reason: `error.handle` needs a body of its own for its recovery actions, and a call has one body.
Postfix keeps the modifier's braces free for what the modifier itself runs.

### Body — `{ … }`

A call that owns a body runs those actions itself, on its own condition:

```
condition.if(Left=%x%, Operator=>, Right=2) { goal.call(GoalName="DoStuff") }
error.handle(Order=GoalFirst) { variable.set(Name=%failed%, Value=true) }
```

A body is a full `chain`, so it recurses: a body can hold peers, modifiers, and further bodies.

Note what a body does **not** carry: the guarded clause's source text. `formal` renders
`condition.if(…) { goal.call(…) }`, never the child step's `text`. At the `formal` level a body is
an action chain, full stop. (On the wire `child` currently wraps that chain in a step record with a
`text` field; that wrapper has no representation here.)

### Parens vs braces

> **Parens carry parameters — data the call reads.**
> **Braces carry a body — actions the call runs.**

This is the pair of rules to teach, and everything else follows from it.

## What the grammar says about the recovery slot

`error.handle`'s recovery actions are, today, a `[…]` inside a parameter value:

```
error.handle(Actions=[variable.set(Name=%content%, Value="from-recovery")], Order=GoalFirst)
```

The grammar above cannot produce that — `value` does not produce `call`. So either the grammar is
wrong, or the current shape is. It is the current shape: the recovery actions are a body, and
`error.handle` is a call that owns one. Rendering them as a body is the change:

```
error.handle(Order=GoalFirst) { variable.set(Name=%content%, Value="from-recovery") }
```

The runtime consequence is covered elsewhere (a value is lazy — materialised on first touch, long
after load — so an action inside one cannot be born knowing its step, and has to be stamped). The
grammar reaches the same conclusion from the language side alone.

## Migration

The separators change (`,` → ` | ` for sequence, ` | ` → ` @` for wrap). Cost is low precisely
because of the status section above: `formal` is never parsed, so nothing breaks at runtime. What
changes is the prompt (`Compile.llm`'s "Write `formal` first" section and the modifier/peer
section), the per-module notes that quote `formal` examples, and the stored strings — which are
regenerated on the next build anyway.

## Making it real — `formal` derived, not asserted

The grammar is only worth writing down if something enforces it. The order of work:

1. **A renderer.** `formal` is produced FROM `step.action[]` by walking the tree — one method,
   defined by the grammar above. `formal` then has a single source of truth and cannot drift,
   because it is not stored input, it is derived output.
2. **The LLM still writes `formal` first.** That is not redundant — writing the one-liner before the
   JSON is what makes the model commit to a shape. But its string stops being authoritative and
   becomes a *claim*: render `formal` from the emitted `action[]`, compare to the claimed string,
   and a mismatch is a build error the model is asked to fix. The prose rule
   ("MUST match exactly") becomes a mechanical gate.
3. **The per-module notes shrink.** `assert/module.notes.md` and `output/write.notes.md` each
   re-teach "formal mirrors parameters" because nothing checks it. With a check they can drop that
   and keep only what is actually module-specific.
4. **A parser, if ever needed.** Not required for the above — rendering and comparing is enough.
   A parser only becomes interesting if `formal` is ever to be authored by hand.

Step 1 is the one that pays; steps 2–3 fall out of it.

## Open

- **`child`'s step wrapper.** On the wire a condition's body is `[{text, action[]}]` — a step record
  — while `formal` renders only the action chain. Either the `text` matters (and `formal` should
  carry it) or it does not (and the body is an action list, like every other body). Unresolved.
- **The body slot's name on the wire.** `child` for conditions; recovery needs either its own slot
  or to reuse `child`. Open with Ingi.
