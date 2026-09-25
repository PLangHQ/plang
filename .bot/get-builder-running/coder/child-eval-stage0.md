# Child eval — Stage 0: judge the stage-3 prompts before any LLM call

2026-09-25, get-builder-running. No LLM call was made. The prompts are rendered by `build_pr.user_message()`
(render script: stage-0 only, not committed), with hand-written menus (the eval is stage 3 only).

**Ruling being evaluated (Ingi, option a):** the LLM puts a condition's body in the condition's `child`.
`if X, return` → `condition.if {child: [{text, action: [goal.return]}]}`, with the branch text written by
the LLM. `build.fold` keeps folding indented steps only.

## 1. What the model gets

**System:** `os/system/builder/llm/Properties.llm` verbatim (126 lines, at `c32ac59bd`). The python
harness reads it raw; the plang builder renders it as a template, and it has no template tags, so both
send the same text.

**User (python `user_message`), four cases:**

```
MenuModule

step 0: set %key% = "s%step.Index%_%module.Name%"
   menu:
     variable.set
        Name (variable, required)
        Value (item, required)
        Type (item, optional)
        AsDefault (bool, optional)

step 1: if %moduleAnswer[key].noul% is less than %threshold%, return
   menu:
     condition.if
        Left (item, optional)
        Operator (choice<operator>: ==, !=, >, <, >=, <=, contains, startswith, endswith, in, isempty, is, and, or, required)
        Right (item, optional)
        Negate (bool, optional)
     goal.return
        Data (item, optional)
        Depth (number, optional)

step 2: if %module.Action.Count% is 1, add "%module.Name%.%module.Action[0].Name%" to %choices%
   menu:
     condition.if
        (same four rows)
     list.add
        ListName (variable, required)
        Value (item, required)
        AtIndex (number, optional)

step 3: if %module.Action.Count% is more than 1, add "%module.Name%.%actionAnswer[key].choice%" to %choices%
   menu: (same as step 2)
```

```
Route

step 0: if %age% >= 18 call Adult, else call Minor
   menu:
     condition.if
        Left (item, optional)
        Operator (choice<operator>: ==, !=, >, <, >=, <=, contains, startswith, endswith, in, isempty, is, and, or, required)
        Right (item, optional)
        Negate (bool, optional)
     condition.else
     goal.call
        Name (text, required)
        Parameter (list, optional)
        Actor (choice<actor>: system, user, optional)
        Parallel (bool, optional)
```

```
Grade

step 0: if %score% > 90, write out "A", else if %score% > 70, write out "B", else write out "C"
   menu:
     condition.if      (four rows as above)
     condition.elseif  (four rows as above)
     condition.else
     output.write
        Data (item, required)
```

```
ReadIfThere

step 0: if %file% exists
   menu:
     condition.if      (four rows as above)

step 1: read %file%, write to %content%          ← indented under step 0 in the source; not shown
   menu:
     file.read
        Path (item, required)                    ← wrong type, see P9
        ResolveVariables (bool, optional)
     variable.set  (four rows as above)
```

**The plang builder's `propertiesUser.template`, hand-rendered (~, not run) for MenuModule step 1:**

```
step 1: if %moduleAnswer[key].noul% is less than %threshold%, return
   menu:
     condition.if
        Left (item, optional)
        Operator (choice<operator>: one of ==, !=, >, <, >=, <=, contains, startswith, endswith, in, isempty, is, and, or, required)
        Right (item, optional)
        Negate (bool, required)             ← [Default(false)]: `{% if p.Default %}` is falsy for false
     goal.return
        Data (item, optional)
        Depth (number, required, default 1)
```

## 2. Problems, read as the model reads them

### Condition teaching: the core of this eval

