# Plan stage 2 — formal: the notation, its parser and its writer

Branch `builder-formal`, python, no LLM calls. Code: `tools/decider/formal.py`. Check: `tools/decider/formal_check.py`. All 58 golden steps written in formal: `tools/decider/formal_golden.txt` (also at `/shared/coder/llm/plang/builder-formal/formal/formal_golden.txt`).

## 2b — the notation revised (vision.md §4) — current

**Types in the formal.** `Name: type = value`, the same shape as the signatures:
- The writer always writes them.
- They're optional in the LLM's answer and in a `.goal` step; the parser adds them.
- A written type must equal a declared one (`file.read(Path: text = "x")` → "`Path` is path, not text").
- In an open `item` slot, a written type wins (`Value: date = "2026-01-01"`); otherwise the literal's own applies.
- A frozen default is `Name: type ?= value`, a `frozen: true` flag on the row.

**A modifier wraps its action.** `{ }` always holds the actions an action contains: a condition's body, or the one action a modifier wraps. error.handle's recovery is its `Recovery` property. Nesting is the wrap order, kept outermost first in the wrapped action's `modifier` list:

```
[3] error.handle(Recovery: list<action> = [goal.call(Name: text = "HandleBuildFailure")]) {
        goal.call(Name: text = "Compile")
    }
error.handle(Key="A", Recovery=[goal.call(Name="RA")]) { error.handle(Key="B", Recovery=[goal.call(Name="RB")]) { goal.call(Name="X") } }
    → goal.call(X), modifier [A, B]
```

The next-line form is retired. Writing it now fails with the fix: "line 2, column 5: `error.handle` wraps the action it modifies: error.handle(…) { action }".

**Grammar now:**

```
step      = ["[" index "]"] action { ";" action }          whitespace, new lines included, between any tokens
action    = module "." name "(" [ prop { "," prop } ] ")" [ "{" action { ";" action } "}" ]
prop      = Name [ ":" type ] ( "=" | "?=" ) value         type = name [ "<" kind ">" ]
value     = "text" | number | true | false | null | %variable% | [list] | {dict} | action
```

**Results (`python3 formal_check.py`):**

