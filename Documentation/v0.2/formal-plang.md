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

value       = item ;                             (* every value IS an item — that is the base type *)
item        = text | number | bool | null | variable | call | list | dict ;
                                              (* a call here is an EXPRESSION — see below *)
text        = '"' … '"' ;                        (* quoted *)
number      = digits [ "." digits ] ;            (* unquoted *)
bool        = "true" | "false" ;                 (* unquoted *)
null        = "null" ;
variable    = "%" name { "." name | "[" index "]" } "%" ;   (* verbatim, unquoted *)
list        = "[" [ item { ", " item } ] "]" ;
dict        = "{" [ name ":" item { ", " name ":" item } ] "}" ;
```

`ParamName` is the schema parameter name as declared on the action handler — not an invented alias.

The value names are plang's own type names — `text`, `number`, `bool`, `null`, `list`, `dict` — and
`item` is the base every value is one of (`app.type.item.@this`). **`string` and `object` are not
plang words** and must not appear in `formal` or in a `type` slot. `text` is the type; `item` is the
base. The grammar above enumerates only the literal forms; a value can carry any registered item
type (`path`, `date`, `duration`, `url`, `image`, `guid`, …) — those arrive typed on the wire rather
than through a distinct literal syntax.

> **Leak, already on the wire.** `type/list/this.cs` answers `"object"` as a fallback whenever it
> cannot name a CLR type (`if (type == null) return "object"`, `if (type == typeof(data.@this))
> return "object"`). That fallback has reached disk: `"name": "object"` appears **353** times across
> the `.pr` files, as often as `"name": "text"` (350), plus 82 `Type=object` inside `formal`
> strings. It is not an edge case — it is roughly half the type annotations in the corpus, and every
> one of them is a value whose real type was not determined.

### The one rule that matters

> **Every call is CONSTRUCTED at load. Only its EVALUATION is deferred.**

That is the whole answer to "where can an action be inserted": anywhere the grammar puts a `call`,
including inside a value — what may never happen is building the action lazily, at the moment it is
first touched. A `%var%` is the model: constructed at load (a Data holding a name), evaluated on
read. A nested call is the same — a real action, born holding its step, run when its slot is read.

Confusing the two is the live bug in `error.handle`: its recovery actions are constructed lazily,
so nobody holds a step when they appear, so a step has to be stamped on afterwards.

## Four relationships between calls

Each has one symbol, and each symbol means one thing.

| symbol | relationship | when the nested call runs | slot on the wire |
|---|---|---|---|
| ` \| ` | **pipe** — runs next; the left's result is the right's `%!data%` | in sequence | `step.action[]` |
| ` @` | **wrap** — runs the call to its left, inside itself | around its host | `action.modifier[]` |
| `{ … }` | **body** — the call runs these when it decides to | when the owner decides | the call's body slot |
| `P=call(…)` | **expression** — its result IS the parameter's value | when the owner reads `P` | `action.parameter[]` |

A **reference** is not on this list and is not a call: `channel.set(Name="audit", Goal=…)` stores a
goal to be invoked later on an event. It is neither evaluated on read nor run by its owner, so it
rides as a value of the `goal.call` TYPE (`{name:"AuditLog"}`), never as a nested call.

Picking the wrong relationship is the failure mode. `error.handle`'s recovery is a **body** — it
runs when the handler decides, not when a parameter is read — but it is currently written as an
expression, which is why it needs a stamp.

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

### Expression — `P=call(…)`

A call in a value slot produces that parameter's value when the owner reads it:

```
condition.if(Left=file.exists(Path="file.txt"), Operator="isTrue") { goal.call(GoalName="xxx") }
```

The flat equivalent already works today and is the same computation written left to right:

```
file.exists(Path="file.txt") | condition.if(Left=%!data%, Operator="isTrue") { goal.call(GoalName="xxx") }
```

They are genuinely different trees — one action with a sub-expression, versus two piped calls — so
both are legal programs and `formal` renders whichever the builder produced. Which one the builder
should PREFER for a given step text is a teaching decision, still open.

**Not implemented.** The grammar admits expressions; the runtime does not yet evaluate one. Reading
a parameter would have to run an action and take its result. Sized separately — this is a feature,
not a correction.

### Parens vs braces

> **Parens carry values the call reads** — a literal, a `%var%`, or an expression evaluated on read.
> **Braces carry a body the call runs** — actions, on the call's own condition.

Both hold calls. The difference is *who decides when it runs*: for an expression, whoever reads the
parameter; for a body, the owning call.

## What the grammar says about the recovery slot

`error.handle`'s recovery actions are, today, a `[…]` inside a parameter value:

```
error.handle(Actions=[variable.set(Name=%content%, Value="from-recovery")], Order=GoalFirst)
```

The grammar CAN produce that — a value may hold a call. What it cannot produce is that *meaning*:
an expression is evaluated when its parameter is read, and nobody reads `Actions` to obtain a
value. The handler runs those actions on its own condition, which is a body.

```
error.handle(Order=GoalFirst) { variable.set(Name=%content%, Value="from-recovery") }
```

The runtime says the same thing from the other side: recovery actions are built lazily today, so
nothing holds a step when they appear, and `handle.cs` has to stamp one on — the last stamp left on
the graph. Both readings land on the same fix, which is a good sign the category is the real error.

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
