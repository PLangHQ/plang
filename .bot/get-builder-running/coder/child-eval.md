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


---

# Recomputed without `indented_if_else` (no new LLM calls)

Plang has no standalone `- else` step. Else is written inline in the if step (`- if %x% then call A, else B`). So `indented_if_else` tested a construct plang doesn't have, and it is dropped from `tools/decider/child_golden.json`. The sentence teaching a lone `else` step is removed from `Properties.llm`. `indented_body` (a lone `- if X` with indented steps below) is valid plang and stays. Totals recomputed from the same raw answers:

| | run 1 (prompt 4df7f8075) | run 2 (prompt b5bfe3b3a) |
|---|---|---|
| gpt-5.4-nano, steps | 59/72 | **71/72** (98.6%) |
| gpt-5.4-nano, cases right 3/3 | 13/19 | **18/19** |
| gpt-5.4-mini, steps | 63/72 | **66/72** (91.7%) |
| gpt-5.4-mini, cases right 3/3 | 15/19 | **16/19** |

- **nano's one remaining miss:** `indented_body` run 3. The lone `if %count% > 0` got a child copied from the next step; the next step still has its own entry. The wording PF5 addresses (not applied).
- **mini's six** are unchanged (N3-N5 above): MenuModule steps merged (2 runs), an if lost its body, a nest came back flat. All are the model's own.
- With the standalone-else construct gone, **nano measures ahead of mini** on this set.