| # | Problem | Evidence | Proposed fix (Stage 1) |
|---|---|---|---|
| P1 | **`Properties.llm` never mentions conditions.** Nothing about `child`, the if/elseif/else chain, or where a body goes. A model given `if X, return` with `condition.if` + `goal.return` on the menu can only emit them flat, and a flat sibling runs when the condition is FALSE. | Properties.llm:1-126 | A "Conditions" section: the body goes in the condition's `child` as `[{text, action}]`; `elseif`/`else` follow the `if` as siblings in the step's own list, each with its own `child`. |
| P2 | **The schema has no `child`.** The plang Schema (`Properties.goal:16`) cannot carry one; python sends `json_object` with no schema. | Properties.goal:16, build_pr.py:177 | Add `child?: list<{text: string, action: list<…>}>` to the action shape. It is recursive; see Q2. |
| P3 | **"each with its actions in the order they run"** (Properties.llm:11) is wrong for a chain: `else` runs *instead of* `if`, not after it. | Properties.llm:11 | "…in the order they are written; a condition's body runs only when it holds". |
| P4 | **Nothing says what may follow a condition in the step.** At runtime every action after an `if` in the same step is the else-chain (description.md; if.notes "Multiple top-level condition.if … if/elseif/else chain"). A trailing `write to %x%` beside the if would run only when the condition is false. | if.notes.md, description.md | Rule: after a condition only `elseif`/`else` may follow at step level; everything the branch does goes in `child`. A setup action BEFORE the `if` stays at step level. |
| P5 | **A condition with no body in its own text** (`if %file% exists` over an indented step) must get NO child; the body comes from the steps below via fold. Nothing says so. Positively, the model can't see indentation (neither user message renders it), so it cannot nest the indented step by mistake. | user_message, propertiesUser.template | One line: "a condition whose step names no action gets no `child` — its body is the indented steps below, placed by the builder". |
| P6 | **No worked example of a condition at all**, and none of `goal.return`. | Properties.llm | One example for `if X, return`, one for `if/else call`; see §3. |

### Teaching elsewhere that contradicts the ruling

These files are **not in the stage-3 prompt today**: neither the plang template nor python sends action notes or examples (#22). So they cannot affect this eval, but they will once #22 lands.

| # | Problem | Fix |
|---|---|---|
| P7 | `if.examples.md:2` "the call is its own action"; `elseif.examples.md:2` "the write is its own action". Both read as siblings. | "the call is the body: the if's `child`". |
| P8 | `if.notes.md` compound example: flat `…, condition.if(…), goal.call(GoalName={name:"DoThing"})`. It is flat, and `GoalName` is an old property name (now `Name`). Its operator list omits `is`, which exists (Operator.cs:42, the IS-A type check). | Rewrite with the call in `child` and `Name`; add `is`. |

### Python `user_message()` vs the plang `propertiesUser.template` (the plang one ships)

| # | Difference | python | plang template | Fix |
|---|---|---|---|---|
| P9 | Alias-typed slots | `data.@this<path>` → **`item`** (regex only knows `app.type.item.X`; build_pr.py:58) | `path` (reflected) | Map bare aliases in `plang_type`, or read the type the way the catalog does. Properties.llm's own worked example says `Path (path, required)`, so python contradicts the system prompt. |
| P10 | "optional" | nullable **or** has `[Default]` | **nullable only** (`property/this.cs:30-33`) → `Negate (bool, required)`. The default doesn't even show: `{% if p.Default %}` is falsy for `false` (Depth's `default 1` does show). | Decide the truth (Q3). In plang, "required" + Properties.llm "a required property the step does not write is yours to work out" means: expect `Negate` filled on every if. The template's default test should be "has a default", not "default is truthy". |
| P11 | Defaults | not rendered | `, default X` | python renders `[Default(x)]`. Properties.llm:35 shows `optional, default false`. |
| P12 | Choice options | `choice<operator>: ==, …` | `choice<operator>: one of ==, …` | Align with Properties.llm:123, which teaches the python form. |
| P13 | `[modifier]` tag | from `[Modifier(` in the source | never (modifiers never reach the plang menu, finding D) | Out of scope here; held with D. |
| P14 | `SHAPES` (`= {"module": "goal", …}` after action-typed slots) | yes | no | Drop from python (Properties.llm has a worked example for `action` slots), or add to the template. For the eval, match plang. |
| P15 | Menu order | the menu's order | catalog order (module list, then its actions) | harmless; noted. |