| check | result |
|---|---|
| round trip, **typed** (the writer's form) | **58/58** |
| round trip, **untyped** (the LLM's / a programmer's form; the parser adds the types) | **58/58** |
| a `.goal` step in untyped formal is recognised and parses to the LLM path's rows | **58/58** |
| no natural step text read as formal | **0 of 58** |
| the prompt-C mock `/shared/coder/2.0/checkout/expected.txt` | **10/10** |
| `goal.return(Depth: number ?= 1)` → frozen → written back identical | same |
| two nested error.handles → modifier order [A, B] → written back | same |

**Argument rows carry their types too (architect's ruling):**
- The writer writes `goal.call(Name: text = "EmitBuildEvent", Parameter: list = {kind: text = "build-path", path: item = %path%})`.
- On input the type stays optional (and is then the literal's); a written one wins (`{a: date = "2026-01-01"}`).
- A dict LITERAL keeps untyped entries, and the writer quotes its keys, so the two forms never look alike: `Value: dict = {"name": "a", "n": 5}`.
- The grammar tells them apart: after `key:`, a `type =` makes the entry a typed row.
- A typed entry given to a non-list property is refused: "`Value` takes a value, not argument rows".
- Round trip is still 58/58 typed, 58/58 untyped, and the mock 10/10.

A condition's child text is its body's untyped formal (the open point from stage 2; no ruling yet, so it stands).

The earlier stage-2 sections below describe the first notation (next-line modifiers, untyped writer). They are kept for the record.

## The notation (stage 2, superseded by 2b)

```
[3] goal.call(Name="Compile")
    error.handle(RetryCount=2, Order="GoalFirst") { goal.call(Name="FixProperties") }
[2] condition.if(Left=%itemCount%, Operator="==", Right=0) { output.write(Data="Your cart is empty") }; condition.elseif(Left=%itemCount%, Operator="==", Right=1) { output.write(Data="One item") }; condition.else() { output.write(Data="%itemCount% items") }
[4] goal.call(Name="EmitBuildEvent", Parameter={kind: "build-path", path: %path%})
[3] channel.set(Name="builder", Goal=goal.call(Name="BuilderChannel"))
```

```
step      = ["[" index "]"] line { NEWLINE indent line }
line      = action { ";" action }
              a line holding a modifier wraps the action written before it (its host);
              any other line continues the step's actions
action    = module "." name "(" [ prop { "," prop } ] ")" [ body ]
body      = "{" [ action { ";" action } ] "}"      a condition's body → its child; a modifier's → its recovery
prop      = Name "=" value
value     = "text" | number | true | false | null | %variable% | list | dict | action
list      = "[" [ value { "," value } ] "]"
dict      = "{" [ key ":" value { "," key ":" value } ] "}"      key = name | "text"
```

**One change from the mock** (`/shared/coder/2.0/checkout/system.txt`): the mock writes a modifier after `|` on the same line. vision.md (Ingi) puts it on the next line, under the action it wraps, and that is what the grammar does. To wrap the first of two actions, the writer ends the line at the host and continues the rest on a new line:

```
[i] goal.call(Name="X")
    error.handle() { goal.call(Name="Y") }
    variable.set(Name=%r%, Value=%!data%)
```

The mock's expected answer has no modifiers and parses unchanged (below).

## Types — never from the answer

```python
def typed(declared, value):
    """The row's type: the declared one, or for an open (`item`) slot the literal's own."""
    if declared and declared != 'item':
        m = re.fullmatch(r'(\w+)<(\w+)>', declared)
        return {'name': m.group(1), 'kind': m.group(2)} if m else {'name': declared}
    if isinstance(value, bool): return {'name': 'bool'}
    if isinstance(value, (int, float)): return {'name': 'number'}
    if isinstance(value, list): return {'name': 'list'}
    if isinstance(value, dict): return {'name': 'dict'}
    if value is None: return {'name': 'item'}
    return {'name': 'item'} if VARIABLE.fullmatch(value) else {'name': 'text'}
```

- `Path="orders/…json"` → path; `Operator="=="` → {choice, operator}, checked against the options; `Name=%x%` → variable, which must be a %variable%; `Value=0.24` → number; `Data="Total: %x%"` → text; `Value=%!data%` → item.
- A dict handed to a `list` property is its rows: `Parameter={code: %order.coupon%, order: %order%}` becomes goal.call's two argument rows, each typed the same way.
- The parser reads the handler's declared properties **including the action-typed ones the catalogue drops** (`build_pr.declared(…, held_actions=True)`). So `channel.set(Goal=goal.call(…))` parses. It is v4 item 4's python half.

## Results (`python3 formal_check.py`)

| check | result |
|---|---|
| 1. round trip: each expected answer → rows → **write** → **parse** → rows | **58/58 equal** |
| 2. a `.goal` step written in formal is recognised (`is_formal`) and parses to the rows the LLM path gives (`build_pr.pr_action` over the same answer as JSON) | **58/58** |
| 2b. no natural step text is taken for formal (all 58 golden step texts, e.g. `build.appSave`, `math.subtract A=…, write to …`) | **0 false** |
| 3. the hand-written prompt-C answer `/shared/coder/2.0/checkout/expected.txt` → `parse_answer` → the golden rows | **10/10 steps** |

Parse errors name line, column and what was expected:

```
'file.read(Path="x")\n    variable.set(Name=%y% Value=%!data%)'  → line 2, column 27: expected `,`
'file.read(Pth="x")'                                            → line 1, column 11: `file.read` has no property `Pth` (it has Path, ResolveVariables)
'condition.if(Left=%n%, Operator="less", Right=5)'              → line 1, column 33: `Operator` is one of ==, !=, …; `less` is not
'variable.set(Name="y", Value=1)'                               → line 1, column 19: `Name` names a variable: write it with its % signs
'error.handle() { goal.call(Name="X") }'                        → line 1, column 1: `error.handle` is a modifier: write it on the next line, under the action it wraps
'goal.call(Name="X") { output.write(Data="y") }'                → line 1, column 21: `goal.call` takes no body: only a condition (its child) and a modifier (its recovery) do
'file.read(Path="x"'                                            → line 1, column 19: `file.read(` is not closed: expected `)`
'nope.nothing()'                                                → line 1, column 1: `nope.nothing` is not an action
```

`parse_answer` reports the position in the whole answer, not in the step.

## What doesn't hold — for you

1. **A child's `text` has no place in formal.** The .pr's child is `{text, action}`, and text is the body's words from the step (`write out "Your cart is empty"`). Formal writes only the body's actions, so the parser sets the child's text to the body's formal (`output.write(Data="Your cart is empty")`). The round trip is compared with child text set aside (9 bodies).
   - Options:
     - (a) the child's text **is** its formal (readable, and never wrong);
     - (b) the builder cuts the words from the step text, a heuristic that can be wrong;
     - (c) drop `text` from an inline child.
   - I'd take (a). It changes one thing: Match's "invented child" rule compares a child's text with the indented steps' words. With (a) that compare goes. The rule only matters over an indented body, where fold replaces the child anyway.
2. **A body with several child steps** (`child: [{…}, {…}]`) writes as one `{ a; b }` and reads back as one child. None of the golden has one. The plang .pr only splits children for indented bodies, which fold builds, never from formal.
3. **A dict literal holding a bare %variable%** (Start 2, `{"goal": %goal%, …}`) is not JSON. Formal writes that value as the text the step wrote. `Value={id: "%!trace.id%", goal: %goal%, …}` also parses, as a dict. The golden accepts both.
4. **The `?` in signatures** (Ingi, today): every property that is not required now shows `?`, a defaulted one too: `Depth?: number = 1`, `AsDefault?: bool = false`. This is in `propertiesUserB.template` and `build_pr.signature`, and plang/python parity still holds, 5/5. The mock's system.txt says the same thing ("`?` marks an optional property, `= x` its default").

## Next (queued)

Decider v4 (plang-3b's list):
- the runner-up scored by a yes/no;
- the common actions' example steps beside their question;
- held goal.calls (`set channel … call X`, `…, on error call X`) not counted as picks, with the golden changed and listed;
- action-typed properties in the eval's signatures (`build_pr.declared(held_actions=True)` exists now).
