# llm-writer coder handoff (paused 2026-10-15, Ingi: save usage)

Coder: tester (plang-f2). Paused at a safe point; `llm-writer` is green at its tip.

## Where it is

Branch `llm-writer`, HEAD **cd5e2ea39** (pushed). Green:
- Stage 0 trace; Stage 1 (llm.Writer + `%!llm.setting.length%`=100,000 + full-match text cut);
  Stage 1b (the `BeginText`/`EndText` IWriter door, sink design; surface re-pinned under 800);
  Stage 2 **part 1** (lists/dicts → markdown table via `Table` from the primitive stream, cut at
  `%!llm.setting.rows%`=100 with `--- N more rows ---`; dict → `key (type): value`, `--- N more keys ---`;
  nested cell → `[list of N]`/`{dict of N keys}`; column type is the primitive guess → path family/
  choice/duration/size labelled `(text)`, branch-A backlog).
- "No worse than runtime2" proven for everything through part 1 (plang fail list identical to
  cf66e2d01; the 6 Wire snapshot failures are pre-existing/identical on runtime2).

## WIP — stage 2 part 2 (C), in `git stash` (NOT committed; branch stays green)

`stash@{0}` on `llm-writer`: "stage2-part2-C-wip". Restore with `git stash apply stash@{0}`
(worktree `/workspace/llm-writer`; `git stash list` to confirm). It contains:
- `message.Content` changed from C# `string` to a plang value `text.@this?` (the text template, kept
  UNRENDERED at materialize) — `PLang/app/module/llm/type/message/this.cs`. Plus a 3-arg `Create`
  that builds a message from a dict itself (so the reflection binder never lowers a string into text).
- `OpenAi` renders each message's content through the llm writer ONCE, right after materialize
  (before hooks/cache/snapshot/body) — `RenderContent` helper; all downstream `Content` reads use
  `?.ToString()`; the static `Writer.Cut` was removed (dead).
- `debug/this.cs`: the two `m.Content` subscriber reads → `?.ToString()`.
- `settings_golden.json` re-pinned: **+`rows: number = 100`** only (verified diff).
- New test `RankOrdersEndToEndTests.cs` + assertion fixes in `DataAsTResolutionTests.cs` and
  `LlmTypeTests.cs` (Content is now a value → `.ToString()`).

(C) is FUNCTIONALLY DONE and proven: `ask llm "rank these %orders%"` tables end-to-end through the
request body (RankOrders green); conversation-continue green; **all 1295 Modules tests green**; build
0 PLNG. Architect confirmed (decisions 800/802) the render-once-after-materialize point and guess+doc
for column types.

## THE ONE OPEN ITEM — move 7 tests off LlmMessage (architect decision (a))

`message.Content` is now a `text` value, not a `string` leaf — so it no longer resolves its `%var%`
AT MATERIALIZE (it renders when the provider sends it; the LLM still sees it resolved — RankOrders
proves it). These 7 general type-system/resolution tests used `LlmMessage` as their string-leaf
example and assert resolution at materialize. **Move each to a record with a plain `string` field, or
to `list<dict>` (deep-resolves its string leaves), keeping what they assert unchanged. Name each in
the commit.** (Architect: "they're yours to move with this change.")

1. `PLang.Tests/Generator/Generator/Matrix/Resolution/ResolutionTests.cs` — `DeepResolutionList_NestedDict_SubstitutesInside`, `DeepResolutionList_NestedListsAndDicts_FullyWalked` (fixture `DeepResolutionList` in `Matrix/Resolution/Handlers.cs` has a `list<LlmMessage>` slot — change the slot or read as `list<dict>`).
2. `PLang.Tests/Generator/Generator/Matrix/DataWrapped/…` — `DataWrappedList_NestedVarInDict_DeepResolvesAndTypes` (fixture `Matrix/DataWrapped/Handlers.cs`, `list<LlmMessage>` slot).
3. `PLang.Tests/Data/App/DataTests/DataResolutionTests.cs` — `DeepResolution_ListWithVar_RefreshesPerCall`, `AsT_ListObjectSlot_AsListLlmMessage_StoredLeavesNotReResolved`.
4. `PLang.Tests/Types/App/ConversionGapTests/DictListToRecordTests.cs` — `SingleDict_To_DomainRecord` (this one is a no-var CONVERSION test; it may just need the `Content` assertion on `.ToString()`, or a plain-string record — judge it).
5. `PLang.Tests/Runtime/App/SingularNamespaces/BuilderSchemaTests/ScopeTests.cs` — `AListOfMessages_ReadAsLlmMessages_RendersItsVariables`.

## After the 7 tests (to finish part 2)

- Rebuild; confirm those suites green and no others moved.
- Builder byte-equal: a real `plang build` / the decider pins — the builder's llm.query content is
  full-match text, rendered through the writer = same bytes under 100k (settings_golden already +rows,
  re-pinned).
- Full suite vs runtime2 (cf66e2d01, worktree `/workspace/rt2-baseline`): the ONLY remaining C#
  failures must be the 6 pre-existing Wire snapshot tests (identical on runtime2); plang fail list
  identical.
- Commit part 2 (message + OpenAi + tests + settings_golden), push, send the architect Done + hash +
  the suite comparison. Then stage 3 (images/binary → `[mime, size]`) and stage 4 (`%x.trim%`).

## Settled sub-decisions (don't re-litigate)
- markdown column type as suffix `id (number)`; dict overflow `--- N more keys ---`; nested cell
  `[list of N]`/`{dict of N keys}`; default length 100,000, rows 100; guess+doc for column types
  (path family/choice/duration/size → `(text)`), real-type-without-envelope on branch A's list.