For the eval, python must render what plang renders, or the eval measures a prompt that doesn't ship. Proposal: fix P9, P11, P12 and P14 in `user_message()`, and render P10 the way we decide in Q3.

### Properties.goal:14: prose pasted into a JSON literal?

**It renders valid JSON.** A `set %x% = [ {…} ]` literal is stored **structurally**: the `.pr` holds a list of dicts whose string holds `"%propertiesSystemMsg%"`, not text to be re-parsed. The live `start.pr` step 2 stores `set %trace% = {…}` as `{"name":"Value","type":{"name":"dict"},"value":{"id":"%!trace.id%", …}}`. Substitution happens per string at runtime, so quotes and newlines in the prompt can't break it.

**The real risk is different:** six Generator baseline reds say a `%var%` nested inside a list/dict parameter stays literal. For example, `DataWrappedList_NestedVarInDict_DeepResolvesAndTypes` expected "you are a compiler" and got `"%comment%"`, which is this exact shape. If `variable.set`'s `Value` takes that path, the model receives the text `%propertiesSystemMsg%` as its system prompt. Not verified (~). It belongs to the builder-run work, not this eval (python sends the prompt directly).

### Smaller

| # | Problem |
|---|---|
| P16 | `if %file% exists`: no operator means "exists". Path truthiness is `IBooleanResolvable`, but `Operator` is required and if.notes has no row for "exists" / "is set". I'll avoid it in the golden set unless you want it taught (Q4). |
| P17 | `goal.return` `Depth (number, …, default 1)`. A model may write `Depth: 1`, which the "never repeat a default" rule forbids; fine once P10/P11 render the default. |

## 3. Proposed teaching (the Stage 1 draft direction, for your review)

```
Conditions. A condition (`condition.if`, `condition.elseif`, `condition.else`) guards a body: the
actions that run only when it holds. The body goes in the condition's `child` — a list of steps, each
{"text": <the part of the step it does>, "action": [...]}. Nothing but `elseif`/`else` may follow a
condition in the step's own list: an elseif/else is the next branch of the same chain, with its own
`child`. An action the step does BEFORE testing stays in the step's list, ahead of the `if`.
A condition whose step names no action of its own gets no `child` — its body is written as indented
steps below it, and the builder places those.

  step 1: if %n% is less than 5, return
  {"index": 1, "action": [
    {"module": "condition", "name": "if", "property": [
       {"name": "Left", "type": {"name": "item"}, "value": "%n%"},
       {"name": "Operator", "type": {"name": "choice", "kind": "operator"}, "value": "<"},
       {"name": "Right", "type": {"name": "number"}, "value": 5}],
     "child": [{"text": "return", "action": [{"module": "goal", "name": "return"}]}]}
  ]}

  step 2: if %age% >= 18 call Adult, else call Minor
  → [if{Left,Operator,Right, child:[{text:"call Adult", action:[goal.call Name=Adult]}]},
     else{child:[{text:"call Minor", action:[goal.call Name=Minor]}]}]
```

## 4. Open questions

1. **Branch `text`:** the words of the branch as written in the step ("return", "call Adult"), or a normalised sentence? The golden set needs one answer to judge against. I propose the verbatim fragment.
2. **Nesting depth:** the plang Schema type literal can't be recursive. One level of `child` (an action in a child with its own child) or two? For `if A, if B, call X`, one level is not enough. I propose two levels in the schema, with deeper nesting left to indentation.
3. **P10 "optional":** should a non-nullable `[Default]` slot render as optional? The catalog (`property.Nullable`) says required; the handler says "has a default". I propose the template renders `optional, default X` whenever a default exists.
4. **Truthiness conditions** (`if %file% exists`, `if %flag%`): which operator should the LLM use? Or keep them out of the golden set for now?
5. **The eval harness:** send the plang Schema as an OpenAI JSON schema (`response_format: json_schema`) so python measures what plang sends, or keep `json_object`? I don't yet know how `OpenAi.cs` turns the plang Schema into the request (~); I'll read it in Stage 1 unless you say otherwise.
