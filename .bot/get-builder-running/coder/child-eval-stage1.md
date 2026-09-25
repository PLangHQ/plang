# Child eval — Stage 1: the draft change and the golden set (no LLM call)

2026-09-25, get-builder-running. The rulings behind it: branch text is the words as written; two levels of `child`; a slot with a default is `optional, default X`; no truthiness cases; python sends what plang sends.

## What changed

| File | Change |
|---|---|
| `os/system/builder/llm/Properties.llm` | Line 11 no longer says "in the order they run" (P3). New **Conditions** section with two worked examples (P1, P4, P5, P6): the body goes in `child` as `[{text, action}]`, never beside the condition; only `elseif`/`else` may follow a condition in the step; a setup action stays ahead of the `if`; a condition that names nothing gets no child. Choice wording is now `one of` (P12). |
| `os/system/builder/llm/Properties.schema` (new, per Ingi) | The answer shape with named parts (`property`, `modifier`, `action`) written once each, `child` on the action, and the two-level rule. It replaces the inline 30-brace literal. |
| `os/system/builder/BuildGoal/Properties.goal` | `render template "/system/builder/llm/Properties.schema", write to %propertiesSchema%`, then `Schema=%propertiesSchema%`. `llm.query` only appends the Schema as text to the system message (`OpenAi.cs:139-148`, `:758-766`) and never enforces it, so readable text is enough. |
| `PLang/app/type/property/this.cs` | `HasDefault` (a template can't tell a `false` default from none, since Liquid treats `false == nil`) and `Required` (not nullable, no default). |
| `PLang/app/goal/step/action/this.Validate.cs` | Asks `declared.Required` instead of working the rule out itself, so the rule lives in one place. |
| `os/system/builder/llm/templates/propertiesUser.template` | Reads `p.Required` and `p.HasDefault`. Now `Negate (bool, optional, default false)`, `Depth (number, optional, default 1)`. |
| `PLang.Tests/.../ui/RenderTests.cs` | +3 tests on the real template loop: `false` default shown, number default shown, no-null/no-default is required. |
| `os/system/modules/condition/{if,elseif}.examples.md`, `if.notes.md` | The body is described as the condition's `child`. The compound example uses `child` and `Name` (not `GoalName`). The operator list adds `is` (P7, P8). |
| `tools/decider/build_pr.py` | `user_message()` renders the template's format exactly: alias types (`path`), defaults as Fluid prints them, `one of`, the synthetic `channel` row, infra-typed rows dropped, no `SHAPES`, no `[modifier]` tag. The request mirrors `OpenAi.cs`: system = `Properties.llm` + `\nYou MUST respond in JSON, schema: <Properties.schema>`, temperature 0, `max_completion_tokens` 16000, no `response_format`. `pr_action` carries `child` into the `.pr`. |
| `tools/decider/child_golden.json` (new) | 20 goals / 26 scored steps, expected structure by hand. |

C# suites: all six match the baseline, plus 3 new passing tests.

## Parity: python vs the plang template

A throwaway test (not committed) rendered the real `propertiesUser.template` through plang's Fluid provider for all 20 golden cases: `Goal.Parse` of the case, `app.Module.list`, and the menu. Python's `user_message()` was compared against it.

- **Every step header and menu block matches in 19/20 cases.** Bytes match exactly in 5; in the other 14 only the menu **order** differs. The template walks the module catalog, a hash dictionary (`module/list/this.cs:111`, `_modules.Keys`), so its order is arbitrary. Python keeps the menu's order.
- **1/20 differs: an enum default.** plang prints `Overflow (choice<overflow>: one of Promote, Throw, optional, default 0)`. Liquid renders a C# enum as its number, so "default 0" isn't among the options. Python prints `default Promote`. **plang bug, see F2.**

## Findings while building this (need your call)

| # | Finding | Evidence | Proposal |
|---|---|---|---|
| **F1** | **The shipped template renders EVERY menu empty.** `{% if menu[s.Index] contains choice %}` compares a plang text item (what `%choices%` holds) against a Liquid string built with `append`, and never matches. The list/dict template views hand Liquid the plang items (`ui/code/Fluid.cs:312,320`), while member access lowers leaves to plain strings/numbers/bools (`:219-220`). | The render with `menu` as a plang list: `step 0: … menu:` and nothing under it. With a `capture` compare it renders fully. | (a) The views lower leaves the way the door does. But the `store` filter relies on list elements staying plang items (`Fluid.cs:119-123`), so this is a design call. (b) #22's "the menu holds actions": the template walks `menu[s.Index]` directly, which removes `contains` and the catalog scan, and also fixes the order. (c) A template-only `capture` compare (works today, ugly). **The eval renders the intended menu**, i.e. what the template produces once F1 is fixed. |
| **F2** | Enum defaults render as numbers (`default 0`) | above | A Fluid value converter: an enum renders as its member name (plang choices are names). Global, so your call. |
| **F3** | The catalog drops `action`-typed slots (`property/list/this.cs:82`): `channel.set`'s `Goal` never reaches a plang menu, so `Properties.llm`'s "A property typed `action`…" example can never apply there. | `Reflect` | Leave it for now (no golden case uses it); decide with #22. |
| **F4** | What does `set %menu[step.Index]% = %choices%` build: a dict keyed "0" or a list? The template indexes `menu[s.Index]` with a number. A dict with string keys rendered nothing even before F1; a list worked. | throwaway render | Verify when the builder runs; the golden render uses a list. |

## The prompts after the fixes

**System**: `os/system/builder/llm/Properties.llm` (now 172 lines), then:

```
You MUST respond in JSON, schema: property = {name: string, type: {name: string, kind?: string}, value?: object}
modifier = {module: string, name: string, property?: list<property>,
            recovery?: list<{module: string, name: string, property?: list<property>}>}
action   = {module: string, name: string, property?: list<property>,
            modifier?: list<modifier>, child?: list<{text: string, action: list<action>}>}
            (child nests at most two levels deep: an action inside a child may have its own child, one more level)

{step: list<{index: int, action: list<action>}>}
```

**User**, rendered by plang (intended menu, F1). `Grade`:

```
Grade

step 0: if %score% > 90, write out "A", else if %score% > 70, write out "B", else write out "C"
   menu:
     output.write
        Data (item, required)
        channel (text, optional)
     condition.if
        Left (item, optional)
        Operator (choice<operator>: one of ==, !=, >, <, >=, <=, contains, startswith, endswith, in, isempty, is, and, or, required)
        Right (item, optional)
        Negate (bool, optional, default false)
     condition.elseif
        (same four rows)
     condition.else
```

`MenuModule` steps 1-2:

```
step 1: if %moduleAnswer[key].noul% is less than %threshold%, return
   menu:
     goal.return
        Data (item, optional)
        Depth (number, optional, default 1)
     condition.if
        (four rows as above)

step 2: if %module.Action.Count% is 1, add "%module.Name%.%module.Action[0].Name%" to %choices%
   menu:
     list.add
        ListName (variable, required)
        Value (item, required)
        AtIndex (number, optional, default -1)
     condition.if
        (four rows as above)
```

`MaybeProcess` (indented body; the model sees no indentation):

```
step 0: if %count% > 0
   menu:
     condition.if
        (four rows as above)

step 1: call ProcessItems
   menu:
     goal.call
        Name (text, required)
        Parameter (list, optional)
        Actor (choice<actor>: one of system, user, optional)
        Parallel (bool, optional, default false)

step 2: write out "done"
   menu:
     output.write
        Data (item, required)
        channel (text, optional)
```

## The golden set: `tools/decider/child_golden.json`

| id | covers | steps |
|---|---|---|
| if_return | `if X, return` | 1 |
| if_call_else_call | if call A, else call B | 1 |
| if_elseif_else_line | if/elseif/else in one line | 1 |
| if_elseif_no_else | if/elseif without else | 1 |
| else_return | else branch is a return | 1 |
| negate_contains | "does not contain" → contains + Negate | 1 |
| negate_isempty | "is not empty" → isempty + Negate, no Right | 1 |
| setup_before_if | count, write to, then if | 1 |
| body_with_write_to | `write to` inside the branch | 1 |
| body_two_actions | two actions in one branch | 1 |
| op_in / op_startswith / op_is_type | operators | 3 |
| menumodule | BuildGoal/Start.goal:42-45 (4 steps, one without a condition) | 4 |
| plain_write / plain_call_args / plain_add | no condition → no child | 3 |
| indented_body | indented body: must NOT be nested | 3 |
| indented_if_else | indented if + else lines | 4 |
| nested_inline | two levels in one line | 1 |

Scoring rules are in the file's `about` field. The action tree must match module/name/order/child placement. Listed property values must match. An unlisted property must be absent. Child text must be the step's own words for that branch. `body_two_actions` accepts one child step or one per action.

None of these steps copies the prompt's worked examples.