N2 (a renumbered or merged answer silently grafting one step's actions onto another) is now caught by `build.match` before any step takes its actions (the commit after this recompute).


---

# Run 3: the 100% bar (5 runs per case, 32 cases / 45 steps)

- **Prompt:** frozen at `455fe7fca`. The changes since run 2:
  - the standalone-else teaching;
  - the Order teaching;
  - PF5 plus the parser's `(its body is step N, indented below …)` marker on a step with a body;
  - the step-matching and child-over-indent refusals in `build.match` (not exercised here: stage 3 only).
- **Golden set:** widened with 9 real builder lines and 3 layouts.
- **Raw:** `tools/decider/runs/child_eval_20260925_111557/`. 320 calls, all answered.
- **The bar (Ingi):** 100% on every run. The model is chosen only among those at 100%.

| | gpt-5.4-nano | gpt-5.4-mini |
|---|---|---|
| steps right | **214/225** (95.1%) | 195/225 (86.7%) |
| cases right on all 5 runs | **29/32** | 22/32 |
| **meets the bar** | **no** | **no** |

**Neither model is at 100%.** Every miss is below, judged prompt-first, with a proposed fix. No fix is applied yet (the prompt stayed frozen for this run).

## Cases not at 5/5

| case | nano | mini |
|---|---|---|
| builder_render_template | 0/5 | 5/5 |
| builder_math_write_to | 3/5 | 5/5 |
| indented_two_levels | 4/5 | 5/5 |
| if_call_else_call | 5/5 | 0/5 |
| else_return | 5/5 | 0/5 |
| op_startswith | 5/5 | 0/5 |
| inline_if_then_step | 5/5 | 0/5 |
| body_with_write_to | 5/5 | 2/5 |
| if_return | 5/5 | 3/5 |
| setup_before_if | 5/5 | 3/5 |
| if_elseif_else_line / negate_isempty / op_in | 5/5 | 4/5 |

## Every miss, judged prompt-first

| Class | Model / step-runs | What happened | Cause | Proposed fix |
|---|---|---|---|---|
| **R1: inline body left beside the condition** | mini, 30 step-runs over 11 cases (nano 0) | `if %n% > 5, call Big` → `[if, goal.call]` flat; for if/else: `if` without child, `else{…}` with one. mini got these right in run 2, so this is a **regression**. | **Prompt, most likely.** PF5 ("A step marked … gets no `child`…") is now the LAST worked example before "Filling a property", and mini generalises "no child" to every condition. It only fails on inline cases, and it gets the marked cases right. | **P1:** move PF5 up, so the Conditions section ends on an inline example, and scope it in words: "Only a step marked `(its body is …)` is left without a child; a body written in the step's own words always goes in the child." |
| **R2: placeholders on ui.render** | nano 5/5 on `builder_render_template` | `Parameter: []` and `IsFile: true` added | **Prompt/teaching.** `ui.render` has no notes. `Parameter: []` breaks "never put in a placeholder". `IsFile: true` is behaviour-neutral: IsFile is nullable, and null = auto-detect finds the file. But the step doesn't name it. | **P2:** `os/system/modules/ui/render.notes.md`: "`Parameter` only when the step passes values — never an empty list. `IsFile` is left out: render finds the file itself; write it only when the step says inline / as text." |
| **R3: defaults repeated** | nano 2/5 on `builder_math_write_to` | `math.subtract` with `Overflow: "Promote"`, `Precision: "Error"`, both the defaults | **Model**, against a rule the prompt states ("never repeat a default"). The same pattern appeared in run 1 (math.multiply). | **P3:** make the rule concrete in "Filling a property": "A property the menu shows with a default is left out unless the step's words ask for another value". Plus F2, so plang shows the default's name, not `0`. |
| **R4: nest everything, drop the rest** | nano 1/5 on `indented_two_levels` (4 step-runs) | step 0 got `child: [if %b% > 0 {call, write}]`, and steps 1-3 were dropped | **Model**, despite both markers. **Caught at build:** `build.match` refuses it ("step 1 has no entry…"), and FixProperties retries. Loud, not silent. | — (the check handles it) |

## What the builder already catches, and what it doesn't

| Class | build.match (in the retry) | Silent wrong program if it reached the .pr? |
|---|---|---|
| R4: dropped or renumbered steps, child over indent | **caught** | — |
| R1: body beside the condition | **not caught** | **yes.** `[if, call]` runs the call when the condition is FALSE (the else-chain rule) |
| R2/R3: extra optional properties | not caught | no: same behaviour here (IsFile auto-detect; a repeated default is the default) |

**P4 (builder check), proposed.** The action list refuses an ordinary action written after a condition in the same list. After a condition, only `elseif`/`else` may follow; anything else is the branch's body put beside it. This is the twin of `ElseWithoutIf`, but it's the LLM's error, not the programmer's, so it goes back through FixProperties: "step 0: `goal.call` is after the if — a branch's body goes in its child". That turns R1 from silent into a retry.

## Proposal

Apply P1 (prompt), P2 (render notes), P3 (defaults rule) and P4 (the check), then re-measure 5 runs on both models. nano is the closer of the two: its remaining misses are R2 (5, a teaching gap), R3 (2) and R4 (1, already caught).

## Run 3: table and every miss

STEP TOTALS: gpt-5.4-nano 214/225, gpt-5.4-mini 195/225

| case | step | expected | gpt-5.4-nano got ×5 | gpt-5.4-nano | gpt-5.4-mini got ×5 | gpt-5.4-mini |
|---|---|---|---|---|---|---|
| if_return | 0 | `condition.if(Left="%count%",Operator="<",Right=3){"return": goal.return()}` | 1: ✓<br>2: ✓<br>3: ✓<br>4: ✓<br>5: ✓ | YES | 1: ✗ `condition.if(Left="%count%",Operator="<",Right=3) ; goal.return()`<br>2: ✓<br>3: ✗ `condition.if(Left="%count%",Operator="<",Right=3) ; goal.return()`<br>4: ✓<br>5: ✓ | NO (3/5) |
| if_call_else_call | 0 | `condition.if(Left="%status%",Operator="==",Right="active"){"call Activate": goal.call(Name="Activate")} ; condition.else(){"call Deactivate": goal.call(Name="Deactivate")}` | 1: ✓<br>2: ✓<br>3: ✓<br>4: ✓<br>5: ✓ | YES | 1: ✗ `condition.if(Left="%status%",Operator="==",Right="active") ; condition.else(){"call Deactivate": goal.call(Name="Deactivate")}`<br>2: ✗ `condition.if(Left="%status%",Operator="==",Right="active") ; condition.else(){"call Deactivate": goal.call(Name="Deactivate")}`<br>3: ✗ `condition.if(Left="%status%",Operator="==",Right="active") ; condition.else(){"call Deactivate": goal.call(Name="Deactivate")}`<br>4: ✗ `condition.if(Left="%status%",Operator="==",Right="active") ; condition.else(){"call Deactivate": goal.call(Name="Deactivate")}`<br>5: ✗ `condition.if(Left="%status%",Operator="==",Right="active") ; condition.else(){"call Deactivate": goal.call(Name="Deactivate")}` | NO (0/5) |
| if_elseif_else_line | 0 | `condition.if(Left="%score%",Operator=">",Right=90){"write out "A"": output.write(Data="A")} ; condition.elseif(Left="%score%",Operator=">",Right=70){"write out "B"": output.write(Data="B")} ; condition.else(){"write out "C"": output.write(Data="C")}` | 1: ✓<br>2: ✓<br>3: ✓<br>4: ✓<br>5: ✓ | YES | 1: ✓<br>2: ✓<br>3: ✗ `condition.if(Left="%score%",Operator=">",Right=90) ; condition.elseif(Left="%score%",Operator=">",Right=70) ; condition.else(){"write out "C"": output.write(Data="C")}`<br>4: ✓<br>5: ✓ | NO (4/5) |
| if_elseif_no_else | 0 | `condition.if(Left="%lang%",Operator="==",Right="is"){"write out "Halló"": output.write(Data="Halló")} ; condition.elseif(Left="%lang%",Operator="==",Right="en"){"write out "Hello"": output.write(Data="Hello")}` | 1: ✓<br>2: ✓<br>3: ✓<br>4: ✓<br>5: ✓ | YES | 1: ✓<br>2: ✓<br>3: ✓<br>4: ✓<br>5: ✓ | YES |
| else_return | 0 | `condition.if(Left="%ok%",Operator="==",Right=true){"call Continue": goal.call(Name="Continue")} ; condition.else(){"return": goal.return()}` | 1: ✓<br>2: ✓<br>3: ✓<br>4: ✓<br>5: ✓ | YES | 1: ✗ `condition.if(Left="%ok%",Operator="==",Right=true) ; condition.else(){"return": goal.return()}`<br>2: ✗ `condition.if(Left="%ok%",Operator="==",Right=true) ; condition.else(){"return": goal.return()}`<br>3: ✗ `condition.if(Left="%ok%",Operator="==",Right=true) ; condition.else(){"return": goal.return()}`<br>4: ✗ `condition.if(Left="%ok%",Operator="==",Right=true) ; condition.else(){"return": goal.return()}`<br>5: ✗ `condition.if(Left="%ok%",Operator="==",Right=true) ; condition.else(){"return": goal.return()}` | NO (0/5) |
| negate_contains | 0 | `condition.if(Left="%name%",Operator="contains",Right="admin",Negate=true){"call Deny": goal.call(Name="Deny")}` | 1: ✓<br>2: ✓<br>3: ✓<br>4: ✓<br>5: ✓ | YES | 1: ✓<br>2: ✓<br>3: ✓<br>4: ✓<br>5: ✓ | YES |
| negate_isempty | 0 | `condition.if(Left="%items%",Operator="isempty",Negate=true){"call ProcessItems": goal.call(Name="ProcessItems")}` | 1: ✓<br>2: ✓<br>3: ✓<br>4: ✓<br>5: ✓ | YES | 1: ✓<br>2: ✓<br>3: ✓<br>4: ✓<br>5: ✗ `condition.if(Left="%items%",Operator="isempty",Negate=true) ; goal.call(Name="ProcessItems")` | NO (4/5) |
| setup_before_if | 0 | `list.count(ListName="%items%") ; variable.set(Name="%n%",Value="%!data%") ; condition.if(Left="%n%",Operator=">",Right=10){"call Paginate": goal.call(Name="Paginate")}` | 1: ✓<br>2: ✓<br>3: ✓<br>4: ✓<br>5: ✓ | YES | 1: ✓<br>2: ✗ `list.count(ListName="%items%") ; variable.set(Name="%n%",Value="%!data%") ; condition.if(Left="%n%",Operator=">",Right=10) ; goal.call(Name="Paginate")`<br>3: ✗ `list.count(ListName="%items%") ; variable.set(Name="%n%",Value="%!data%") ; condition.if(Left="%n%",Operator=">",Right=10) ; goal.call(Name="Paginate")`<br>4: ✓<br>5: ✓ | NO (3/5) |
| body_with_write_to | 0 | `condition.if(Left="%price%",Operator=">",Right=100){"multiply %price% by 0.9, write to %price%": math.multiply(A="%price%",B=0.9); variable.set(Name="%price%",Value="%!data%")}` | 1: ✓<br>2: ✓<br>3: ✓<br>4: ✓<br>5: ✓ | YES | 1: ✗ `condition.if(Left="%price%",Operator=">",Right=100) ; math.multiply(A="%price%",B=0.9)`<br>2: ✓<br>3: ✗ `condition.if(Left="%price%",Operator=">",Right=100) ; math.multiply(A="%price%",B=0.9) ; variable.set(Name="%price%",Value="%!data%")`<br>4: ✗ `condition.if(Left="%price%",Operator=">",Right=100) ; math.multiply(A="%price%",B=0.9)`<br>5: ✓ | NO (2/5) |
| body_two_actions | 0 | `condition.if(Left="%user%",Operator="==",Right=null){"write out "no user" and return": output.write(Data="no user"); goal.return()}` | 1: ✓<br>2: ✓<br>3: ✓<br>4: ✓<br>5: ✓ | YES | 1: ✓<br>2: ✓<br>3: ✓<br>4: ✓<br>5: ✓ | YES |
| op_in | 0 | `condition.if(Left="%role%",Operator="in",Right=["admin", "owner"]){"call GrantAccess": goal.call(Name="GrantAccess")}` | 1: ✓<br>2: ✓<br>3: ✓<br>4: ✓<br>5: ✓ | YES | 1: ✓<br>2: ✓<br>3: ✓<br>4: ✓<br>5: ✗ `condition.if(Left="%role%",Operator="in",Right=["admin", "owner"]) ; goal.call(Name="GrantAccess")` | NO (4/5) |
| op_startswith | 0 | `condition.if(Left="%path%",Operator="startswith",Right="/api"){"call HandleApi path=%path%": goal.call(Name="HandleApi",Parameter=[{"name": "path", "value": "%path%"}])}` | 1: ✓<br>2: ✓<br>3: ✓<br>4: ✓<br>5: ✓ | YES | 1: ✗ `condition.if(Left="%path%",Operator="startswith",Right="/api") ; goal.call(Name="HandleApi",Parameter=[{"name": "path", "type": {"name": "item"}, "value": "%path%"}])`<br>2: ✗ `condition.if(Left="%path%",Operator="startswith",Right="/api") ; goal.call(Name="HandleApi",Parameter=[{"name": "path", "type": {"name": "item"}, "value": "%path%"}])`<br>3: ✗ `condition.if(Left="%path%",Operator="startswith",Right="/api") ; goal.call(Name="HandleApi",Parameter=[{"name": "path", "type": {"name": "item"}, "value": "%path%"}])`<br>4: ✗ `condition.if(Left="%path%",Operator="startswith",Right="/api") ; goal.call(Name="HandleApi",Parameter=[{"name": "path", "type": {"name": "item"}, "value": "%path%"}])`<br>5: ✗ `condition.if(Left="%path%",Operator="startswith",Right="/api") ; goal.call(Name="HandleApi",Parameter=[{"name": "path", "type": {"name": "item"}, "value": "%path%"}])` | NO (0/5) |
| op_is_type | 0 | `condition.if(Left="%value%",Operator="is",Right="number"){"write out "numeric"": output.write(Data="numeric")}` | 1: ✓<br>2: ✓<br>3: ✓<br>4: ✓<br>5: ✓ | YES | 1: ✓<br>2: ✓<br>3: ✓<br>4: ✓<br>5: ✓ | YES |
| menumodule | 0 | `variable.set(Name="%key%",Value="s%step.Index%_%module.Name%")` | 1: ✓<br>2: ✓<br>3: ✓<br>4: ✓<br>5: ✓ | YES | 1: ✓<br>2: ✓<br>3: ✓<br>4: ✓<br>5: ✓ | YES |
| menumodule | 1 | `condition.if(Left="%moduleAnswer[key].noul%",Operator="<",Right="%threshold%"){"return": goal.return()}` | 1: ✓<br>2: ✓<br>3: ✓<br>4: ✓<br>5: ✓ | YES | 1: ✓<br>2: ✓<br>3: ✓<br>4: ✓<br>5: ✓ | YES |
| menumodule | 2 | `condition.if(Left="%module.Action.Count%",Operator="==",Right=1){"add "%module.Name%.%module.Action[0].Name%" to %choices%": list.add(ListName="%choices%",Value="%module.Name%.%module.Action[0].Name%")}` | 1: ✓<br>2: ✓<br>3: ✓<br>4: ✓<br>5: ✓ | YES | 1: ✓<br>2: ✓<br>3: ✓<br>4: ✓<br>5: ✓ | YES |
| menumodule | 3 | `condition.if(Left="%module.Action.Count%",Operator=">",Right=1){"add "%module.Name%.%actionAnswer[key].choice%" to %choices%": list.add(ListName="%choices%",Value="%module.Name%.%actionAnswer[key].choice%")}` | 1: ✓<br>2: ✓<br>3: ✓<br>4: ✓<br>5: ✓ | YES | 1: ✓<br>2: ✓<br>3: ✓<br>4: ✓<br>5: ✓ | YES |
| complementary_steps | 0 | `condition.if(Left="%n%",Operator="==",Right=0){"write out "none"": output.write(Data="none")}` | 1: ✓<br>2: ✓<br>3: ✓<br>4: ✓<br>5: ✓ | YES | 1: ✓<br>2: ✓<br>3: ✓<br>4: ✓<br>5: ✓ | YES |
| complementary_steps | 1 | `condition.if(Left="%n%",Operator=">",Right=0){"write out "some"": output.write(Data="some")}` | 1: ✓<br>2: ✓<br>3: ✓<br>4: ✓<br>5: ✓ | YES | 1: ✓<br>2: ✓<br>3: ✓<br>4: ✓<br>5: ✓ | YES |
| builder_set_default | 0 | `variable.set(Name="%path%",Value="/",AsDefault=true)` | 1: ✓<br>2: ✓<br>3: ✓<br>4: ✓<br>5: ✓ | YES | 1: ✓<br>2: ✓<br>3: ✓<br>4: ✓<br>5: ✓ | YES |
| builder_set_number | 0 | `variable.set(Name="%threshold%",Value=0.5)` | 1: ✓<br>2: ✓<br>3: ✓<br>4: ✓<br>5: ✓ | YES | 1: ✓<br>2: ✓<br>3: ✓<br>4: ✓<br>5: ✓ | YES |
| builder_set_empty_list | 0 | `variable.set(Name="%choices%",Value=[])` | 1: ✓<br>2: ✓<br>3: ✓<br>4: ✓<br>5: ✓ | YES | 1: ✓<br>2: ✓<br>3: ✓<br>4: ✓<br>5: ✓ | YES |
| builder_set_indexed | 0 | `variable.set(Name="%menu[step.Index]%",Value="%choices%")` | 1: ✓<br>2: ✓<br>3: ✓<br>4: ✓<br>5: ✓ | YES | 1: ✓<br>2: ✓<br>3: ✓<br>4: ✓<br>5: ✓ | YES |
| builder_math_write_to | 0 | `math.subtract(A="%Now.Ticks%",B="%buildStart%") ; variable.set(Name="%elapsedTicks%",Value="%!data%")` | 1: ✓<br>2: ✗ `math.subtract(A="%Now.Ticks%",B="%buildStart%",Overflow="Promote",Precision="Error") ; variable.set(Name="%elapsedTicks%",Value="%!data%")`<br>3: ✓<br>4: ✓<br>5: ✗ `math.subtract(A="%Now.Ticks%",B="%buildStart%",Overflow="Promote",Precision="Error") ; variable.set(Name="%elapsedTicks%",Value="%!data%")` | NO (3/5) | 1: ✓<br>2: ✓<br>3: ✓<br>4: ✓<br>5: ✓ | YES |
| builder_render_template | 0 | `ui.render(Template="/system/builder/llm/templates/propertiesUser.template") ; variable.set(Name="%propertiesUserMsg%",Value="%!data%")` | 1: ✗ `ui.render(Template="/system/builder/llm/templates/propertiesUser.template",Parameter=[],IsFile=true) ; variable.set(Name="%propertiesUserMsg%",Value="%!data%")`<br>2: ✗ `ui.render(Template="/system/builder/llm/templates/propertiesUser.template",Parameter=[],IsFile=true) ; variable.set(Name="%propertiesUserMsg%",Value="%!data%")`<br>3: ✗ `ui.render(Template="/system/builder/llm/templates/propertiesUser.template",Parameter=[]) ; variable.set(Name="%propertiesUserMsg%",Value="%!data%")`<br>4: ✗ `ui.render(Template="/system/builder/llm/templates/propertiesUser.template",Parameter=[],IsFile=true) ; variable.set(Name="%propertiesUserMsg%",Value="%!data%")`<br>5: ✗ `ui.render(Template="/system/builder/llm/templates/propertiesUser.template",Parameter=[],IsFile=true) ; variable.set(Name="%propertiesUserMsg%",Value="%!data%")` | NO (0/5) | 1: ✓<br>2: ✓<br>3: ✓<br>4: ✓<br>5: ✓ | YES |
| builder_decider | 0 | `llm.decider(State="%state%",Question="%moduleQuestions%") ; variable.set(Name="%moduleAnswer%",Value="%!data%")` | 1: ✓<br>2: ✓<br>3: ✓<br>4: ✓<br>5: ✓ | YES | 1: ✓<br>2: ✓<br>3: ✓<br>4: ✓<br>5: ✓ | YES |
| builder_save_file | 0 | `file.save(Path="/.build/traces/%!trace.id%/%goal.Name%.json",Value="%trace%")` | 1: ✓<br>2: ✓<br>3: ✓<br>4: ✓<br>5: ✓ | YES | 1: ✓<br>2: ✓<br>3: ✓<br>4: ✓<br>5: ✓ | YES |
| builder_add_to_list | 0 | `list.add(ListName="%traceGoals%",Value="%goal.Name%")` | 1: ✓<br>2: ✓<br>3: ✓<br>4: ✓<br>5: ✓ | YES | 1: ✓<br>2: ✓<br>3: ✓<br>4: ✓<br>5: ✓ | YES |
| indented_two_levels | 0 | `condition.if(Left="%a%",Operator=">",Right=0)` | 1: ✓<br>2: ✓<br>3: ✗ `condition.if(Left="%a%",Operator=">",Right=0){"if %b% > 0": condition.if(Left="%b%",Operator=">",Right=0){"call BothPositive": goal.call(Name="BothPositive") \| "write out "checked"": output.write(Data="checked")}}`<br>4: ✓<br>5: ✓ | NO (4/5) | 1: ✓<br>2: ✓<br>3: ✓<br>4: ✓<br>5: ✓ | YES |
| indented_two_levels | 1 | `condition.if(Left="%b%",Operator=">",Right=0)` | 1: ✓<br>2: ✓<br>3: ✗ `(missing)`<br>4: ✓<br>5: ✓ | NO (4/5) | 1: ✓<br>2: ✓<br>3: ✓<br>4: ✓<br>5: ✓ | YES |
| indented_two_levels | 2 | `goal.call(Name="BothPositive")` | 1: ✓<br>2: ✓<br>3: ✗ `(missing)`<br>4: ✓<br>5: ✓ | NO (4/5) | 1: ✓<br>2: ✓<br>3: ✓<br>4: ✓<br>5: ✓ | YES |
| indented_two_levels | 3 | `output.write(Data="checked")` | 1: ✓<br>2: ✓<br>3: ✗ `(missing)`<br>4: ✓<br>5: ✓ | NO (4/5) | 1: ✓<br>2: ✓<br>3: ✓<br>4: ✓<br>5: ✓ | YES |
| indented_then_outdented | 0 | `condition.if(Left="%retries%",Operator=">",Right=3)` | 1: ✓<br>2: ✓<br>3: ✓<br>4: ✓<br>5: ✓ | YES | 1: ✓<br>2: ✓<br>3: ✓<br>4: ✓<br>5: ✓ | YES |
| indented_then_outdented | 1 | `output.write(Data="giving up")` | 1: ✓<br>2: ✓<br>3: ✓<br>4: ✓<br>5: ✓ | YES | 1: ✓<br>2: ✓<br>3: ✓<br>4: ✓<br>5: ✓ | YES |
| indented_then_outdented | 2 | `goal.return()` | 1: ✓<br>2: ✓<br>3: ✓<br>4: ✓<br>5: ✓ | YES | 1: ✓<br>2: ✓<br>3: ✓<br>4: ✓<br>5: ✓ | YES |
| indented_then_outdented | 3 | `output.write(Data="trying again")` | 1: ✓<br>2: ✓<br>3: ✓<br>4: ✓<br>5: ✓ | YES | 1: ✓<br>2: ✓<br>3: ✓<br>4: ✓<br>5: ✓ | YES |
| inline_if_then_step | 0 | `condition.if(Left="%n%",Operator=">",Right=5){"call Big": goal.call(Name="Big")}` | 1: ✓<br>2: ✓<br>3: ✓<br>4: ✓<br>5: ✓ | YES | 1: ✗ `condition.if(Left="%n%",Operator=">",Right=5) ; goal.call(Name="Big")`<br>2: ✗ `condition.if(Left="%n%",Operator=">",Right=5) ; goal.call(Name="Big")`<br>3: ✗ `condition.if(Left="%n%",Operator=">",Right=5) ; goal.call(Name="Big")`<br>4: ✗ `condition.if(Left="%n%",Operator=">",Right=5) ; goal.call(Name="Big")`<br>5: ✗ `condition.if(Left="%n%",Operator=">",Right=5) ; goal.call(Name="Big")` | NO (0/5) |
| inline_if_then_step | 1 | `list.count(ListName="%items%") ; variable.set(Name="%n%",Value="%!data%")` | 1: ✓<br>2: ✓<br>3: ✓<br>4: ✓<br>5: ✓ | YES | 1: ✓<br>2: ✓<br>3: ✓<br>4: ✓<br>5: ✓ | YES |
| plain_write | 0 | `output.write(Data="hello")` | 1: ✓<br>2: ✓<br>3: ✓<br>4: ✓<br>5: ✓ | YES | 1: ✓<br>2: ✓<br>3: ✓<br>4: ✓<br>5: ✓ | YES |
| plain_call_args | 0 | `goal.call(Name="SendMail",Parameter=[{"name": "to", "value": "%email%"}, {"name": "subject", "value": "Welcome"}])` | 1: ✓<br>2: ✓<br>3: ✓<br>4: ✓<br>5: ✓ | YES | 1: ✓<br>2: ✓<br>3: ✓<br>4: ✓<br>5: ✓ | YES |
| plain_add | 0 | `list.add(ListName="%items%",Value="%item%")` | 1: ✓<br>2: ✓<br>3: ✓<br>4: ✓<br>5: ✓ | YES | 1: ✓<br>2: ✓<br>3: ✓<br>4: ✓<br>5: ✓ | YES |
| indented_body | 0 | `condition.if(Left="%count%",Operator=">",Right=0)` | 1: ✓<br>2: ✓<br>3: ✓<br>4: ✓<br>5: ✓ | YES | 1: ✓<br>2: ✓<br>3: ✓<br>4: ✓<br>5: ✓ | YES |
| indented_body | 1 | `goal.call(Name="ProcessItems")` | 1: ✓<br>2: ✓<br>3: ✓<br>4: ✓<br>5: ✓ | YES | 1: ✓<br>2: ✓<br>3: ✓<br>4: ✓<br>5: ✓ | YES |
| indented_body | 2 | `output.write(Data="done")` | 1: ✓<br>2: ✓<br>3: ✓<br>4: ✓<br>5: ✓ | YES | 1: ✓<br>2: ✓<br>3: ✓<br>4: ✓<br>5: ✓ | YES |
| nested_inline | 0 | `condition.if(Left="%a%",Operator=">",Right=0){"if %b% > 0, call BothPositive": condition.if(Left="%b%",Operator=">",Right=0){"call BothPositive": goal.call(Name="BothPositive")}}` | 1: ✓<br>2: ✓<br>3: ✓<br>4: ✓<br>5: ✓ | YES | 1: ✓<br>2: ✓<br>3: ✓<br>4: ✓<br>5: ✓ | YES |

## Every miss, in full

**body_with_write_to — gpt-5.4-mini run 1**
- step 0: 2 actions, expected 1 (condition.if(Left="%price%",Operator=">",Right=100), math.multiply(A="%price%",B=0.9))
- step 0[0] child: missing — the body is not in child
- got step 0: `condition.if(Left="%price%",Operator=">",Right=100) ; math.multiply(A="%price%",B=0.9)`

**body_with_write_to — gpt-5.4-mini run 3**
- step 0: 3 actions, expected 1 (condition.if(Left="%price%",Operator=">",Right=100), math.multiply(A="%price%",B=0.9), variable.set(Name="%price%",Value="%!data%"))
- step 0[0] child: missing — the body is not in child
- got step 0: `condition.if(Left="%price%",Operator=">",Right=100) ; math.multiply(A="%price%",B=0.9) ; variable.set(Name="%price%",Value="%!data%")`

**body_with_write_to — gpt-5.4-mini run 4**
- step 0: 2 actions, expected 1 (condition.if(Left="%price%",Operator=">",Right=100), math.multiply(A="%price%",B=0.9))
- step 0[0] child: missing — the body is not in child
- got step 0: `condition.if(Left="%price%",Operator=">",Right=100) ; math.multiply(A="%price%",B=0.9)`

**builder_math_write_to — gpt-5.4-nano run 2**
- step 0[0] math.subtract: extra Overflow = 'Promote'
- step 0[0] math.subtract: extra Precision = 'Error'
- got step 0: `math.subtract(A="%Now.Ticks%",B="%buildStart%",Overflow="Promote",Precision="Error") ; variable.set(Name="%elapsedTicks%",Value="%!data%")`

**builder_math_write_to — gpt-5.4-nano run 5**
- step 0[0] math.subtract: extra Overflow = 'Promote'
- step 0[0] math.subtract: extra Precision = 'Error'
- got step 0: `math.subtract(A="%Now.Ticks%",B="%buildStart%",Overflow="Promote",Precision="Error") ; variable.set(Name="%elapsedTicks%",Value="%!data%")`

**builder_render_template — gpt-5.4-nano run 1**
- step 0[0] ui.render: extra Parameter = []
- step 0[0] ui.render: extra IsFile = True
- got step 0: `ui.render(Template="/system/builder/llm/templates/propertiesUser.template",Parameter=[],IsFile=true) ; variable.set(Name="%propertiesUserMsg%",Value="%!data%")`

**builder_render_template — gpt-5.4-nano run 2**
- step 0[0] ui.render: extra Parameter = []
- step 0[0] ui.render: extra IsFile = True
- got step 0: `ui.render(Template="/system/builder/llm/templates/propertiesUser.template",Parameter=[],IsFile=true) ; variable.set(Name="%propertiesUserMsg%",Value="%!data%")`

**builder_render_template — gpt-5.4-nano run 3**
- step 0[0] ui.render: extra Parameter = []
- got step 0: `ui.render(Template="/system/builder/llm/templates/propertiesUser.template",Parameter=[]) ; variable.set(Name="%propertiesUserMsg%",Value="%!data%")`

**builder_render_template — gpt-5.4-nano run 4**
- step 0[0] ui.render: extra Parameter = []
- step 0[0] ui.render: extra IsFile = True
- got step 0: `ui.render(Template="/system/builder/llm/templates/propertiesUser.template",Parameter=[],IsFile=true) ; variable.set(Name="%propertiesUserMsg%",Value="%!data%")`

**builder_render_template — gpt-5.4-nano run 5**
- step 0[0] ui.render: extra Parameter = []
- step 0[0] ui.render: extra IsFile = True
- got step 0: `ui.render(Template="/system/builder/llm/templates/propertiesUser.template",Parameter=[],IsFile=true) ; variable.set(Name="%propertiesUserMsg%",Value="%!data%")`

**else_return — gpt-5.4-mini run 1**
- step 0[0] child: missing — the body is not in child
- got step 0: `condition.if(Left="%ok%",Operator="==",Right=true) ; condition.else(){"return": goal.return()}`

**else_return — gpt-5.4-mini run 2**
- step 0[0] child: missing — the body is not in child
- got step 0: `condition.if(Left="%ok%",Operator="==",Right=true) ; condition.else(){"return": goal.return()}`

**else_return — gpt-5.4-mini run 3**
- step 0[0] child: missing — the body is not in child
- got step 0: `condition.if(Left="%ok%",Operator="==",Right=true) ; condition.else(){"return": goal.return()}`

**else_return — gpt-5.4-mini run 4**
- step 0[0] child: missing — the body is not in child
- got step 0: `condition.if(Left="%ok%",Operator="==",Right=true) ; condition.else(){"return": goal.return()}`

**else_return — gpt-5.4-mini run 5**
- step 0[0] child: missing — the body is not in child
- got step 0: `condition.if(Left="%ok%",Operator="==",Right=true) ; condition.else(){"return": goal.return()}`

**if_call_else_call — gpt-5.4-mini run 1**
- step 0[0] child: missing — the body is not in child
- got step 0: `condition.if(Left="%status%",Operator="==",Right="active") ; condition.else(){"call Deactivate": goal.call(Name="Deactivate")}`

**if_call_else_call — gpt-5.4-mini run 2**
- step 0[0] child: missing — the body is not in child
- got step 0: `condition.if(Left="%status%",Operator="==",Right="active") ; condition.else(){"call Deactivate": goal.call(Name="Deactivate")}`

**if_call_else_call — gpt-5.4-mini run 3**
- step 0[0] child: missing — the body is not in child
- got step 0: `condition.if(Left="%status%",Operator="==",Right="active") ; condition.else(){"call Deactivate": goal.call(Name="Deactivate")}`

**if_call_else_call — gpt-5.4-mini run 4**
- step 0[0] child: missing — the body is not in child
- got step 0: `condition.if(Left="%status%",Operator="==",Right="active") ; condition.else(){"call Deactivate": goal.call(Name="Deactivate")}`

**if_call_else_call — gpt-5.4-mini run 5**
- step 0[0] child: missing — the body is not in child
- got step 0: `condition.if(Left="%status%",Operator="==",Right="active") ; condition.else(){"call Deactivate": goal.call(Name="Deactivate")}`

**if_elseif_else_line — gpt-5.4-mini run 3**
- step 0[0] child: missing — the body is not in child
- step 0[1] child: missing — the body is not in child
- got step 0: `condition.if(Left="%score%",Operator=">",Right=90) ; condition.elseif(Left="%score%",Operator=">",Right=70) ; condition.else(){"write out "C"": output.write(Data="C")}`

**if_return — gpt-5.4-mini run 1**
- step 0: 2 actions, expected 1 (condition.if(Left="%count%",Operator="<",Right=3), goal.return())
- step 0[0] child: missing — the body is not in child
- got step 0: `condition.if(Left="%count%",Operator="<",Right=3) ; goal.return()`

**if_return — gpt-5.4-mini run 3**
- step 0: 2 actions, expected 1 (condition.if(Left="%count%",Operator="<",Right=3), goal.return())
- step 0[0] child: missing — the body is not in child
- got step 0: `condition.if(Left="%count%",Operator="<",Right=3) ; goal.return()`

**indented_two_levels — gpt-5.4-nano run 3**
- step 0[0] child: invented ([{"text": "if %b% > 0", "action": [{"module": "condition", "name": "if", "property": [{"name": "Left", "type": {"name": )
- got step 0: `condition.if(Left="%a%",Operator=">",Right=0){"if %b% > 0": condition.if(Left="%b%",Operator=">",Right=0){"call BothPositive": goal.call(Name="BothPositive") \| "write out "checked"": output.write(Data="checked")}}`
- step 1: not in the answer
- got step 1: `(missing)`
- step 2: not in the answer
- got step 2: `(missing)`
- step 3: not in the answer
- got step 3: `(missing)`

**inline_if_then_step — gpt-5.4-mini run 1**
- step 0: 2 actions, expected 1 (condition.if(Left="%n%",Operator=">",Right=5), goal.call(Name="Big"))
- step 0[0] child: missing — the body is not in child
- got step 0: `condition.if(Left="%n%",Operator=">",Right=5) ; goal.call(Name="Big")`

**inline_if_then_step — gpt-5.4-mini run 2**
- step 0: 2 actions, expected 1 (condition.if(Left="%n%",Operator=">",Right=5), goal.call(Name="Big"))
- step 0[0] child: missing — the body is not in child
- got step 0: `condition.if(Left="%n%",Operator=">",Right=5) ; goal.call(Name="Big")`

**inline_if_then_step — gpt-5.4-mini run 3**
- step 0: 2 actions, expected 1 (condition.if(Left="%n%",Operator=">",Right=5), goal.call(Name="Big"))
- step 0[0] child: missing — the body is not in child
- got step 0: `condition.if(Left="%n%",Operator=">",Right=5) ; goal.call(Name="Big")`

**inline_if_then_step — gpt-5.4-mini run 4**
- step 0: 2 actions, expected 1 (condition.if(Left="%n%",Operator=">",Right=5), goal.call(Name="Big"))
- step 0[0] child: missing — the body is not in child
- got step 0: `condition.if(Left="%n%",Operator=">",Right=5) ; goal.call(Name="Big")`

**inline_if_then_step — gpt-5.4-mini run 5**
- step 0: 2 actions, expected 1 (condition.if(Left="%n%",Operator=">",Right=5), goal.call(Name="Big"))
- step 0[0] child: missing — the body is not in child
- got step 0: `condition.if(Left="%n%",Operator=">",Right=5) ; goal.call(Name="Big")`

**negate_isempty — gpt-5.4-mini run 5**
- step 0: 2 actions, expected 1 (condition.if(Left="%items%",Operator="isempty",Negate=true), goal.call(Name="ProcessItems"))
- step 0[0] child: missing — the body is not in child
- got step 0: `condition.if(Left="%items%",Operator="isempty",Negate=true) ; goal.call(Name="ProcessItems")`

**op_in — gpt-5.4-mini run 5**
- step 0: 2 actions, expected 1 (condition.if(Left="%role%",Operator="in",Right=["admin", "owner"]), goal.call(Name="GrantAccess"))
- step 0[0] child: missing — the body is not in child
- got step 0: `condition.if(Left="%role%",Operator="in",Right=["admin", "owner"]) ; goal.call(Name="GrantAccess")`

**op_startswith — gpt-5.4-mini run 1**
- step 0: 2 actions, expected 1 (condition.if(Left="%path%",Operator="startswith",Right="/api"), goal.call(Name="HandleApi",Parameter=[{"name": "path", "type": {"name": "item"}, "value": "%path%"}]))
- step 0[0] child: missing — the body is not in child
- got step 0: `condition.if(Left="%path%",Operator="startswith",Right="/api") ; goal.call(Name="HandleApi",Parameter=[{"name": "path", "type": {"name": "item"}, "value": "%path%"}])`

**op_startswith — gpt-5.4-mini run 2**
- step 0: 2 actions, expected 1 (condition.if(Left="%path%",Operator="startswith",Right="/api"), goal.call(Name="HandleApi",Parameter=[{"name": "path", "type": {"name": "item"}, "value": "%path%"}]))
- step 0[0] child: missing — the body is not in child
- got step 0: `condition.if(Left="%path%",Operator="startswith",Right="/api") ; goal.call(Name="HandleApi",Parameter=[{"name": "path", "type": {"name": "item"}, "value": "%path%"}])`

**op_startswith — gpt-5.4-mini run 3**
- step 0: 2 actions, expected 1 (condition.if(Left="%path%",Operator="startswith",Right="/api"), goal.call(Name="HandleApi",Parameter=[{"name": "path", "type": {"name": "item"}, "value": "%path%"}]))
- step 0[0] child: missing — the body is not in child
- got step 0: `condition.if(Left="%path%",Operator="startswith",Right="/api") ; goal.call(Name="HandleApi",Parameter=[{"name": "path", "type": {"name": "item"}, "value": "%path%"}])`

**op_startswith — gpt-5.4-mini run 4**
- step 0: 2 actions, expected 1 (condition.if(Left="%path%",Operator="startswith",Right="/api"), goal.call(Name="HandleApi",Parameter=[{"name": "path", "type": {"name": "item"}, "value": "%path%"}]))
- step 0[0] child: missing — the body is not in child
- got step 0: `condition.if(Left="%path%",Operator="startswith",Right="/api") ; goal.call(Name="HandleApi",Parameter=[{"name": "path", "type": {"name": "item"}, "value": "%path%"}])`

**op_startswith — gpt-5.4-mini run 5**
- step 0: 2 actions, expected 1 (condition.if(Left="%path%",Operator="startswith",Right="/api"), goal.call(Name="HandleApi",Parameter=[{"name": "path", "type": {"name": "item"}, "value": "%path%"}]))
- step 0[0] child: missing — the body is not in child
- got step 0: `condition.if(Left="%path%",Operator="startswith",Right="/api") ; goal.call(Name="HandleApi",Parameter=[{"name": "path", "type": {"name": "item"}, "value": "%path%"}])`

**setup_before_if — gpt-5.4-mini run 2**
- step 0: 4 actions, expected 3 (list.count(ListName="%items%"), variable.set(Name="%n%",Value="%!data%"), condition.if(Left="%n%",Operator=">",Right=10), goal.call(Name="Paginate"))
- step 0[2] child: missing — the body is not in child
- got step 0: `list.count(ListName="%items%") ; variable.set(Name="%n%",Value="%!data%") ; condition.if(Left="%n%",Operator=">",Right=10) ; goal.call(Name="Paginate")`

**setup_before_if — gpt-5.4-mini run 3**
- step 0: 4 actions, expected 3 (list.count(ListName="%items%"), variable.set(Name="%n%",Value="%!data%"), condition.if(Left="%n%",Operator=">",Right=10), goal.call(Name="Paginate"))
- step 0[2] child: missing — the body is not in child
- got step 0: `list.count(ListName="%items%") ; variable.set(Name="%n%",Value="%!data%") ; condition.if(Left="%n%",Operator=">",Right=10) ; goal.call(Name="Paginate")`



---

# Run 4 — after P1–P4 (5 runs, 32 cases / 45 steps, prompt frozen at `eaff47125`)

Raw: `tools/decider/runs/child_eval_20260925_122419/`, 320 calls, all answered. Each answer is also run
through the builder's checks (`build.match` + the action list's chain rule, mirrored by
`build_pr.match`): a wrong answer they refuse goes to the FixProperties retry; one they pass is
**silent** — it would reach the .pr wrong.

| | gpt-5.4-nano | gpt-5.4-mini |
|---|---|---|
| **first-attempt accuracy** (goal 100%) | **195/225** (86.7%) — run 3: 214 | **199/225** (88.4%) — run 3: 195 |
| misses the checks catch (→ retry) | 28 | 24 |
| **silent misses** (goal 0) | **2** | **2** |

**Neither model is at either bar.**

## The silent misses — each judged

| Model / run | Case | What reached the .pr | Behaviour | Why no check catches it | Proposal |
|---|---|---|---|---|---|
| nano 2, 3 | builder_decider | `llm.decider … Model: "jev-latest"` | **same** — `jev-latest` is the default | the value is legal | **S1:** `build.validate` drops a property whose value equals its declared default (the default is frozen into `action.Default` anyway) — deterministic, no retry, and the .pr reads cleaner |
| mini 5 | builder_render_template | `ui.render` without the `variable.set` — `write to %propertiesUserMsg%` lost | **different** — the result is never stored | nothing checks that a step's words were all mapped | **S2 (open):** see below |
| mini 2 | negate_contains | `if %name% contains "admin"` — `Negate` missing | **different** — the condition is inverted | a missing optional property is legal | **S2 (open)** |

**S2 — semantic misses can't be caught by shape checks.** A dropped `write to` and a dropped `Negate`
are well-formed answers. Options: (a) a second, cheap LLM pass that reads each step back ("does this
action list do what the step says?") and routes a "no" to FixProperties; (b) a deterministic
*coverage* check — every `%var%` a step writes to must be the Name of some variable.set in the entry
(catches the dropped write-to; not the Negate). Both are a design call.

## First-attempt misses, by class (all caught → retry)

| Class | Model / step-runs | Cause | Proposal |
|---|---|---|---|
| **Indented body pulled into the child** (and body steps dropped) | nano 15/15 runs of the 3 indented cases; mini 2 | **Prompt, via P1**: moving PF5 up so the Conditions section ends on inline examples fixed mini's flattening but made nano nest again — the two models pull opposite ways on the same paragraph order. The marker line alone doesn't hold nano. | **I1:** don't ask the model to hold back: for a step with an indented body, the builder already knows the answer's `child` must be empty — normalize (drop the LLM child, keep the entry) instead of refusing, since `build.fold` places the body anyway. Refuse only when the body's own steps are missing from the answer. |
| **Inline body beside / missing** | mini 13 (BodyBesideCondition 8, BodyMissing 10 across runs) | model — the teaching is explicit and in place | P4 catches all of them; the retry fixes |

## Stage-3 size (plang-3b's question, measured on run 3's prompts)

| | chars | tokens (OpenAI usage) |
|---|---|---|
| system prompt (Properties.llm + schema) | 11 420 | ~3 100, every request |
| user message, largest golden case (MenuModule, 4 steps) | 11 970 — **86% action notes** | 6 480 prompt / 892 completion |
| user message, 1-step case with notes (else_return) | 4 889 — 87% notes | 4 462 / 301 |
| user message, 1-step case without notes (save file) | 169 | 3 157 / 132 |
| all 32 cases | 129 606 — **87% notes** | |

Where it comes from: each action's notes print under every step that lists it — `condition.if`'s ~3.4k
chars repeat per condition step. De-duplicating (notes once per request) on the golden cases: −11%
overall, −25% to −47% on the multi-condition goals (MenuModule 11 970 → 6 305). A real 20–30-step goal
with 8 condition steps would carry ~27k chars of repeated `condition.if` notes alone; dedup leaves 3.4k.
My view: dedup first (queued) — it removes most of the size without touching what the model is asked;
splitting (v0.1's 8 s → 4 s, but 10→4 correct on a split 10-step goal) costs accuracy exactly where the
steps refer to one another, so it should come after dedup and only above a measured size.

## Run 4 — table and every miss

STEP TOTALS: gpt-5.4-nano 195/225, gpt-5.4-mini 199/225

| case | step | expected | gpt-5.4-nano got ×5 | gpt-5.4-nano | gpt-5.4-mini got ×5 | gpt-5.4-mini |
|---|---|---|---|---|---|---|
| if_return | 0 | `condition.if(Left="%count%",Operator="<",Right=3){"return": goal.return()}` | 1: ✓<br>2: ✓<br>3: ✓<br>4: ✓<br>5: ✓ | YES | 1: ✓<br>2: ✓<br>3: ✓<br>4: ✗ `condition.if(Left="%count%",Operator="<",Right=3) ; goal.return()`<br>5: ✓ | NO (4/5) |
| if_call_else_call | 0 | `condition.if(Left="%status%",Operator="==",Right="active"){"call Activate": goal.call(Name="Activate")} ; condition.else(){"call Deactivate": goal.call(Name="Deactivate")}` | 1: ✓<br>2: ✓<br>3: ✓<br>4: ✓<br>5: ✓ | YES | 1: ✗ `condition.if(Left="%status%",Operator="==",Right="active") ; condition.else(){"call Deactivate": goal.call(Name="Deactivate")}`<br>2: ✗ `condition.if(Left="%status%",Operator="==",Right="active") ; condition.else(){"call Deactivate": goal.call(Name="Deactivate")}`<br>3: ✗ `condition.if(Left="%status%",Operator="==",Right="active") ; condition.else(){"call Deactivate": goal.call(Name="Deactivate")}`<br>4: ✗ `condition.if(Left="%status%",Operator="==",Right="active") ; condition.else(){"call Deactivate": goal.call(Name="Deactivate")}`<br>5: ✗ `condition.if(Left="%status%",Operator="==",Right="active") ; condition.else(){"call Deactivate": goal.call(Name="Deactivate")}` | NO (0/5) |
| if_elseif_else_line | 0 | `condition.if(Left="%score%",Operator=">",Right=90){"write out "A"": output.write(Data="A")} ; condition.elseif(Left="%score%",Operator=">",Right=70){"write out "B"": output.write(Data="B")} ; condition.else(){"write out "C"": output.write(Data="C")}` | 1: ✓<br>2: ✓<br>3: ✓<br>4: ✓<br>5: ✓ | YES | 1: ✓<br>2: ✓<br>3: ✓<br>4: ✓<br>5: ✓ | YES |
| if_elseif_no_else | 0 | `condition.if(Left="%lang%",Operator="==",Right="is"){"write out "Halló"": output.write(Data="Halló")} ; condition.elseif(Left="%lang%",Operator="==",Right="en"){"write out "Hello"": output.write(Data="Hello")}` | 1: ✓<br>2: ✓<br>3: ✓<br>4: ✓<br>5: ✓ | YES | 1: ✓<br>2: ✓<br>3: ✓<br>4: ✓<br>5: ✓ | YES |
| else_return | 0 | `condition.if(Left="%ok%",Operator="==",Right=true){"call Continue": goal.call(Name="Continue")} ; condition.else(){"return": goal.return()}` | 1: ✓<br>2: ✓<br>3: ✓<br>4: ✓<br>5: ✓ | YES | 1: ✓<br>2: ✗ `condition.if(Left="%ok%",Operator="==",Right=true) ; condition.else(){"return": goal.return()}`<br>3: ✗ `condition.if(Left="%ok%",Operator="==",Right=true) ; condition.else(){"return": goal.return()}`<br>4: ✗ `condition.if(Left="%ok%",Operator="==",Right=true) ; condition.else(){"return": goal.return()}`<br>5: ✗ `condition.if(Left="%ok%",Operator="==",Right=true) ; condition.else(){"return": goal.return()}` | NO (1/5) |
| negate_contains | 0 | `condition.if(Left="%name%",Operator="contains",Right="admin",Negate=true){"call Deny": goal.call(Name="Deny")}` | 1: ✓<br>2: ✓<br>3: ✓<br>4: ✓<br>5: ✓ | YES | 1: ✓<br>2: ✗ `condition.if(Left="%name%",Operator="contains",Right="admin"){"call Deny": goal.call(Name="Deny")}`<br>3: ✓<br>4: ✓<br>5: ✓ | NO (4/5) |
| negate_isempty | 0 | `condition.if(Left="%items%",Operator="isempty",Negate=true){"call ProcessItems": goal.call(Name="ProcessItems")}` | 1: ✓<br>2: ✓<br>3: ✓<br>4: ✓<br>5: ✓ | YES | 1: ✓<br>2: ✓<br>3: ✓<br>4: ✓<br>5: ✓ | YES |
| setup_before_if | 0 | `list.count(ListName="%items%") ; variable.set(Name="%n%",Value="%!data%") ; condition.if(Left="%n%",Operator=">",Right=10){"call Paginate": goal.call(Name="Paginate")}` | 1: ✓<br>2: ✓<br>3: ✓<br>4: ✓<br>5: ✓ | YES | 1: ✓<br>2: ✓<br>3: ✓<br>4: ✓<br>5: ✗ `list.count(ListName="%items%") ; variable.set(Name="%n%",Value="%!data%") ; condition.if(Left="%n%",Operator=">",Right=10) ; goal.call(Name="Paginate")` | NO (4/5) |
| body_with_write_to | 0 | `condition.if(Left="%price%",Operator=">",Right=100){"multiply %price% by 0.9, write to %price%": math.multiply(A="%price%",B=0.9); variable.set(Name="%price%",Value="%!data%")}` | 1: ✓<br>2: ✓<br>3: ✓<br>4: ✓<br>5: ✓ | YES | 1: ✗ `condition.if(Left="%price%",Operator=">",Right=100) ; math.multiply(A="%price%",B=0.9)`<br>2: ✗ `condition.if(Left="%price%",Operator=">",Right=100) ; math.multiply(A="%price%",B=0.9)`<br>3: ✗ `condition.if(Left="%price%",Operator=">",Right=100) ; math.multiply(A="%price%",B=0.9)`<br>4: ✗ `condition.if(Left="%price%",Operator=">",Right=100) ; math.multiply(A="%price%",B=0.9)`<br>5: ✗ `condition.if(Left="%price%",Operator=">",Right=100) ; math.multiply(A="%price%",B=0.9)` | NO (0/5) |
| body_two_actions | 0 | `condition.if(Left="%user%",Operator="==",Right=null){"write out "no user" and return": output.write(Data="no user"); goal.return()}` | 1: ✓<br>2: ✓<br>3: ✓<br>4: ✓<br>5: ✓ | YES | 1: ✓<br>2: ✓<br>3: ✓<br>4: ✓<br>5: ✓ | YES |
| op_in | 0 | `condition.if(Left="%role%",Operator="in",Right=["admin", "owner"]){"call GrantAccess": goal.call(Name="GrantAccess")}` | 1: ✓<br>2: ✓<br>3: ✓<br>4: ✓<br>5: ✓ | YES | 1: ✓<br>2: ✓<br>3: ✓<br>4: ✓<br>5: ✓ | YES |
| op_startswith | 0 | `condition.if(Left="%path%",Operator="startswith",Right="/api"){"call HandleApi path=%path%": goal.call(Name="HandleApi",Parameter=[{"name": "path", "value": "%path%"}])}` | 1: ✓<br>2: ✓<br>3: ✓<br>4: ✓<br>5: ✓ | YES | 1: ✓<br>2: ✓<br>3: ✓<br>4: ✓<br>5: ✗ `condition.if(Left="%path%",Operator="startswith",Right="/api") ; goal.call(Name="HandleApi",Parameter=[{"name": "path", "type": {"name": "item"}, "value": "%path%"}])` | NO (4/5) |
| op_is_type | 0 | `condition.if(Left="%value%",Operator="is",Right="number"){"write out "numeric"": output.write(Data="numeric")}` | 1: ✓<br>2: ✓<br>3: ✓<br>4: ✓<br>5: ✓ | YES | 1: ✓<br>2: ✓<br>3: ✓<br>4: ✓<br>5: ✓ | YES |
| menumodule | 0 | `variable.set(Name="%key%",Value="s%step.Index%_%module.Name%")` | 1: ✓<br>2: ✓<br>3: ✓<br>4: ✓<br>5: ✓ | YES | 1: ✓<br>2: ✓<br>3: ✓<br>4: ✓<br>5: ✓ | YES |
| menumodule | 1 | `condition.if(Left="%moduleAnswer[key].noul%",Operator="<",Right="%threshold%"){"return": goal.return()}` | 1: ✓<br>2: ✓<br>3: ✓<br>4: ✓<br>5: ✓ | YES | 1: ✓<br>2: ✓<br>3: ✓<br>4: ✓<br>5: ✓ | YES |
| menumodule | 2 | `condition.if(Left="%module.Action.Count%",Operator="==",Right=1){"add "%module.Name%.%module.Action[0].Name%" to %choices%": list.add(ListName="%choices%",Value="%module.Name%.%module.Action[0].Name%")}` | 1: ✓<br>2: ✓<br>3: ✓<br>4: ✓<br>5: ✓ | YES | 1: ✓<br>2: ✓<br>3: ✓<br>4: ✓<br>5: ✓ | YES |
| menumodule | 3 | `condition.if(Left="%module.Action.Count%",Operator=">",Right=1){"add "%module.Name%.%actionAnswer[key].choice%" to %choices%": list.add(ListName="%choices%",Value="%module.Name%.%actionAnswer[key].choice%")}` | 1: ✓<br>2: ✓<br>3: ✓<br>4: ✓<br>5: ✓ | YES | 1: ✓<br>2: ✓<br>3: ✓<br>4: ✓<br>5: ✓ | YES |
| complementary_steps | 0 | `condition.if(Left="%n%",Operator="==",Right=0){"write out "none"": output.write(Data="none")}` | 1: ✓<br>2: ✓<br>3: ✓<br>4: ✓<br>5: ✓ | YES | 1: ✓<br>2: ✓<br>3: ✓<br>4: ✓<br>5: ✓ | YES |
| complementary_steps | 1 | `condition.if(Left="%n%",Operator=">",Right=0){"write out "some"": output.write(Data="some")}` | 1: ✓<br>2: ✓<br>3: ✓<br>4: ✓<br>5: ✓ | YES | 1: ✓<br>2: ✓<br>3: ✓<br>4: ✓<br>5: ✓ | YES |
| builder_set_default | 0 | `variable.set(Name="%path%",Value="/",AsDefault=true)` | 1: ✓<br>2: ✓<br>3: ✓<br>4: ✓<br>5: ✓ | YES | 1: ✓<br>2: ✓<br>3: ✓<br>4: ✓<br>5: ✓ | YES |
| builder_set_number | 0 | `variable.set(Name="%threshold%",Value=0.5)` | 1: ✓<br>2: ✓<br>3: ✓<br>4: ✓<br>5: ✓ | YES | 1: ✓<br>2: ✓<br>3: ✓<br>4: ✓<br>5: ✓ | YES |
| builder_set_empty_list | 0 | `variable.set(Name="%choices%",Value=[])` | 1: ✓<br>2: ✓<br>3: ✓<br>4: ✓<br>5: ✓ | YES | 1: ✓<br>2: ✓<br>3: ✓<br>4: ✓<br>5: ✓ | YES |
| builder_set_indexed | 0 | `variable.set(Name="%menu[step.Index]%",Value="%choices%")` | 1: ✓<br>2: ✓<br>3: ✓<br>4: ✓<br>5: ✓ | YES | 1: ✓<br>2: ✓<br>3: ✓<br>4: ✓<br>5: ✓ | YES |
| builder_math_write_to | 0 | `math.subtract(A="%Now.Ticks%",B="%buildStart%") ; variable.set(Name="%elapsedTicks%",Value="%!data%")` | 1: ✓<br>2: ✓<br>3: ✓<br>4: ✓<br>5: ✓ | YES | 1: ✓<br>2: ✓<br>3: ✓<br>4: ✓<br>5: ✓ | YES |
| builder_render_template | 0 | `ui.render(Template="/system/builder/llm/templates/propertiesUser.template") ; variable.set(Name="%propertiesUserMsg%",Value="%!data%")` | 1: ✓<br>2: ✓<br>3: ✓<br>4: ✓<br>5: ✓ | YES | 1: ✓<br>2: ✓<br>3: ✓<br>4: ✓<br>5: ✗ `ui.render(Template="/system/builder/llm/templates/propertiesUser.template")` | NO (4/5) |
| builder_decider | 0 | `llm.decider(State="%state%",Question="%moduleQuestions%") ; variable.set(Name="%moduleAnswer%",Value="%!data%")` | 1: ✓<br>2: ✗ `llm.decider(State="%state%",Question="%moduleQuestions%",Model="jev-latest") ; variable.set(Name="%moduleAnswer%",Value="%!data%")`<br>3: ✗ `llm.decider(State="%state%",Question="%moduleQuestions%",Model="jev-latest") ; variable.set(Name="%moduleAnswer%",Value="%!data%")`<br>4: ✓<br>5: ✓ | NO (3/5) | 1: ✓<br>2: ✓<br>3: ✓<br>4: ✓<br>5: ✓ | YES |
| builder_save_file | 0 | `file.save(Path="/.build/traces/%!trace.id%/%goal.Name%.json",Value="%trace%")` | 1: ✓<br>2: ✓<br>3: ✓<br>4: ✓<br>5: ✓ | YES | 1: ✓<br>2: ✓<br>3: ✓<br>4: ✓<br>5: ✓ | YES |
| builder_add_to_list | 0 | `list.add(ListName="%traceGoals%",Value="%goal.Name%")` | 1: ✓<br>2: ✓<br>3: ✓<br>4: ✓<br>5: ✓ | YES | 1: ✓<br>2: ✓<br>3: ✓<br>4: ✓<br>5: ✓ | YES |
| indented_two_levels | 0 | `condition.if(Left="%a%",Operator=">",Right=0)` | 1: ✗ `condition.if(Left="%a%",Operator=">",Right=0){"if %b% > 0": condition.if(Left="%b%",Operator=">",Right=0){"call BothPositive": goal.call(Name="BothPositive") \| "write out "checked"": output.write(Data="checked")}}`<br>2: ✗ `condition.if(Left="%a%",Operator=">",Right=0){"if %b% > 0": condition.if(Left="%b%",Operator=">",Right=0){"call BothPositive": goal.call(Name="BothPositive") \| "write out "checked"": output.write(Data="checked")}}`<br>3: ✗ `condition.if(Left="%a%",Operator=">",Right=0){"if %b% > 0": condition.if(Left="%b%",Operator=">",Right=0){"call BothPositive": goal.call(Name="BothPositive") \| "write out "checked"": output.write(Data="checked")}}`<br>4: ✗ `condition.if(Left="%a%",Operator=">",Right=0){"if %b% > 0": condition.if(Left="%b%",Operator=">",Right=0){"call BothPositive": goal.call(Name="BothPositive") \| "write out "checked"": output.write(Data="checked")}}`<br>5: ✗ `condition.if(Left="%a%",Operator=">",Right=0){"if %b% > 0": condition.if(Left="%b%",Operator=">",Right=0){"call BothPositive": goal.call(Name="BothPositive") \| "write out "checked"": output.write(Data="checked")}}` | NO (0/5) | 1: ✓<br>2: ✓<br>3: ✓<br>4: ✓<br>5: ✓ | YES |
| indented_two_levels | 1 | `condition.if(Left="%b%",Operator=">",Right=0)` | 1: ✗ `(missing)`<br>2: ✓<br>3: ✓<br>4: ✗ `condition.if(Left="%b%",Operator=">",Right=0){"call BothPositive": goal.call(Name="BothPositive") \| "write out "checked"": output.write(Data="checked")}`<br>5: ✗ `(missing)` | NO (2/5) | 1: ✓<br>2: ✓<br>3: ✓<br>4: ✓<br>5: ✓ | YES |
| indented_two_levels | 2 | `goal.call(Name="BothPositive")` | 1: ✗ `(missing)`<br>2: ✓<br>3: ✓<br>4: ✓<br>5: ✗ `(missing)` | NO (3/5) | 1: ✓<br>2: ✓<br>3: ✓<br>4: ✗ `goal.call(Name="BothPositive") ; output.write(Data="checked")`<br>5: ✓ | NO (4/5) |
| indented_two_levels | 3 | `output.write(Data="checked")` | 1: ✗ `(missing)`<br>2: ✓<br>3: ✓<br>4: ✓<br>5: ✗ `(missing)` | NO (3/5) | 1: ✓<br>2: ✓<br>3: ✓<br>4: ✗ `(missing)`<br>5: ✓ | NO (4/5) |
| indented_then_outdented | 0 | `condition.if(Left="%retries%",Operator=">",Right=3)` | 1: ✗ `condition.if(Left="%retries%",Operator=">",Right=3){"write out "giving up"": output.write(Data="giving up") \| "return": goal.return()}`<br>2: ✗ `condition.if(Left="%retries%",Operator=">",Right=3){"write out "giving up"": output.write(Data="giving up") \| "return": goal.return()}`<br>3: ✗ `condition.if(Left="%retries%",Operator=">",Right=3){"write out "giving up"": output.write(Data="giving up") \| "return": goal.return()}`<br>4: ✗ `condition.if(Left="%retries%",Operator=">",Right=3){"write out "giving up"": output.write(Data="giving up") \| "return": goal.return()}`<br>5: ✗ `condition.if(Left="%retries%",Operator=">",Right=3){"write out "giving up"": output.write(Data="giving up") \| "return": goal.return()}` | NO (0/5) | 1: ✗ `condition.if(Left="%retries%",Operator=">",Right=3){"write out "giving up"": output.write(Data="giving up") \| "return": goal.return()}`<br>2: ✓<br>3: ✓<br>4: ✓<br>5: ✓ | NO (4/5) |
| indented_then_outdented | 1 | `output.write(Data="giving up")` | 1: ✓<br>2: ✓<br>3: ✓<br>4: ✓<br>5: ✓ | YES | 1: ✓<br>2: ✓<br>3: ✓<br>4: ✓<br>5: ✓ | YES |
| indented_then_outdented | 2 | `goal.return()` | 1: ✓<br>2: ✓<br>3: ✓<br>4: ✓<br>5: ✓ | YES | 1: ✓<br>2: ✓<br>3: ✓<br>4: ✓<br>5: ✓ | YES |
| indented_then_outdented | 3 | `output.write(Data="trying again")` | 1: ✓<br>2: ✓<br>3: ✓<br>4: ✓<br>5: ✓ | YES | 1: ✓<br>2: ✓<br>3: ✓<br>4: ✓<br>5: ✓ | YES |
| inline_if_then_step | 0 | `condition.if(Left="%n%",Operator=">",Right=5){"call Big": goal.call(Name="Big")}` | 1: ✓<br>2: ✓<br>3: ✓<br>4: ✓<br>5: ✓ | YES | 1: ✗ `condition.if(Left="%n%",Operator=">",Right=5) ; goal.call(Name="Big")`<br>2: ✗ `condition.if(Left="%n%",Operator=">",Right=5){"call Big": goal.call(Name="Big")} ; goal.call(Name="Big")`<br>3: ✓<br>4: ✗ `condition.if(Left="%n%",Operator=">",Right=5) ; goal.call(Name="Big")`<br>5: ✗ `condition.if(Left="%n%",Operator=">",Right=5) ; goal.call(Name="Big")` | NO (1/5) |
| inline_if_then_step | 1 | `list.count(ListName="%items%") ; variable.set(Name="%n%",Value="%!data%")` | 1: ✓<br>2: ✓<br>3: ✓<br>4: ✓<br>5: ✓ | YES | 1: ✓<br>2: ✓<br>3: ✓<br>4: ✓<br>5: ✓ | YES |
| plain_write | 0 | `output.write(Data="hello")` | 1: ✓<br>2: ✓<br>3: ✓<br>4: ✓<br>5: ✓ | YES | 1: ✓<br>2: ✓<br>3: ✓<br>4: ✓<br>5: ✓ | YES |
| plain_call_args | 0 | `goal.call(Name="SendMail",Parameter=[{"name": "to", "value": "%email%"}, {"name": "subject", "value": "Welcome"}])` | 1: ✓<br>2: ✓<br>3: ✓<br>4: ✓<br>5: ✓ | YES | 1: ✓<br>2: ✓<br>3: ✓<br>4: ✓<br>5: ✓ | YES |
| plain_add | 0 | `list.add(ListName="%items%",Value="%item%")` | 1: ✓<br>2: ✓<br>3: ✓<br>4: ✓<br>5: ✓ | YES | 1: ✓<br>2: ✓<br>3: ✓<br>4: ✓<br>5: ✓ | YES |
| indented_body | 0 | `condition.if(Left="%count%",Operator=">",Right=0)` | 1: ✗ `condition.if(Left="%count%",Operator=">",Right=0){"call ProcessItems": goal.call(Name="ProcessItems") \| "write out "done"": output.write(Data="done")}`<br>2: ✗ `condition.if(Left="%count%",Operator=">",Right=0){"call ProcessItems": goal.call(Name="ProcessItems") \| "write out "done"": output.write(Data="done")}`<br>3: ✗ `condition.if(Left="%count%",Operator=">",Right=0){"call ProcessItems": goal.call(Name="ProcessItems") \| "write out "done"": output.write(Data="done")}`<br>4: ✗ `condition.if(Left="%count%",Operator=">",Right=0){"call ProcessItems": goal.call(Name="ProcessItems") \| "write out "done"": output.write(Data="done")}`<br>5: ✗ `condition.if(Left="%count%",Operator=">",Right=0){"call ProcessItems": goal.call(Name="ProcessItems") \| "write out "done"": output.write(Data="done")}` | NO (0/5) | 1: ✓<br>2: ✓<br>3: ✓<br>4: ✓<br>5: ✓ | YES |
| indented_body | 1 | `goal.call(Name="ProcessItems")` | 1: ✗ `(missing)`<br>2: ✓<br>3: ✗ `(missing)`<br>4: ✗ `(missing)`<br>5: ✓ | NO (2/5) | 1: ✓<br>2: ✓<br>3: ✓<br>4: ✓<br>5: ✓ | YES |
| indented_body | 2 | `output.write(Data="done")` | 1: ✗ `(missing)`<br>2: ✓<br>3: ✗ `(missing)`<br>4: ✗ `(missing)`<br>5: ✓ | NO (2/5) | 1: ✓<br>2: ✓<br>3: ✓<br>4: ✓<br>5: ✓ | YES |
| nested_inline | 0 | `condition.if(Left="%a%",Operator=">",Right=0){"if %b% > 0, call BothPositive": condition.if(Left="%b%",Operator=">",Right=0){"call BothPositive": goal.call(Name="BothPositive")}}` | 1: ✓<br>2: ✓<br>3: ✓<br>4: ✓<br>5: ✓ | YES | 1: ✓<br>2: ✓<br>3: ✓<br>4: ✓<br>5: ✓ | YES |

## Every miss, in full

**body_with_write_to — gpt-5.4-mini run 1**
- step 0: 2 actions, expected 1 (condition.if(Left="%price%",Operator=">",Right=100), math.multiply(A="%price%",B=0.9))
- step 0[0] child: missing — the body is not in child
- got step 0: `condition.if(Left="%price%",Operator=">",Right=100) ; math.multiply(A="%price%",B=0.9)`

**body_with_write_to — gpt-5.4-mini run 2**
- step 0: 2 actions, expected 1 (condition.if(Left="%price%",Operator=">",Right=100), math.multiply(A="%price%",B=0.9))
- step 0[0] child: missing — the body is not in child
- got step 0: `condition.if(Left="%price%",Operator=">",Right=100) ; math.multiply(A="%price%",B=0.9)`

**body_with_write_to — gpt-5.4-mini run 3**
- step 0: 2 actions, expected 1 (condition.if(Left="%price%",Operator=">",Right=100), math.multiply(A="%price%",B=0.9))
- step 0[0] child: missing — the body is not in child
- got step 0: `condition.if(Left="%price%",Operator=">",Right=100) ; math.multiply(A="%price%",B=0.9)`

**body_with_write_to — gpt-5.4-mini run 4**
- step 0: 2 actions, expected 1 (condition.if(Left="%price%",Operator=">",Right=100), math.multiply(A="%price%",B=0.9))
- step 0[0] child: missing — the body is not in child
- got step 0: `condition.if(Left="%price%",Operator=">",Right=100) ; math.multiply(A="%price%",B=0.9)`

**body_with_write_to — gpt-5.4-mini run 5**
- step 0: 2 actions, expected 1 (condition.if(Left="%price%",Operator=">",Right=100), math.multiply(A="%price%",B=0.9))
- step 0[0] child: missing — the body is not in child
- got step 0: `condition.if(Left="%price%",Operator=">",Right=100) ; math.multiply(A="%price%",B=0.9)`

**builder_decider — gpt-5.4-nano run 2**
- step 0[0] llm.decider: extra Model = 'jev-latest'
- got step 0: `llm.decider(State="%state%",Question="%moduleQuestions%",Model="jev-latest") ; variable.set(Name="%moduleAnswer%",Value="%!data%")`

**builder_decider — gpt-5.4-nano run 3**
- step 0[0] llm.decider: extra Model = 'jev-latest'
- got step 0: `llm.decider(State="%state%",Question="%moduleQuestions%",Model="jev-latest") ; variable.set(Name="%moduleAnswer%",Value="%!data%")`

**builder_render_template — gpt-5.4-mini run 5**
- step 0: 1 actions, expected 2 (ui.render(Template="/system/builder/llm/templates/propertiesUser.template"))
- got step 0: `ui.render(Template="/system/builder/llm/templates/propertiesUser.template")`

**else_return — gpt-5.4-mini run 2**
- step 0[0] child: missing — the body is not in child
- got step 0: `condition.if(Left="%ok%",Operator="==",Right=true) ; condition.else(){"return": goal.return()}`

**else_return — gpt-5.4-mini run 3**
- step 0[0] child: missing — the body is not in child
- got step 0: `condition.if(Left="%ok%",Operator="==",Right=true) ; condition.else(){"return": goal.return()}`

**else_return — gpt-5.4-mini run 4**
- step 0[0] child: missing — the body is not in child
- got step 0: `condition.if(Left="%ok%",Operator="==",Right=true) ; condition.else(){"return": goal.return()}`

**else_return — gpt-5.4-mini run 5**
- step 0[0] child: missing — the body is not in child
- got step 0: `condition.if(Left="%ok%",Operator="==",Right=true) ; condition.else(){"return": goal.return()}`

**if_call_else_call — gpt-5.4-mini run 1**
- step 0[0] child: missing — the body is not in child
- got step 0: `condition.if(Left="%status%",Operator="==",Right="active") ; condition.else(){"call Deactivate": goal.call(Name="Deactivate")}`

**if_call_else_call — gpt-5.4-mini run 2**
- step 0[0] child: missing — the body is not in child
- got step 0: `condition.if(Left="%status%",Operator="==",Right="active") ; condition.else(){"call Deactivate": goal.call(Name="Deactivate")}`

**if_call_else_call — gpt-5.4-mini run 3**
- step 0[0] child: missing — the body is not in child
- got step 0: `condition.if(Left="%status%",Operator="==",Right="active") ; condition.else(){"call Deactivate": goal.call(Name="Deactivate")}`

**if_call_else_call — gpt-5.4-mini run 4**
- step 0[0] child: missing — the body is not in child
- got step 0: `condition.if(Left="%status%",Operator="==",Right="active") ; condition.else(){"call Deactivate": goal.call(Name="Deactivate")}`

**if_call_else_call — gpt-5.4-mini run 5**
- step 0[0] child: missing — the body is not in child
- got step 0: `condition.if(Left="%status%",Operator="==",Right="active") ; condition.else(){"call Deactivate": goal.call(Name="Deactivate")}`

**if_return — gpt-5.4-mini run 4**
- step 0: 2 actions, expected 1 (condition.if(Left="%count%",Operator="<",Right=3), goal.return())
- step 0[0] child: missing — the body is not in child
- got step 0: `condition.if(Left="%count%",Operator="<",Right=3) ; goal.return()`

**indented_body — gpt-5.4-nano run 1**
- step 0[0] child: invented ([{"text": "call ProcessItems", "action": [{"module": "goal", "name": "call", "property": [{"name": "Name", "type": {"nam)
- got step 0: `condition.if(Left="%count%",Operator=">",Right=0){"call ProcessItems": goal.call(Name="ProcessItems") \| "write out "done"": output.write(Data="done")}`
- step 1: not in the answer
- got step 1: `(missing)`
- step 2: not in the answer
- got step 2: `(missing)`

**indented_body — gpt-5.4-nano run 2**
- step 0[0] child: invented ([{"text": "call ProcessItems", "action": [{"module": "goal", "name": "call", "property": [{"name": "Name", "type": {"nam)
- got step 0: `condition.if(Left="%count%",Operator=">",Right=0){"call ProcessItems": goal.call(Name="ProcessItems") \| "write out "done"": output.write(Data="done")}`

**indented_body — gpt-5.4-nano run 3**
- step 0[0] child: invented ([{"text": "call ProcessItems", "action": [{"module": "goal", "name": "call", "property": [{"name": "Name", "type": {"nam)
- got step 0: `condition.if(Left="%count%",Operator=">",Right=0){"call ProcessItems": goal.call(Name="ProcessItems") \| "write out "done"": output.write(Data="done")}`
- step 1: not in the answer
- got step 1: `(missing)`
- step 2: not in the answer
- got step 2: `(missing)`

**indented_body — gpt-5.4-nano run 4**
- step 0[0] child: invented ([{"text": "call ProcessItems", "action": [{"module": "goal", "name": "call", "property": [{"name": "Name", "type": {"nam)
- got step 0: `condition.if(Left="%count%",Operator=">",Right=0){"call ProcessItems": goal.call(Name="ProcessItems") \| "write out "done"": output.write(Data="done")}`
- step 1: not in the answer
- got step 1: `(missing)`
- step 2: not in the answer
- got step 2: `(missing)`

**indented_body — gpt-5.4-nano run 5**
- step 0[0] child: invented ([{"text": "call ProcessItems", "action": [{"module": "goal", "name": "call", "property": [{"name": "Name", "type": {"nam)
- got step 0: `condition.if(Left="%count%",Operator=">",Right=0){"call ProcessItems": goal.call(Name="ProcessItems") \| "write out "done"": output.write(Data="done")}`

**indented_then_outdented — gpt-5.4-mini run 1**
- step 0[0] child: invented ([{"text": "write out \"giving up\"", "action": [{"module": "output", "name": "write", "property": [{"name": "Data", "typ)
- got step 0: `condition.if(Left="%retries%",Operator=">",Right=3){"write out "giving up"": output.write(Data="giving up") \| "return": goal.return()}`

**indented_then_outdented — gpt-5.4-nano run 1**
- step 0[0] child: invented ([{"text": "write out \"giving up\"", "action": [{"module": "output", "name": "write", "property": [{"name": "Data", "typ)
- got step 0: `condition.if(Left="%retries%",Operator=">",Right=3){"write out "giving up"": output.write(Data="giving up") \| "return": goal.return()}`

**indented_then_outdented — gpt-5.4-nano run 2**
- step 0[0] child: invented ([{"text": "write out \"giving up\"", "action": [{"module": "output", "name": "write", "property": [{"name": "Data", "typ)
- got step 0: `condition.if(Left="%retries%",Operator=">",Right=3){"write out "giving up"": output.write(Data="giving up") \| "return": goal.return()}`

**indented_then_outdented — gpt-5.4-nano run 3**
- step 0[0] child: invented ([{"text": "write out \"giving up\"", "action": [{"module": "output", "name": "write", "property": [{"name": "Data", "typ)
- got step 0: `condition.if(Left="%retries%",Operator=">",Right=3){"write out "giving up"": output.write(Data="giving up") \| "return": goal.return()}`

**indented_then_outdented — gpt-5.4-nano run 4**
- step 0[0] child: invented ([{"text": "write out \"giving up\"", "action": [{"module": "output", "name": "write", "property": [{"name": "Data", "typ)
- got step 0: `condition.if(Left="%retries%",Operator=">",Right=3){"write out "giving up"": output.write(Data="giving up") \| "return": goal.return()}`

**indented_then_outdented — gpt-5.4-nano run 5**
- step 0[0] child: invented ([{"text": "write out \"giving up\"", "action": [{"module": "output", "name": "write", "property": [{"name": "Data", "typ)
- got step 0: `condition.if(Left="%retries%",Operator=">",Right=3){"write out "giving up"": output.write(Data="giving up") \| "return": goal.return()}`

**indented_two_levels — gpt-5.4-mini run 4**
- step 2: 2 actions, expected 1 (goal.call(Name="BothPositive"), output.write(Data="checked"))
- got step 2: `goal.call(Name="BothPositive") ; output.write(Data="checked")`
- step 3: not in the answer
- got step 3: `(missing)`

**indented_two_levels — gpt-5.4-nano run 1**
- step 0[0] child: invented ([{"text": "if %b% > 0", "action": [{"module": "condition", "name": "if", "property": [{"name": "Left", "type": {"name": )
- got step 0: `condition.if(Left="%a%",Operator=">",Right=0){"if %b% > 0": condition.if(Left="%b%",Operator=">",Right=0){"call BothPositive": goal.call(Name="BothPositive") \| "write out "checked"": output.write(Data="checked")}}`
- step 1: not in the answer
- got step 1: `(missing)`
- step 2: not in the answer
- got step 2: `(missing)`
- step 3: not in the answer
- got step 3: `(missing)`

**indented_two_levels — gpt-5.4-nano run 2**
- step 0[0] child: invented ([{"text": "if %b% > 0", "action": [{"module": "condition", "name": "if", "property": [{"name": "Left", "type": {"name": )
- got step 0: `condition.if(Left="%a%",Operator=">",Right=0){"if %b% > 0": condition.if(Left="%b%",Operator=">",Right=0){"call BothPositive": goal.call(Name="BothPositive") \| "write out "checked"": output.write(Data="checked")}}`

**indented_two_levels — gpt-5.4-nano run 3**
- step 0[0] child: invented ([{"text": "if %b% > 0", "action": [{"module": "condition", "name": "if", "property": [{"name": "Left", "type": {"name": )
- got step 0: `condition.if(Left="%a%",Operator=">",Right=0){"if %b% > 0": condition.if(Left="%b%",Operator=">",Right=0){"call BothPositive": goal.call(Name="BothPositive") \| "write out "checked"": output.write(Data="checked")}}`

**indented_two_levels — gpt-5.4-nano run 4**
- step 0[0] child: invented ([{"text": "if %b% > 0", "action": [{"module": "condition", "name": "if", "property": [{"name": "Left", "type": {"name": )
- got step 0: `condition.if(Left="%a%",Operator=">",Right=0){"if %b% > 0": condition.if(Left="%b%",Operator=">",Right=0){"call BothPositive": goal.call(Name="BothPositive") \| "write out "checked"": output.write(Data="checked")}}`
- step 1[0] child: invented ([{"text": "call BothPositive", "action": [{"module": "goal", "name": "call", "property": [{"name": "Name", "type": {"nam)
- got step 1: `condition.if(Left="%b%",Operator=">",Right=0){"call BothPositive": goal.call(Name="BothPositive") \| "write out "checked"": output.write(Data="checked")}`

**indented_two_levels — gpt-5.4-nano run 5**
- step 0[0] child: invented ([{"text": "if %b% > 0", "action": [{"module": "condition", "name": "if", "property": [{"name": "Left", "type": {"name": )
- got step 0: `condition.if(Left="%a%",Operator=">",Right=0){"if %b% > 0": condition.if(Left="%b%",Operator=">",Right=0){"call BothPositive": goal.call(Name="BothPositive") \| "write out "checked"": output.write(Data="checked")}}`
- step 1: not in the answer
- got step 1: `(missing)`
- step 2: not in the answer
- got step 2: `(missing)`
- step 3: not in the answer
- got step 3: `(missing)`

**inline_if_then_step — gpt-5.4-mini run 1**
- step 0: 2 actions, expected 1 (condition.if(Left="%n%",Operator=">",Right=5), goal.call(Name="Big"))
- step 0[0] child: missing — the body is not in child
- got step 0: `condition.if(Left="%n%",Operator=">",Right=5) ; goal.call(Name="Big")`

**inline_if_then_step — gpt-5.4-mini run 2**
- step 0: 2 actions, expected 1 (condition.if(Left="%n%",Operator=">",Right=5){"call Big": goal.call(Name="Big")}, goal.call(Name="Big"))
- got step 0: `condition.if(Left="%n%",Operator=">",Right=5){"call Big": goal.call(Name="Big")} ; goal.call(Name="Big")`

**inline_if_then_step — gpt-5.4-mini run 4**
- step 0: 2 actions, expected 1 (condition.if(Left="%n%",Operator=">",Right=5), goal.call(Name="Big"))
- step 0[0] child: missing — the body is not in child
- got step 0: `condition.if(Left="%n%",Operator=">",Right=5) ; goal.call(Name="Big")`

**inline_if_then_step — gpt-5.4-mini run 5**
- step 0: 2 actions, expected 1 (condition.if(Left="%n%",Operator=">",Right=5), goal.call(Name="Big"))
- step 0[0] child: missing — the body is not in child
- got step 0: `condition.if(Left="%n%",Operator=">",Right=5) ; goal.call(Name="Big")`

**negate_contains — gpt-5.4-mini run 2**
- step 0[0] condition.if: Negate missing (expected True)
- got step 0: `condition.if(Left="%name%",Operator="contains",Right="admin"){"call Deny": goal.call(Name="Deny")}`

**op_startswith — gpt-5.4-mini run 5**
- step 0: 2 actions, expected 1 (condition.if(Left="%path%",Operator="startswith",Right="/api"), goal.call(Name="HandleApi",Parameter=[{"name": "path", "type": {"name": "item"}, "value": "%path%"}]))
- step 0[0] child: missing — the body is not in child
- got step 0: `condition.if(Left="%path%",Operator="startswith",Right="/api") ; goal.call(Name="HandleApi",Parameter=[{"name": "path", "type": {"name": "item"}, "value": "%path%"}])`

**setup_before_if — gpt-5.4-mini run 5**
- step 0: 4 actions, expected 3 (list.count(ListName="%items%"), variable.set(Name="%n%",Value="%!data%"), condition.if(Left="%n%",Operator=">",Right=10), goal.call(Name="Paginate"))
- step 0[2] child: missing — the body is not in child
- got step 0: `list.count(ListName="%items%") ; variable.set(Name="%n%",Value="%!data%") ; condition.if(Left="%n%",Operator=">",Right=10) ; goal.call(Name="Paginate")`

