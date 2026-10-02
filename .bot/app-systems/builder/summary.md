# builder — summary

## Version
v2 (2026-10-02) — builder bug-fixing round. (v1 was the docs/familiarization pass; see v1/plan.md.)

## What this is
The builder's new role: other bots (architect, educator, coder, truthiness/os agents) report
builder *mapping* bugs — a natural-language step that compiles to the wrong `.pr`. Builder maps
each to the PLang-written builder (`os/system/builder/**`) and fixes the builder's own source so a
developer gets a correct mapping first try. Issues tracked in
`.bot/app-systems/architect/builder-issues-2026-10-01.md`.

Scope: builder C# = modules (`PLang/app/module/build/**`) + system tree (`os/system/**`). Core
(`goal/step/pick/**`, `app/this.cs`, …) → diagnose + hand to the coder via architect.

## What was done (this round)

### Pushed
- **Write-back (list.query)** `cd4fdcc62` + follow-up `7a45c0ebb`. A destination-less reshape
  (`sort %people% by age`, no `write to`) lost its result — list.query answers a NEW list. Fix:
  `build.Match` (`PLang/app/module/build/code/Default.cs`, method `WriteBack`) appends a
  deterministic `variable.set(Name=<[Input] var>, Value=%!data%)` right after the query. Follow-up:
  insert at `IndexOf(act)+1` (not step end); `ReadsData` asks parsed `Variable` list for the `!data`
  root; target check uses the value's structured vars (one whole, non-`!`). Teaching: `list/query.notes`
  + `examples` (writer writes only the query; "sort by X is always order, never a where"). Goldens
  `WriteBack`, `WriteBackInsertPosition`, `WriteBackConsumed` (mutation-verified).
- **Issue 21 part (a)** `9b928dcd8`. `decider.state.template`: a condition over a FILE/WEB RESOURCE's
  fact makes the resource's module the main module. Measured: if-exists main module → `file` 5/5
  (was 0/5). pick_golden re-pinned. One cosmetic label move (`if %name% is "a"`, identical .pr).
- **Deleted** redundant `WhereUnsetVariable` test `ce93493a8` (coder's twin is tighter).

### Code example (the pattern of these fixes — teach the builder, don't touch the dev's goal)
```
# build.Match post-pass: a result-only action with no destination gets its write-back
if (act.Module?[act.Name]?.Input is {} inputDef && <not consumed in-step> && <target is one whole var>)
    step.Code.Insert(step.Code.IndexOf(act)+1, variable.set(Name=<var>, Value=%!data%));
```

### Handed off / escalated (not mine to land)
- **Issue 21 part (b)** → CODER (core `pick/list/this.cs`). The decider's `=> formal:` starting line
  nests `file.exists` INSIDE the if body (`Prefill` orders by `Link`; `condition.if` Link 0 leads and
  opens the body). The predicate that FEEDS the if must lead it. Build is 4/5 (exists-probe), 0/5
  (c1probe) wrong until fixed. **I measure after the coder's fix** (c1probe+exists-probe 5× + guard
  sweep incl. a body-producer `if %x% > 5, read 'a.txt'`), then land the golden.
- **Issue 25 (load vars → Template)** → INGI (option rename). Teaching 0/9: nano makes the lexical
  `default`→`Default` match but not the semantic `load vars`→`Template`. `properties.template` renders
  notes not examples; a Properties Rule (lever 1) measured 0/5→0/5, disproven. LoadVars golden ready
  on disk (`test/module/file/read/`, uncommitted — red until the rename/fix).
- **Issue 24 (files-build nulls app.pr name)** → CODER (core). app.pr persists a derived `Name`
  (`[LlmBuilder]`, setting.Name ?? _folder); a files-build computes it null. (Causes `app.pr` churn on
  every single-file build; I `git checkout`'d it out of every commit.)
- **Issue 26 (continue the conversation)** → teaching + CODER. Naming the answer
  (`continue the conversation from %answer%`) maps `Conversation={continue:%answer%}` 3/3; bare drops
  it. But the Conversation build path throws `JsonException: BeginArray expected StartArray` ~2/5 even
  single-step — core, coder.

### Closed by re-check (3× cache-off each, both CORRECT)
- **Issue 1** `foreach %list%, call Unmatched action=%item%` → goal.call(Name="Unmatched",
  Parameter={action:%item%}); name not fused. Fixed (9fb6bfc51).
- **Issue 2** `foreach %list% as %x%, call Show x=%x%` → loop.foreach(Item=%x%); Item survives.

## What to do next
1. When coder pushes issue-21 part (b): measure (above) and land the issue-21 golden (data-leaving
   step, existing file, missing-file twin).
2. When Ingi rules on the issue-25 option rename: re-measure 5× bare phrase + plain-read guard, land
   the LoadVars golden.
3. Watch for the coder's issue 24 / 26 fixes; re-verify.
