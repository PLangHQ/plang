# coder v6 — stage 6 result: the reference

Branch `app-systems`. The trace, rulings and per-part decisions are in `v6/plan.md`; the rulings came
from plang-40 and are logged in `.bot/app-systems/architect/summary.md`.

## Commits
| commit | what |
|---|---|
| c150acea7 | trace and plan |
| 6f44b6054 | 6a — `app.variable` moves to `app.type.item.variable` (the memory at `variable/list`); the generator finds `IName` there; 150 unused usings go |
| 94abfbc11 | 6b — the parser and the four hops; a variable is `Text` + `Code`; `variable.set` writes through it |
| 531126e37 | 6c — text owns grep, grepcount, maxlength, trim, tolower, toupper, replace (`[LlmBuilder]`); Data's switch goes; the default grep provider births its answer with a context |
| 90303d2f7 | 6d/6e — `item.Variable`; a stored row writes `"variable"`, loading reads it; PrFormatOutdated for a marked row without one; the 16 marked `.pr` rewritten; `step.Typed`; the python twin writes the same lists |
| 8df240b2f | 6f — `data.Get(path)` runs the parser; `path/`, the walker, the clr/kind path walk and `CleanName` ×2 go; the store is root-only; `%setting.X%` goes; every other reference definition runs the parser (C# and the twin) |
| (this) | 6g — the eval, write-up, summary |

## What a variable is now
```
%user.address[idx].city%   →  Text  "%user.address[idx].city%"
                              Code  [variable user] [property address] [index → %idx%] [property city]
v.Start(ctx)        runs every hop (each gets the previous Data)       → what it holds
v.Set(value, ctx)   runs all but the last; the last writes itself      → member / key / !binding / rebind
```
In the `.pr`: `{"text": "%x.y%", "code": [{"variable": "x"}, {"property": "y"}]}` after the row's value.

## Plang-visible changes (for Ingi)
- **Index keys:** `[0]` a number, `["k"]` a text, a bare path (`[idx]`) or `[%i%]` a variable. An unset
  index variable is IndexNotSet; the silent fallback to the literal text is gone. os/ and Tests/ have 9
  bare indexes, all real variables, none relied on the fallback. `%dict[key]%` meaning the literal
  "key" is now `%dict["key"]%`.
- **Methods:** only methods a type marks `[LlmBuilder]` are reachable from a variable; a missing one is
  "text has no method 'foo'". Methods run on text only (the old switch stringified any value).
- **`!` forms:** `%x.kind!cost%` (the child's binding), `%x!a!b%`, `%!x!cost%` now read and write;
  `%x!!cost%` and `%x!%` don't parse.
- **A reference ends at a space** outside quotes, parentheses and brackets: `50% of %total%` holds one.
- **`%setting.X%` is gone** (settings go through `%!…%`, stage 7).
- **A bare variable name in a variable slot** is written back as `%name%`.

## The eval (one run, C + nano, as ruled)
- 5 golden goals (c_eval round 11): final 58/58 right, silent 0, failed 0. First attempt 56 right,
  2 caught and fixed by the retry — both nano's answer shape (a missing step entry; an operator
  `notisempty` that isn't an option), neither a reference.
- The builder's 12 goals (bootstrap): 9 first try, 1 right after the per-step retry, 3 refused
  (stage 5: 9 / 2 / 1). All three are nano's answer: Start `Operator==` (an unquoted `==`), Compile
  picked an unlisted `goal.return`, SourceError double-escaped `\n` (the literal check, untouched this
  stage). To rule out a prompt change, the old regexes and the new parser-based pick helpers were
  compared over all 73 builder steps: identical, so the prompts are byte-equal and the difference is
  run-to-run variance. Not chased (as ruled); the recorded answers stay.

## Verification
- Six suites at or under the baseline by name after every commit (final: Modules 34, Types 15, Wire 18,
  Data 41, Generator 18, Runtime 23; `Run_ParallelExecution_RespectsSemaphoreLimit` is a timing flake —
  red once, green on two reruns).
- Twins: `PickListTests`, `BootstrapTests`, `VariableListTwinTests` equal; formal_golden/bare/errors and
  pick_golden regenerate unchanged after the parser moved into the twin.
- `plang --test`: 7 pass, 0 fail, 317 stale; `plang build` of Tests/Simple writes the rewritten `.pr`
  byte for byte.

## Left for later (noted, not done)
- The index hop hands the container the key's text form; a typed container door is later work (ruled).
- `pick`'s grammar regexes (`as`, `=`, `write to`) stay; only the references in them are the parser's.
- The python `params.candidates` (an eval-only helper) still finds `%…%` by regex; it twins nothing in C#.
- 6 LLM snapshots were re-recorded by the suites (item gained members, which changes the snapshot key's
  class shape) and committed, as the repo tracks them.
