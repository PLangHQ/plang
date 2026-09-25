# Child eval — Stage 2: does the model put a condition's body in `child`?

2026-09-25, get-builder-running.
- **Run:** `tools/decider/child_eval.py` over `tools/decider/child_golden.json`: 20 goals, **28 scored steps**, 3 runs per model. Stage 3 only. temperature 0.
- **Raw data:** `tools/decider/runs/child_eval_20260925_095821/`: `system.txt`, `user.<case>.txt`, and per model/case `<run>.raw.json` + `<run>.answer.json`, plus `summary.json`.
- **Prompt:** frozen for the whole run, the one committed at `4df7f8075`. The system message is `Properties.llm` + `You MUST respond in JSON, schema: <Properties.schema>`, exactly as `OpenAi.cs:139-148` appends it. The user message is the intended `propertiesUser.template` render (F1 fixed). The one difference from plang is that an enum default shows its name (F2).
- **Calls:** all 120 answered; every answer parsed as JSON; no fences.

## Result

| | gpt-5.4-nano | gpt-5.4-mini |
|---|---|---|
| steps right, raw | **59/84** (70%) | **63/84** (75%) |
| misses the prompt caused (below) | 21 | 16 |
| misses I judge the model's | **4** | **5** |
| steps right if the prompt-caused misses are fixed (projection, not measured) | ~80/84 | ~79/84 |
| body placed in `child` on the 15 inline-condition cases (if / elseif / else / negation / operators / setup / MenuModule / nested) | **45/45 runs** | 40/45: else merged into the if's child 2×; `write to` dropped or left outside 3× |
| a nested inline condition (`if A, if B, call X`), two levels | 3/3 | 3/3 |
| no invented child on a step without a condition (plain_*) | 3/3 each | 3/3 each |

**Answer to the question:** yes. Both models put an inline condition's body in `child`, including a two-level nest. nano did it on every inline-condition run. Nearly every miss comes from something the prompt doesn't show or teach, not from the child ruling. **nano is good enough on the child question** once the prompt fixes below land; a re-run with them would confirm.

## Every miss, judged: prompt first

