# builder — v2 plan

## What this is
Builder bug-fixing round (the new role: other bots report builder mapping bugs;
builder maps them to the PLang-written builder and fixes the builder's source so a
developer's natural-language step builds correctly). Issues come from architect's
`.bot/app-systems/architect/builder-issues-2026-10-01.md` and cross-session messages.

## Issues worked (status at end of v2)

1. **Write-back regression (list.query)** — DONE, pushed.
   `sort %people% by age` with no `write to` lost its result (list.query answers a NEW
   list, never mutates input). Fix: `build.Match` (`PLang/app/module/build/code/Default.cs`)
   appends a deterministic `variable.set(Name=<[Input] var>, Value=%!data%)` for a
   destination-less result-only action (one with an `[Input]` property). Teaching flipped
   (`list/query.notes.md`, `examples.md`): writer writes only the query; "sort/order by X
   is always order, never a where". pick_golden re-pinned. Golden
   `test/plan/list-query/module/list/query/build/WriteBack.test.goal`.

2. **Write-back follow-up** (architect review) — DONE, pushed.
   (a) insert the set right after the query (`step.Code.Insert(IndexOf(act)+1, …)`), not at
   the step's end; (b) `ReadsData` asks the value's parsed `Variable` list for the `!data`
   root (not a substring, so `%!data.count%` counts); (c) target check uses the value's
   structured variables (one whole var, non-`!` root) not a regex. Pins
   `WriteBackInsertPosition` + `WriteBackConsumed` (formal, mutation-verified).

3. **Issue 21 part (a)** — DONE, pushed. `if '<file>' exists, …` never surfaced
   `file.exists`. `decider.state.template` principle: a condition over a FILE/WEB RESOURCE's
   fact makes the resource's module the main module. Measured: exists-probe/c1probe
   if-exists main module → `file` 5/5 (was condition/output 0/5). Guard sweep clean; one
   cosmetic label move (`if %name% is "a"` goal→condition, identical .pr).

4. **Issue 21 part (b)** — HANDED TO CODER (core, `pick/list/this.cs`).
   Diagnosis: the decider's `=> formal:` starting line nests `file.exists` INSIDE the if's
   body (`condition.if(Left) { file.exists; output.write }`) because `Prefill` orders by
   `Link` and `condition.if` (Link 0) leads and opens the body. The predicate that FEEDS
   the if must lead it. I measure after the coder's fix.

5. **Issue 25 (load vars → Template=true)** — ESCALATED TO INGI (rename).
   Teaching (notes + kernel example) is 0/9: nano makes the lexical match
   `default`→`Default` but not the semantic `load vars`→`Template` (param name absent from
   step). `properties.template` renders notes, not examples. Lever 1 (a Properties Rule)
   measured 0/5→0/5 — disproven. Architect takes the option rename to Ingi. LoadVars golden
   ready on disk (`test/module/file/read/`, uncommitted — red until the fix).

6. **Issue 24 (single-file build nulls app.pr name)** — DIAGNOSED, core → coder.
   app.pr persists a derived `Name` (`[LlmBuilder]`, = setting.Name ?? _folder); a
   files-build computes it null. Fix core (stop persisting it, or load settings first).

7. **Issue 26 (continue the conversation)** — LOGGED. Conversation option dropped (same
   lexical/semantic class as 3/25); a context-specific JSON build error in multi-step goals.

8. Deleted redundant `WhereUnsetVariable` test (coder's twin covers it tighter).

## Scope note
Builder C# scope = modules (`PLang/app/module/build/**`) + `os/system/**`. Core
(`goal/step/pick/**`, `app/this.cs`) → diagnose + hand to coder via architect. Honored.