| Class | Model / runs | What happened | Cause | Prompt fix |
|---|---|---|---|---|
| **M1: separate steps merged; answer steps dropped** | both, `indented_if_else` 3/3 each (12 step-runs each) | For `step 0: if %x% == 1`, `step 1: write out "one"`, `step 2: else`, `step 3: write out "other"`, the model wrote ONE entry: step 0 = `if{write one}` + `else{write other}`. Steps 1-3 were missing from the answer. | **Prompt.** The user message doesn't show indentation (the template has no `s.Indent`), and nothing says "one entry per step; a step holds only its own words". The Conditions rule "a condition whose step names nothing gets no child" was right there and still lost to merging, because nothing forbids moving another step's actions. | PF1 |
| **M2: invented child from the next steps** | nano, `indented_body` 3/3 | `step 0: if %count% > 0` got `child: call ProcessItems` (run 1 also `write out "done"`). Steps 1-2 were still answered, so the call appears twice. | **Prompt**, same as M1: the model can't see that step 1 is indented under step 0, and no rule says a step's entry holds only its own words. mini got it right 3/3. | PF1 |
| **M3: literal typed as text** | mini `body_two_actions` 3/3 (`Right: "null"`), nano `else_return` 1/3 (`Right: "true"`), nano `op_in` 2/3 (`Right: "[\"admin\", \"owner\"]"`) | null, true and a list written as strings | **Prompt.** "A value the step writes goes in as written — a quoted text as that text, a %variable% with its % signs, a number as a number" covers text, variables and numbers only; true/false, null and `[…]` are never mentioned. | PF2 |
| **M4: `Right` on a unary operator** | nano `negate_isempty` 3/3 | `isempty` with `Right: "%items%"` | **Prompt.** "isempty takes no Right" lives in `condition/if.notes.md`, which stage 3 never sees (#22). | PF3 |
| **M5: `is` operand** | mini `op_is_type` 1/3 | `Right: "a number"` instead of `"number"` | **Prompt**, same source: the `is` rule is only in the notes. | PF3 |
| **M6: else folded into the if's child** | mini `if_call_else_call` 2/3 | `if{call Activate \| call Deactivate}`: two child steps, no `else` action, although `condition.else` is on the menu | **Mostly model.** Properties.llm's second worked example is this exact shape. One weakness in the prompt: "the body goes in the condition's child — a list of steps" can be read as "the branches". | PF4 (reinforce) |
| **M7: a branch's `write to` lost** | mini `body_with_write_to` 3/3 | `variable.set` dropped (runs 1, 3) or put after the if at step level (run 2) | **Mostly model.** The rule "the step's own list holds nothing but the next branch" covers run 2. Nothing specific says a trailing `write to %x%` belongs to the branch it follows. nano got it right 2/3. | PF4 (reinforce) |
| **M8: a default repeated** | nano `body_with_write_to` 1/3 | `Overflow: "Promote"`, `Precision: "Error"` written although they are the defaults | **Model.** "Never put in a placeholder and never repeat a default" is in the prompt. | — |
| **M9: invented value, shortened text** | nano `menumodule` run 3 (steps 1-3) | `goal.return` with `Data: "%moduleAnswer[key].noul%"`; child text `"add to %choices%"` instead of the step's words | **Model.** Both rules are in the prompt ("leave an optional property out unless the step's text names it"; the text is "the words of the step that body does, as written"). | — |

## Proposed prompt fixes (not applied; the prompt stayed frozen)

- **PF1** (`Properties.llm`, the top section): "Answer every step, one entry per step index. A step's entry holds only the actions its OWN words name: never move another step's work into it, even when that step is indented under a condition. A step that is only `else` is `condition.else` with no `child`."
  - Option: render indentation in `propertiesUser.template` (`s.Indent`) so the model sees the structure. **Not recommended**: with indentation visible, the model is more likely to nest, and Ingi ruled that `build.fold` owns indented bodies.
- **PF2** (Filling a property): "…a number as a number, `true`/`false` as a bool, `null` as null, a `[…]` list as a list."
- **PF3**: give stage 3 the chosen actions' notes (the #22 direction; `condition/if.notes.md` already has the isempty / `is` / operator table). Stop-gap: one line in the Conditions section: "`isempty` takes no `Right`; `is` takes a type name (`number`, `text`)".
- **PF4** (Conditions): "Each branch has its own `child`: the if's body in the if's `child`, the else's body in the else's `child`. A `write to %x%` inside a branch is part of that branch."

## Notes on the method

- The golden expected `Right: null` for `%user% == null`. That's a judgment I made, and it's how `if.notes.md` maps "is null". If you'd rather accept the string `"null"`, M3 for mini drops out.
- The menu order in the user message is the decider's order (python), not plang's hash-ordered catalog walk. With F1's fix (the template walks `%menu[i]%`) plang gets the same order.

---

STEP TOTALS: gpt-5.4-nano 59/84, gpt-5.4-mini 63/84

| case | step | expected | gpt-5.4-nano got ×3 | gpt-5.4-nano | gpt-5.4-mini got ×3 | gpt-5.4-mini |
|---|---|---|---|---|---|---|
| if_return | 0 | `condition.if(Left="%count%",Operator="<",Right=3){"return": goal.return()}` | 1: ✓<br>2: ✓<br>3: ✓ | YES | 1: ✓<br>2: ✓<br>3: ✓ | YES |
| if_call_else_call | 0 | `condition.if(Left="%status%",Operator="==",Right="active"){"call Activate": goal.call(Name="Activate")} ; condition.else(){"call Deactivate": goal.call(Name="Deactivate")}` | 1: ✓<br>2: ✓<br>3: ✓ | YES | 1: ✓<br>2: ✗ `condition.if(Left="%status%",Operator="==",Right="active"){"call Activate": goal.call(Name="Activate") \| "call Deactivate": goal.call(Name="Deactivate")}`<br>3: ✗ `condition.if(Left="%status%",Operator="==",Right="active"){"call Activate": goal.call(Name="Activate") \| "call Deactivate": goal.call(Name="Deactivate")}` | NO (1/3) |
| if_elseif_else_line | 0 | `condition.if(Left="%score%",Operator=">",Right=90){"write out "A"": output.write(Data="A")} ; condition.elseif(Left="%score%",Operator=">",Right=70){"write out "B"": output.write(Data="B")} ; condition.else(){"write out "C"": output.write(Data="C")}` | 1: ✓<br>2: ✓<br>3: ✓ | YES | 1: ✓<br>2: ✓<br>3: ✓ | YES |
| if_elseif_no_else | 0 | `condition.if(Left="%lang%",Operator="==",Right="is"){"write out "Halló"": output.write(Data="Halló")} ; condition.elseif(Left="%lang%",Operator="==",Right="en"){"write out "Hello"": output.write(Data="Hello")}` | 1: ✓<br>2: ✓<br>3: ✓ | YES | 1: ✓<br>2: ✓<br>3: ✓ | YES |
| else_return | 0 | `condition.if(Left="%ok%",Operator="==",Right=true){"call Continue": goal.call(Name="Continue")} ; condition.else(){"return": goal.return()}` | 1: ✓<br>2: ✗ `condition.if(Left="%ok%",Operator="==",Right="true"){"call Continue": goal.call(Name="Continue")} ; condition.else(){"return": goal.return()}`<br>3: ✓ | NO (2/3) | 1: ✓<br>2: ✓<br>3: ✓ | YES |
| negate_contains | 0 | `condition.if(Left="%name%",Operator="contains",Right="admin",Negate=true){"call Deny": goal.call(Name="Deny")}` | 1: ✓<br>2: ✓<br>3: ✓ | YES | 1: ✓<br>2: ✓<br>3: ✓ | YES |
| negate_isempty | 0 | `condition.if(Left="%items%",Operator="isempty",Negate=true){"call ProcessItems": goal.call(Name="ProcessItems")}` | 1: ✗ `condition.if(Left="%items%",Operator="isempty",Right="%items%",Negate=true){"call ProcessItems": goal.call(Name="ProcessItems")}`<br>2: ✗ `condition.if(Left="%items%",Operator="isempty",Right="%items%",Negate=true){"call ProcessItems": goal.call(Name="ProcessItems")}`<br>3: ✗ `condition.if(Left="%items%",Operator="isempty",Right="%items%",Negate=true){"call ProcessItems": goal.call(Name="ProcessItems")}` | NO (0/3) | 1: ✓<br>2: ✓<br>3: ✓ | YES |
| setup_before_if | 0 | `list.count(ListName="%items%") ; variable.set(Name="%n%",Value="%!data%") ; condition.if(Left="%n%",Operator=">",Right=10){"call Paginate": goal.call(Name="Paginate")}` | 1: ✓<br>2: ✓<br>3: ✓ | YES | 1: ✓<br>2: ✓<br>3: ✓ | YES |
| body_with_write_to | 0 | `condition.if(Left="%price%",Operator=">",Right=100){"multiply %price% by 0.9, write to %price%": math.multiply(A="%price%",B=0.9); variable.set(Name="%price%",Value="%!data%")}` | 1: ✓<br>2: ✗ `condition.if(Left="%price%",Operator=">",Right=100){"multiply %price% by 0.9, write to %price%": math.multiply(A="%price%",B=0.9,Overflow="Promote",Precision="Error"); variable.set(Name="%price%",Value="%!data%")}`<br>3: ✓ | NO (2/3) | 1: ✗ `condition.if(Left="%price%",Operator=">",Right=100){"multiply %price% by 0.9, write to %price%": math.multiply(A="%price%",B=0.9)}`<br>2: ✗ `condition.if(Left="%price%",Operator=">",Right=100){"multiply %price% by 0.9, write to %price%": math.multiply(A="%price%",B=0.9)} ; variable.set(Name="%price%",Value="%!data%")`<br>3: ✗ `condition.if(Left="%price%",Operator=">",Right=100){"multiply %price% by 0.9, write to %price%": math.multiply(A="%price%",B=0.9)}` | NO (0/3) |
| body_two_actions | 0 | `condition.if(Left="%user%",Operator="==",Right=null){"write out "no user" and return": output.write(Data="no user"); goal.return()}` | 1: ✓<br>2: ✓<br>3: ✓ | YES | 1: ✗ `condition.if(Left="%user%",Operator="==",Right="null"){"write out "no user" and return": output.write(Data="no user"); goal.return()}`<br>2: ✗ `condition.if(Left="%user%",Operator="==",Right="null"){"write out "no user" and return": output.write(Data="no user"); goal.return()}`<br>3: ✗ `condition.if(Left="%user%",Operator="==",Right="null"){"write out "no user" and return": output.write(Data="no user"); goal.return()}` | NO (0/3) |
| op_in | 0 | `condition.if(Left="%role%",Operator="in",Right=["admin", "owner"]){"call GrantAccess": goal.call(Name="GrantAccess")}` | 1: ✗ `condition.if(Left="%role%",Operator="in",Right="[\"admin\", \"owner\"]"){"call GrantAccess": goal.call(Name="GrantAccess")}`<br>2: ✓<br>3: ✗ `condition.if(Left="%role%",Operator="in",Right="[\"admin\", \"owner\"]"){"call GrantAccess": goal.call(Name="GrantAccess")}` | NO (1/3) | 1: ✓<br>2: ✓<br>3: ✓ | YES |
| op_startswith | 0 | `condition.if(Left="%path%",Operator="startswith",Right="/api"){"call HandleApi path=%path%": goal.call(Name="HandleApi",Parameter=[{"name": "path", "value": "%path%"}])}` | 1: ✓<br>2: ✓<br>3: ✓ | YES | 1: ✓<br>2: ✓<br>3: ✓ | YES |
| op_is_type | 0 | `condition.if(Left="%value%",Operator="is",Right="number"){"write out "numeric"": output.write(Data="numeric")}` | 1: ✓<br>2: ✓<br>3: ✓ | YES | 1: ✗ `condition.if(Left="%value%",Operator="is",Right="a number"){"write out "numeric"": output.write(Data="numeric")}`<br>2: ✓<br>3: ✓ | NO (2/3) |
| menumodule | 0 | `variable.set(Name="%key%",Value="s%step.Index%_%module.Name%")` | 1: ✓<br>2: ✓<br>3: ✓ | YES | 1: ✓<br>2: ✓<br>3: ✓ | YES |
| menumodule | 1 | `condition.if(Left="%moduleAnswer[key].noul%",Operator="<",Right="%threshold%"){"return": goal.return()}` | 1: ✓<br>2: ✓<br>3: ✗ `condition.if(Left="%moduleAnswer[key].noul%",Operator="<",Right="%threshold%"){"return": goal.return(Data="%moduleAnswer[key].noul%")}` | NO (2/3) | 1: ✓<br>2: ✓<br>3: ✓ | YES |
| menumodule | 2 | `condition.if(Left="%module.Action.Count%",Operator="==",Right=1){"add "%module.Name%.%module.Action[0].Name%" to %choices%": list.add(ListName="%choices%",Value="%module.Name%.%module.Action[0].Name%")}` | 1: ✓<br>2: ✓<br>3: ✗ `condition.if(Left="%module.Action.Count%",Operator="==",Right=1){"add to %choices%": list.add(ListName="%choices%",Value="%module.Name%.%module.Action[0].Name%")}` | NO (2/3) | 1: ✓<br>2: ✓<br>3: ✓ | YES |
| menumodule | 3 | `condition.if(Left="%module.Action.Count%",Operator=">",Right=1){"add "%module.Name%.%actionAnswer[key].choice%" to %choices%": list.add(ListName="%choices%",Value="%module.Name%.%actionAnswer[key].choice%")}` | 1: ✓<br>2: ✓<br>3: ✗ `condition.if(Left="%module.Action.Count%",Operator=">",Right=1){"add to %choices%": list.add(ListName="%choices%",Value="%module.Name%.%actionAnswer[key].choice%")}` | NO (2/3) | 1: ✓<br>2: ✓<br>3: ✓ | YES |
| plain_write | 0 | `output.write(Data="hello")` | 1: ✓<br>2: ✓<br>3: ✓ | YES | 1: ✓<br>2: ✓<br>3: ✓ | YES |
| plain_call_args | 0 | `goal.call(Name="SendMail",Parameter=[{"name": "to", "value": "%email%"}, {"name": "subject", "value": "Welcome"}])` | 1: ✓<br>2: ✓<br>3: ✓ | YES | 1: ✓<br>2: ✓<br>3: ✓ | YES |
| plain_add | 0 | `list.add(ListName="%items%",Value="%item%")` | 1: ✓<br>2: ✓<br>3: ✓ | YES | 1: ✓<br>2: ✓<br>3: ✓ | YES |
| indented_body | 0 | `condition.if(Left="%count%",Operator=">",Right=0)` | 1: ✗ `condition.if(Left="%count%",Operator=">",Right=0){"call ProcessItems": goal.call(Name="ProcessItems") \| "write out "done"": output.write(Data="done")}`<br>2: ✗ `condition.if(Left="%count%",Operator=">",Right=0){"call ProcessItems": goal.call(Name="ProcessItems")}`<br>3: ✗ `condition.if(Left="%count%",Operator=">",Right=0){"call ProcessItems": goal.call(Name="ProcessItems")}` | NO (0/3) | 1: ✓<br>2: ✓<br>3: ✓ | YES |
| indented_body | 1 | `goal.call(Name="ProcessItems")` | 1: ✓<br>2: ✓<br>3: ✓ | YES | 1: ✓<br>2: ✓<br>3: ✓ | YES |
| indented_body | 2 | `output.write(Data="done")` | 1: ✓<br>2: ✓<br>3: ✓ | YES | 1: ✓<br>2: ✓<br>3: ✓ | YES |
| indented_if_else | 0 | `condition.if(Left="%x%",Operator="==",Right=1)` | 1: ✗ `condition.if(Left="%x%",Operator="==",Right=1){"write out "one"": output.write(Data="one")} ; condition.else(){"write out "other"": output.write(Data="other")}`<br>2: ✗ `condition.if(Left="%x%",Operator="==",Right=1){"write out "one"": output.write(Data="one")} ; condition.else(){"write out "other"": output.write(Data="other")}`<br>3: ✗ `condition.if(Left="%x%",Operator="==",Right=1){"write out "one"": output.write(Data="one")} ; condition.else(){"write out "other"": output.write(Data="other")}` | NO (0/3) | 1: ✗ `condition.if(Left="%x%",Operator="==",Right=1){"write out "one"": output.write(Data="one")} ; condition.else(){"write out "other"": output.write(Data="other")}`<br>2: ✗ `condition.if(Left="%x%",Operator="==",Right=1){"write out "one"": output.write(Data="one")} ; condition.else(){"write out "other"": output.write(Data="other")}`<br>3: ✗ `condition.if(Left="%x%",Operator="==",Right=1){"write out "one"": output.write(Data="one")} ; condition.else(){"write out "other"": output.write(Data="other")}` | NO (0/3) |
| indented_if_else | 1 | `output.write(Data="one")` | 1: ✗ `(missing)`<br>2: ✗ `(missing)`<br>3: ✗ `(missing)` | NO (0/3) | 1: ✗ `(missing)`<br>2: ✗ `(missing)`<br>3: ✗ `(missing)` | NO (0/3) |
| indented_if_else | 2 | `condition.else()` | 1: ✗ `(missing)`<br>2: ✗ `(missing)`<br>3: ✗ `(missing)` | NO (0/3) | 1: ✗ `(missing)`<br>2: ✗ `(missing)`<br>3: ✗ `(missing)` | NO (0/3) |
| indented_if_else | 3 | `output.write(Data="other")` | 1: ✗ `(missing)`<br>2: ✗ `(missing)`<br>3: ✗ `(missing)` | NO (0/3) | 1: ✗ `(missing)`<br>2: ✗ `(missing)`<br>3: ✗ `(missing)` | NO (0/3) |
| nested_inline | 0 | `condition.if(Left="%a%",Operator=">",Right=0){"if %b% > 0, call BothPositive": condition.if(Left="%b%",Operator=">",Right=0){"call BothPositive": goal.call(Name="BothPositive")}}` | 1: ✓<br>2: ✓<br>3: ✓ | YES | 1: ✓<br>2: ✓<br>3: ✓ | YES |

## Every miss, in full

**body_two_actions — gpt-5.4-mini run 1**
- step 0[0] condition.if: Right = 'null', expected None
- got step 0: `condition.if(Left="%user%",Operator="==",Right="null"){"write out "no user" and return": output.write(Data="no user"); goal.return()}`

**body_two_actions — gpt-5.4-mini run 2**
- step 0[0] condition.if: Right = 'null', expected None
- got step 0: `condition.if(Left="%user%",Operator="==",Right="null"){"write out "no user" and return": output.write(Data="no user"); goal.return()}`

**body_two_actions — gpt-5.4-mini run 3**
- step 0[0] condition.if: Right = 'null', expected None
- got step 0: `condition.if(Left="%user%",Operator="==",Right="null"){"write out "no user" and return": output.write(Data="no user"); goal.return()}`

**body_with_write_to — gpt-5.4-mini run 1**
- step 0[0] child[0]: 1 actions, expected 2 (math.multiply(A="%price%",B=0.9))
- got step 0: `condition.if(Left="%price%",Operator=">",Right=100){"multiply %price% by 0.9, write to %price%": math.multiply(A="%price%",B=0.9)}`

**body_with_write_to — gpt-5.4-mini run 2**
- step 0: 2 actions, expected 1 (condition.if(Left="%price%",Operator=">",Right=100){"multiply %price% by 0.9, write to %price%": math.multiply(A="%price%",B=0.9)}, variable.set(Name="%price%",Value="%!data%"))
- step 0[0] child[0]: 1 actions, expected 2 (math.multiply(A="%price%",B=0.9))
- got step 0: `condition.if(Left="%price%",Operator=">",Right=100){"multiply %price% by 0.9, write to %price%": math.multiply(A="%price%",B=0.9)} ; variable.set(Name="%price%",Value="%!data%")`

**body_with_write_to — gpt-5.4-mini run 3**
- step 0[0] child[0]: 1 actions, expected 2 (math.multiply(A="%price%",B=0.9))
- got step 0: `condition.if(Left="%price%",Operator=">",Right=100){"multiply %price% by 0.9, write to %price%": math.multiply(A="%price%",B=0.9)}`

**body_with_write_to — gpt-5.4-nano run 2**
- step 0[0] child[0][0] math.multiply: extra Overflow = 'Promote'
- step 0[0] child[0][0] math.multiply: extra Precision = 'Error'
- got step 0: `condition.if(Left="%price%",Operator=">",Right=100){"multiply %price% by 0.9, write to %price%": math.multiply(A="%price%",B=0.9,Overflow="Promote",Precision="Error"); variable.set(Name="%price%",Value="%!data%")}`

**else_return — gpt-5.4-nano run 2**
- step 0[0] condition.if: Right = 'true', expected True
- got step 0: `condition.if(Left="%ok%",Operator="==",Right="true"){"call Continue": goal.call(Name="Continue")} ; condition.else(){"return": goal.return()}`

**if_call_else_call — gpt-5.4-mini run 2**
- step 0: 1 actions, expected 2 (condition.if(Left="%status%",Operator="==",Right="active"){"call Activate": goal.call(Name="Activate") | "call Deactivate": goal.call(Name="Deactivate")})
- step 0[0] child: 2 child steps, expected 1
- got step 0: `condition.if(Left="%status%",Operator="==",Right="active"){"call Activate": goal.call(Name="Activate") \| "call Deactivate": goal.call(Name="Deactivate")}`

**if_call_else_call — gpt-5.4-mini run 3**
- step 0: 1 actions, expected 2 (condition.if(Left="%status%",Operator="==",Right="active"){"call Activate": goal.call(Name="Activate") | "call Deactivate": goal.call(Name="Deactivate")})
- step 0[0] child: 2 child steps, expected 1
- got step 0: `condition.if(Left="%status%",Operator="==",Right="active"){"call Activate": goal.call(Name="Activate") \| "call Deactivate": goal.call(Name="Deactivate")}`

**indented_body — gpt-5.4-nano run 1**
- step 0[0] child: invented ([{"text": "call ProcessItems", "action": [{"module": "goal", "name": "call", "property": [{"name": "Name", "type": {"nam)
- got step 0: `condition.if(Left="%count%",Operator=">",Right=0){"call ProcessItems": goal.call(Name="ProcessItems") \| "write out "done"": output.write(Data="done")}`

**indented_body — gpt-5.4-nano run 2**
- step 0[0] child: invented ([{"text": "call ProcessItems", "action": [{"module": "goal", "name": "call", "property": [{"name": "Name", "type": {"nam)
- got step 0: `condition.if(Left="%count%",Operator=">",Right=0){"call ProcessItems": goal.call(Name="ProcessItems")}`

**indented_body — gpt-5.4-nano run 3**
- step 0[0] child: invented ([{"text": "call ProcessItems", "action": [{"module": "goal", "name": "call", "property": [{"name": "Name", "type": {"nam)
- got step 0: `condition.if(Left="%count%",Operator=">",Right=0){"call ProcessItems": goal.call(Name="ProcessItems")}`

**indented_if_else — gpt-5.4-mini run 1**
- step 0: 2 actions, expected 1 (condition.if(Left="%x%",Operator="==",Right=1){"write out "one"": output.write(Data="one")}, condition.else(){"write out "other"": output.write(Data="other")})
- step 0[0] child: invented ([{"text": "write out \"one\"", "action": [{"module": "output", "name": "write", "property": [{"name": "Data", "type": {")
- got step 0: `condition.if(Left="%x%",Operator="==",Right=1){"write out "one"": output.write(Data="one")} ; condition.else(){"write out "other"": output.write(Data="other")}`
- step 1: not in the answer
- got step 1: `(missing)`
- step 2: not in the answer
- got step 2: `(missing)`
- step 3: not in the answer
- got step 3: `(missing)`

**indented_if_else — gpt-5.4-mini run 2**
- step 0: 2 actions, expected 1 (condition.if(Left="%x%",Operator="==",Right=1){"write out "one"": output.write(Data="one")}, condition.else(){"write out "other"": output.write(Data="other")})
- step 0[0] child: invented ([{"text": "write out \"one\"", "action": [{"module": "output", "name": "write", "property": [{"name": "Data", "type": {")
- got step 0: `condition.if(Left="%x%",Operator="==",Right=1){"write out "one"": output.write(Data="one")} ; condition.else(){"write out "other"": output.write(Data="other")}`
- step 1: not in the answer
- got step 1: `(missing)`
- step 2: not in the answer
- got step 2: `(missing)`
- step 3: not in the answer
- got step 3: `(missing)`

**indented_if_else — gpt-5.4-mini run 3**
- step 0: 2 actions, expected 1 (condition.if(Left="%x%",Operator="==",Right=1){"write out "one"": output.write(Data="one")}, condition.else(){"write out "other"": output.write(Data="other")})
- step 0[0] child: invented ([{"text": "write out \"one\"", "action": [{"module": "output", "name": "write", "property": [{"name": "Data", "type": {")
- got step 0: `condition.if(Left="%x%",Operator="==",Right=1){"write out "one"": output.write(Data="one")} ; condition.else(){"write out "other"": output.write(Data="other")}`
- step 1: not in the answer
- got step 1: `(missing)`
- step 2: not in the answer
- got step 2: `(missing)`
- step 3: not in the answer
- got step 3: `(missing)`

**indented_if_else — gpt-5.4-nano run 1**
- step 0: 2 actions, expected 1 (condition.if(Left="%x%",Operator="==",Right=1){"write out "one"": output.write(Data="one")}, condition.else(){"write out "other"": output.write(Data="other")})
- step 0[0] child: invented ([{"text": "write out \"one\"", "action": [{"module": "output", "name": "write", "property": [{"name": "Data", "type": {")
- got step 0: `condition.if(Left="%x%",Operator="==",Right=1){"write out "one"": output.write(Data="one")} ; condition.else(){"write out "other"": output.write(Data="other")}`
- step 1: not in the answer
- got step 1: `(missing)`
- step 2: not in the answer
- got step 2: `(missing)`
- step 3: not in the answer
- got step 3: `(missing)`

**indented_if_else — gpt-5.4-nano run 2**
- step 0: 2 actions, expected 1 (condition.if(Left="%x%",Operator="==",Right=1){"write out "one"": output.write(Data="one")}, condition.else(){"write out "other"": output.write(Data="other")})
- step 0[0] child: invented ([{"text": "write out \"one\"", "action": [{"module": "output", "name": "write", "property": [{"name": "Data", "type": {")
- got step 0: `condition.if(Left="%x%",Operator="==",Right=1){"write out "one"": output.write(Data="one")} ; condition.else(){"write out "other"": output.write(Data="other")}`
- step 1: not in the answer
- got step 1: `(missing)`
- step 2: not in the answer
- got step 2: `(missing)`
- step 3: not in the answer
- got step 3: `(missing)`

**indented_if_else — gpt-5.4-nano run 3**
- step 0: 2 actions, expected 1 (condition.if(Left="%x%",Operator="==",Right=1){"write out "one"": output.write(Data="one")}, condition.else(){"write out "other"": output.write(Data="other")})
- step 0[0] child: invented ([{"text": "write out \"one\"", "action": [{"module": "output", "name": "write", "property": [{"name": "Data", "type": {")
- got step 0: `condition.if(Left="%x%",Operator="==",Right=1){"write out "one"": output.write(Data="one")} ; condition.else(){"write out "other"": output.write(Data="other")}`
- step 1: not in the answer
- got step 1: `(missing)`
- step 2: not in the answer
- got step 2: `(missing)`
- step 3: not in the answer
- got step 3: `(missing)`

**menumodule — gpt-5.4-nano run 3**
- step 1[0] child[0][0] goal.return: extra Data = '%moduleAnswer[key].noul%'
- got step 1: `condition.if(Left="%moduleAnswer[key].noul%",Operator="<",Right="%threshold%"){"return": goal.return(Data="%moduleAnswer[key].noul%")}`
- step 2[0] child[0] text 'add to %choices%', expected 'add "%module.Name%.%module.Action[0].Name%" to %choices%'
- got step 2: `condition.if(Left="%module.Action.Count%",Operator="==",Right=1){"add to %choices%": list.add(ListName="%choices%",Value="%module.Name%.%module.Action[0].Name%")}`
- step 3[0] child[0] text 'add to %choices%', expected 'add "%module.Name%.%actionAnswer[key].choice%" to %choices%'
- got step 3: `condition.if(Left="%module.Action.Count%",Operator=">",Right=1){"add to %choices%": list.add(ListName="%choices%",Value="%module.Name%.%actionAnswer[key].choice%")}`

**negate_isempty — gpt-5.4-nano run 1**
- step 0[0] condition.if: extra Right = '%items%'
- got step 0: `condition.if(Left="%items%",Operator="isempty",Right="%items%",Negate=true){"call ProcessItems": goal.call(Name="ProcessItems")}`

**negate_isempty — gpt-5.4-nano run 2**
- step 0[0] condition.if: extra Right = '%items%'
- got step 0: `condition.if(Left="%items%",Operator="isempty",Right="%items%",Negate=true){"call ProcessItems": goal.call(Name="ProcessItems")}`

**negate_isempty — gpt-5.4-nano run 3**
- step 0[0] condition.if: extra Right = '%items%'
- got step 0: `condition.if(Left="%items%",Operator="isempty",Right="%items%",Negate=true){"call ProcessItems": goal.call(Name="ProcessItems")}`

**op_in — gpt-5.4-nano run 1**
- step 0[0] condition.if: Right = '["admin", "owner"]', expected ['admin', 'owner']
- got step 0: `condition.if(Left="%role%",Operator="in",Right="[\"admin\", \"owner\"]"){"call GrantAccess": goal.call(Name="GrantAccess")}`

**op_in — gpt-5.4-nano run 3**
- step 0[0] condition.if: Right = '["admin", "owner"]', expected ['admin', 'owner']
- got step 0: `condition.if(Left="%role%",Operator="in",Right="[\"admin\", \"owner\"]"){"call GrantAccess": goal.call(Name="GrantAccess")}`

**op_is_type — gpt-5.4-mini run 1**
- step 0[0] condition.if: Right = 'a number', expected 'number'
- got step 0: `condition.if(Left="%value%",Operator="is",Right="a number"){"write out "numeric"": output.write(Data="numeric")}`



---

# Run 2 — after PF1-PF4 (prompt at `b5bfe3b3a`)

Ingi: *"Accuracy on the mapping of programmers intent to execution path is the most important thing."* So the numbers below are the measured ones, and every miss matters.

- **Run:** `tools/decider/runs/child_eval_20260925_100811/`, the same 20 cases / 28 steps, 3 runs per model, 120 calls, all answered.
- **What changed in the prompt:**
  - PF1: one entry per step, holding only its own words; a lone `else` has no child.
  - PF2: true/false/null/[…] as literals.
  - PF4: each branch has its own child, and a branch's `write to` belongs to it.
  - PF3: every menu action's `os/system/modules/<m>/<a>.notes.md`, printed under that action, in both the template and python. Parity was re-checked against the real template render and holds, except F2's enum default.
  - The notes that now reach stage 3 were corrected where they contradicted the prompt (the old `parameter` key in goal.call's example, `json` → list/dict in list.add, dead references in variable.set; `is` row in if.notes).
- **`Right: null` stays expected** for `== null` (Ingi).

## Measured

| | gpt-5.4-nano | gpt-5.4-mini |
|---|---|---|
| steps right, run 1 → **run 2** | 59/84 → **73/84** (87%) | 63/84 → **78/84** (93%) |
| cases right 3/3, run 2 | 18/20 | 17/20 |
| body in `child`, 15 inline-condition cases | **45/45** | 43/45 (1 if lost its body; 1 nest came back flat) |
| separate steps kept separate | lone-condition layout: 2/6 runs | MenuModule: 1/3 runs |
| no invented child on plain steps | 9/9 | 9/9 |

Added case `complementary_steps` (two separate if steps with complementary conditions) after this run, because mini merged MenuModule's steps 2-3 into one if/else or two ifs. Measured alone with the same prompt (`runs/child_eval_20260925_100903/`): **nano 3/3, mini 3/3**. So mini's merge depends on MenuModule's context, not on the pattern itself.

## Before / after per case (runs right out of 3)

| case | nano before | nano after | mini before | mini after |
|---|---|---|---|---|
| if_return | 3/3 | 3/3 | 3/3 | 3/3 |
| if_call_else_call | 3/3 | 3/3 | 1/3 | 2/3 |
| if_elseif_else_line | 3/3 | 3/3 | 3/3 | 3/3 |
| if_elseif_no_else | 3/3 | 3/3 | 3/3 | 3/3 |
| else_return | 2/3 | 3/3 | 3/3 | 3/3 |
| negate_contains | 3/3 | 3/3 | 3/3 | 3/3 |
| negate_isempty | 0/3 | 3/3 | 3/3 | 3/3 |
| setup_before_if | 3/3 | 3/3 | 3/3 | 3/3 |
| body_with_write_to | 2/3 | 3/3 | 0/3 | 3/3 |
| body_two_actions | 3/3 | 3/3 | 0/3 | 3/3 |
| op_in | 1/3 | 3/3 | 3/3 | 3/3 |
| op_startswith | 3/3 | 3/3 | 3/3 | 3/3 |
| op_is_type | 3/3 | 3/3 | 2/3 | 3/3 |
| menumodule | 2/3 | 3/3 | 3/3 | 1/3 |
| plain_write | 3/3 | 3/3 | 3/3 | 3/3 |
| plain_call_args | 3/3 | 3/3 | 3/3 | 3/3 |
| plain_add | 3/3 | 3/3 | 3/3 | 3/3 |
| indented_body | 0/3 | 2/3 | 3/3 | 3/3 |
| indented_if_else | 0/3 | 0/3 | 0/3 | 3/3 |
| nested_inline | 3/3 | 3/3 | 3/3 | 2/3 |

## Every miss in run 2, judged prompt-first

| Class | Model / runs | What happened | Cause | Proposed fix |
|---|---|---|---|---|
| **N1: lone-condition layout, steps pulled in and RENUMBERED** | nano `indented_if_else` 3/3, `indented_body` 1/3 (11 step-runs) | For `step 0: if %x% == 1` / `step 1: write out "one"` / `step 2: else` / `step 3: write out "other"`, the model nested step 1 into step 0's `child`, then **renumbered**: runs 1/3 answer `index 1` = `else` and drop the last step; run 2 folds everything into step 0. | **Prompt, most likely.** The Conditions sentence "a condition whose step names nothing gets no `child`: **its body is written as indented steps below it**, and those are placed for you" tells the model that the body IS the steps below, while it can't see indentation. PF1's rule is there, but no worked example shows a lone-condition step. mini now gets this 3/3. | **PF5:** replace the sentence with "A step that is only a condition (`if %x% > 0` alone, `else` alone) is just that condition, with no `child`; the steps after it are their own entries." Add one worked example of a lone `if` step followed by an ordinary step (different content from the golden set). Do NOT mention indentation, since the model can't see it. |
| **N2: renumbering is silent at build time** | follows from N1 | `Apply` grafts `%properties.step[step.Index].action%` by index (`BuildGoal/Start.goal`, Settle → Apply). A renumbered answer puts one step's actions on another step and still validates when the shapes happen to fit: here `else` would land on `write out "one"`. | **Builder**, not the prompt. | **PF6 (builder):** the stage-3 answer echoes each step's `text`; Apply (or `build.validate`) refuses an entry whose text doesn't match the goal's step at that index, and refuses a missing or duplicate index. That turns a silent wrong mapping into a fix-and-retry through FixProperties. |
| **N3: separate steps merged** | mini `menumodule` 2/3 (steps 2-3 → one if/else, or two ifs in step 2; step 3 missing) | see above | **Model.** PF1 says "never move another step's work into it", and the isolated `complementary_steps` passes 3/3. N2's check would catch it at build (step 3 missing). | PF6 catches it. |
| **N4: an if lost its body** | mini `if_call_else_call` 1/3 | `if` without child, `else{call Deactivate}`; "call Activate" gone | **Model.** The second worked example is this exact shape. | — |
| **N5: nest came back flat** | mini `nested_inline` 1/3 | `if a>0` ; `if b>0{call}` as siblings | **Model.** "never beside the condition" is in the prompt. The flat form means the second `if` runs as the first one's else-chain: wrong behaviour. | — |

**Model-caused misses in run 2:** nano 0 of 11; mini 6 of 6 (N3 4 step-runs, N4 1, N5 1).

## Reading it

- **mini is ahead on the measurement (78 vs 73).**
- **nano's misses all sit in one layout** (a condition alone on its step, with the body on the following steps). I judge that layout prompt-caused (N1).
- **mini's misses are the model's own.** They are scattered (merge, lost body, flat nest).
- Before choosing, I'd apply PF5 (prompt) and PF6 (builder check) and measure once more. That's Ingi's call, since PF6 is a builder change.

## Run 2 — table and every miss

STEP TOTALS: gpt-5.4-nano 73/84, gpt-5.4-mini 78/84

| case | step | expected | gpt-5.4-nano got ×3 | gpt-5.4-nano | gpt-5.4-mini got ×3 | gpt-5.4-mini |
|---|---|---|---|---|---|---|
| if_return | 0 | `condition.if(Left="%count%",Operator="<",Right=3){"return": goal.return()}` | 1: ✓<br>2: ✓<br>3: ✓ | YES | 1: ✓<br>2: ✓<br>3: ✓ | YES |
| if_call_else_call | 0 | `condition.if(Left="%status%",Operator="==",Right="active"){"call Activate": goal.call(Name="Activate")} ; condition.else(){"call Deactivate": goal.call(Name="Deactivate")}` | 1: ✓<br>2: ✓<br>3: ✓ | YES | 1: ✓<br>2: ✗ `condition.if(Left="%status%",Operator="==",Right="active") ; condition.else(){"call Deactivate": goal.call(Name="Deactivate")}`<br>3: ✓ | NO (2/3) |
| if_elseif_else_line | 0 | `condition.if(Left="%score%",Operator=">",Right=90){"write out "A"": output.write(Data="A")} ; condition.elseif(Left="%score%",Operator=">",Right=70){"write out "B"": output.write(Data="B")} ; condition.else(){"write out "C"": output.write(Data="C")}` | 1: ✓<br>2: ✓<br>3: ✓ | YES | 1: ✓<br>2: ✓<br>3: ✓ | YES |
| if_elseif_no_else | 0 | `condition.if(Left="%lang%",Operator="==",Right="is"){"write out "Halló"": output.write(Data="Halló")} ; condition.elseif(Left="%lang%",Operator="==",Right="en"){"write out "Hello"": output.write(Data="Hello")}` | 1: ✓<br>2: ✓<br>3: ✓ | YES | 1: ✓<br>2: ✓<br>3: ✓ | YES |
| else_return | 0 | `condition.if(Left="%ok%",Operator="==",Right=true){"call Continue": goal.call(Name="Continue")} ; condition.else(){"return": goal.return()}` | 1: ✓<br>2: ✓<br>3: ✓ | YES | 1: ✓<br>2: ✓<br>3: ✓ | YES |
| negate_contains | 0 | `condition.if(Left="%name%",Operator="contains",Right="admin",Negate=true){"call Deny": goal.call(Name="Deny")}` | 1: ✓<br>2: ✓<br>3: ✓ | YES | 1: ✓<br>2: ✓<br>3: ✓ | YES |
| negate_isempty | 0 | `condition.if(Left="%items%",Operator="isempty",Negate=true){"call ProcessItems": goal.call(Name="ProcessItems")}` | 1: ✓<br>2: ✓<br>3: ✓ | YES | 1: ✓<br>2: ✓<br>3: ✓ | YES |
| setup_before_if | 0 | `list.count(ListName="%items%") ; variable.set(Name="%n%",Value="%!data%") ; condition.if(Left="%n%",Operator=">",Right=10){"call Paginate": goal.call(Name="Paginate")}` | 1: ✓<br>2: ✓<br>3: ✓ | YES | 1: ✓<br>2: ✓<br>3: ✓ | YES |
| body_with_write_to | 0 | `condition.if(Left="%price%",Operator=">",Right=100){"multiply %price% by 0.9, write to %price%": math.multiply(A="%price%",B=0.9); variable.set(Name="%price%",Value="%!data%")}` | 1: ✓<br>2: ✓<br>3: ✓ | YES | 1: ✓<br>2: ✓<br>3: ✓ | YES |
| body_two_actions | 0 | `condition.if(Left="%user%",Operator="==",Right=null){"write out "no user" and return": output.write(Data="no user"); goal.return()}` | 1: ✓<br>2: ✓<br>3: ✓ | YES | 1: ✓<br>2: ✓<br>3: ✓ | YES |
| op_in | 0 | `condition.if(Left="%role%",Operator="in",Right=["admin", "owner"]){"call GrantAccess": goal.call(Name="GrantAccess")}` | 1: ✓<br>2: ✓<br>3: ✓ | YES | 1: ✓<br>2: ✓<br>3: ✓ | YES |
| op_startswith | 0 | `condition.if(Left="%path%",Operator="startswith",Right="/api"){"call HandleApi path=%path%": goal.call(Name="HandleApi",Parameter=[{"name": "path", "value": "%path%"}])}` | 1: ✓<br>2: ✓<br>3: ✓ | YES | 1: ✓<br>2: ✓<br>3: ✓ | YES |
| op_is_type | 0 | `condition.if(Left="%value%",Operator="is",Right="number"){"write out "numeric"": output.write(Data="numeric")}` | 1: ✓<br>2: ✓<br>3: ✓ | YES | 1: ✓<br>2: ✓<br>3: ✓ | YES |
| menumodule | 0 | `variable.set(Name="%key%",Value="s%step.Index%_%module.Name%")` | 1: ✓<br>2: ✓<br>3: ✓ | YES | 1: ✓<br>2: ✓<br>3: ✓ | YES |
| menumodule | 1 | `condition.if(Left="%moduleAnswer[key].noul%",Operator="<",Right="%threshold%"){"return": goal.return()}` | 1: ✓<br>2: ✓<br>3: ✓ | YES | 1: ✓<br>2: ✓<br>3: ✓ | YES |
| menumodule | 2 | `condition.if(Left="%module.Action.Count%",Operator="==",Right=1){"add "%module.Name%.%module.Action[0].Name%" to %choices%": list.add(ListName="%choices%",Value="%module.Name%.%module.Action[0].Name%")}` | 1: ✓<br>2: ✓<br>3: ✓ | YES | 1: ✗ `condition.if(Left="%module.Action.Count%",Operator="==",Right=1){"add "%module.Name%.%module.Action[0].Name%" to %choices%": list.add(ListName="%choices%",Value="%module.Name%.%module.Action[0].Name%")} ; condition.else(){"add "%module.Name%.%actionAnswer[key].choice%" to %choices%": list.add(ListName="%choices%",Value="%module.Name%.%actionAnswer[key].choice%")}`<br>2: ✗ `condition.if(Left="%module.Action.Count%",Operator="==",Right=1){"add "%module.Name%.%module.Action[0].Name%" to %choices%": list.add(ListName="%choices%",Value="%module.Name%.%module.Action[0].Name%")} ; condition.if(Left="%module.Action.Count%",Operator=">",Right=1){"add "%module.Name%.%actionAnswer[key].choice%" to %choices%": list.add(ListName="%choices%",Value="%module.Name%.%actionAnswer[key].choice%")}`<br>3: ✓ | NO (1/3) |
| menumodule | 3 | `condition.if(Left="%module.Action.Count%",Operator=">",Right=1){"add "%module.Name%.%actionAnswer[key].choice%" to %choices%": list.add(ListName="%choices%",Value="%module.Name%.%actionAnswer[key].choice%")}` | 1: ✓<br>2: ✓<br>3: ✓ | YES | 1: ✗ `(missing)`<br>2: ✗ `(missing)`<br>3: ✓ | NO (1/3) |
| plain_write | 0 | `output.write(Data="hello")` | 1: ✓<br>2: ✓<br>3: ✓ | YES | 1: ✓<br>2: ✓<br>3: ✓ | YES |
| plain_call_args | 0 | `goal.call(Name="SendMail",Parameter=[{"name": "to", "value": "%email%"}, {"name": "subject", "value": "Welcome"}])` | 1: ✓<br>2: ✓<br>3: ✓ | YES | 1: ✓<br>2: ✓<br>3: ✓ | YES |
| plain_add | 0 | `list.add(ListName="%items%",Value="%item%")` | 1: ✓<br>2: ✓<br>3: ✓ | YES | 1: ✓<br>2: ✓<br>3: ✓ | YES |
| indented_body | 0 | `condition.if(Left="%count%",Operator=">",Right=0)` | 1: ✓<br>2: ✓<br>3: ✗ `condition.if(Left="%count%",Operator=">",Right=0){"call ProcessItems": goal.call(Name="ProcessItems")}` | NO (2/3) | 1: ✓<br>2: ✓<br>3: ✓ | YES |
| indented_body | 1 | `goal.call(Name="ProcessItems")` | 1: ✓<br>2: ✓<br>3: ✓ | YES | 1: ✓<br>2: ✓<br>3: ✓ | YES |
| indented_body | 2 | `output.write(Data="done")` | 1: ✓<br>2: ✓<br>3: ✓ | YES | 1: ✓<br>2: ✓<br>3: ✓ | YES |
| indented_if_else | 0 | `condition.if(Left="%x%",Operator="==",Right=1)` | 1: ✗ `condition.if(Left="%x%",Operator="==",Right=1){"write out "one"": output.write(Data="one")}`<br>2: ✗ `condition.if(Left="%x%",Operator="==",Right=1){"write out "one"": output.write(Data="one")} ; condition.else(){"write out "other"": output.write(Data="other")}`<br>3: ✗ `condition.if(Left="%x%",Operator="==",Right=1){"write out "one"": output.write(Data="one")}` | NO (0/3) | 1: ✓<br>2: ✓<br>3: ✓ | YES |
| indented_if_else | 1 | `output.write(Data="one")` | 1: ✗ `condition.else()`<br>2: ✗ `(missing)`<br>3: ✗ `condition.else()` | NO (0/3) | 1: ✓<br>2: ✓<br>3: ✓ | YES |
| indented_if_else | 2 | `condition.else()` | 1: ✗ `(missing)`<br>2: ✗ `(missing)`<br>3: ✗ `(missing)` | NO (0/3) | 1: ✓<br>2: ✓<br>3: ✓ | YES |
| indented_if_else | 3 | `output.write(Data="other")` | 1: ✓<br>2: ✗ `(missing)`<br>3: ✓ | NO (2/3) | 1: ✓<br>2: ✓<br>3: ✓ | YES |
| nested_inline | 0 | `condition.if(Left="%a%",Operator=">",Right=0){"if %b% > 0, call BothPositive": condition.if(Left="%b%",Operator=">",Right=0){"call BothPositive": goal.call(Name="BothPositive")}}` | 1: ✓<br>2: ✓<br>3: ✓ | YES | 1: ✓<br>2: ✓<br>3: ✗ `condition.if(Left="%a%",Operator=">",Right=0) ; condition.if(Left="%b%",Operator=">",Right=0){"call BothPositive": goal.call(Name="BothPositive")}` | NO (2/3) |

## Every miss, in full

**if_call_else_call — gpt-5.4-mini run 2**
- step 0[0] child: missing — the body is not in child
- got step 0: `condition.if(Left="%status%",Operator="==",Right="active") ; condition.else(){"call Deactivate": goal.call(Name="Deactivate")}`

**indented_body — gpt-5.4-nano run 3**
- step 0[0] child: invented ([{"text": "call ProcessItems", "action": [{"module": "goal", "name": "call", "property": [{"name": "Name", "type": {"nam)
- got step 0: `condition.if(Left="%count%",Operator=">",Right=0){"call ProcessItems": goal.call(Name="ProcessItems")}`

**indented_if_else — gpt-5.4-nano run 1**
- step 0[0] child: invented ([{"text": "write out \"one\"", "action": [{"module": "output", "name": "write", "property": [{"name": "Data", "type": {")
- got step 0: `condition.if(Left="%x%",Operator="==",Right=1){"write out "one"": output.write(Data="one")}`
- step 1[0]: condition.else, expected output.write
- got step 1: `condition.else()`
- step 2: not in the answer
- got step 2: `(missing)`

**indented_if_else — gpt-5.4-nano run 2**
- step 0: 2 actions, expected 1 (condition.if(Left="%x%",Operator="==",Right=1){"write out "one"": output.write(Data="one")}, condition.else(){"write out "other"": output.write(Data="other")})
- step 0[0] child: invented ([{"text": "write out \"one\"", "action": [{"module": "output", "name": "write", "property": [{"name": "Data", "type": {")
- got step 0: `condition.if(Left="%x%",Operator="==",Right=1){"write out "one"": output.write(Data="one")} ; condition.else(){"write out "other"": output.write(Data="other")}`
- step 1: 0 actions, expected 1 ()
- got step 1: `(missing)`
- step 2: 0 actions, expected 1 ()
- got step 2: `(missing)`
- step 3: not in the answer
- got step 3: `(missing)`

**indented_if_else — gpt-5.4-nano run 3**
- step 0[0] child: invented ([{"text": "write out \"one\"", "action": [{"module": "output", "name": "write", "property": [{"name": "Data", "type": {")
- got step 0: `condition.if(Left="%x%",Operator="==",Right=1){"write out "one"": output.write(Data="one")}`
- step 1[0]: condition.else, expected output.write
- got step 1: `condition.else()`
- step 2: not in the answer
- got step 2: `(missing)`

**menumodule — gpt-5.4-mini run 1**
- step 2: 2 actions, expected 1 (condition.if(Left="%module.Action.Count%",Operator="==",Right=1){"add "%module.Name%.%module.Action[0].Name%" to %choices%": list.add(ListName="%choices%",Value="%module.Name%.%module.Action[0].Name%")}, condition.else(){"add "%module.Name%.%actionAnswer[key].choice%" to %choices%": list.add(ListName="%choices%",Value="%module.Name%.%actionAnswer[key].choice%")})
- got step 2: `condition.if(Left="%module.Action.Count%",Operator="==",Right=1){"add "%module.Name%.%module.Action[0].Name%" to %choices%": list.add(ListName="%choices%",Value="%module.Name%.%module.Action[0].Name%")} ; condition.else(){"add "%module.Name%.%actionAnswer[key].choice%" to %choices%": list.add(ListName="%choices%",Value="%module.Name%.%actionAnswer[key].choice%")}`
- step 3: not in the answer
- got step 3: `(missing)`

**menumodule — gpt-5.4-mini run 2**
- step 2: 2 actions, expected 1 (condition.if(Left="%module.Action.Count%",Operator="==",Right=1){"add "%module.Name%.%module.Action[0].Name%" to %choices%": list.add(ListName="%choices%",Value="%module.Name%.%module.Action[0].Name%")}, condition.if(Left="%module.Action.Count%",Operator=">",Right=1){"add "%module.Name%.%actionAnswer[key].choice%" to %choices%": list.add(ListName="%choices%",Value="%module.Name%.%actionAnswer[key].choice%")})
- got step 2: `condition.if(Left="%module.Action.Count%",Operator="==",Right=1){"add "%module.Name%.%module.Action[0].Name%" to %choices%": list.add(ListName="%choices%",Value="%module.Name%.%module.Action[0].Name%")} ; condition.if(Left="%module.Action.Count%",Operator=">",Right=1){"add "%module.Name%.%actionAnswer[key].choice%" to %choices%": list.add(ListName="%choices%",Value="%module.Name%.%actionAnswer[key].choice%")}`
- step 3: not in the answer
- got step 3: `(missing)`

**nested_inline — gpt-5.4-mini run 3**
- step 0: 2 actions, expected 1 (condition.if(Left="%a%",Operator=">",Right=0), condition.if(Left="%b%",Operator=">",Right=0){"call BothPositive": goal.call(Name="BothPositive")})
- step 0[0] child: missing — the body is not in child
- got step 0: `condition.if(Left="%a%",Operator=">",Right=0) ; condition.if(Left="%b%",Operator=">",Right=0){"call BothPositive": goal.call(Name="BothPositive")}`

