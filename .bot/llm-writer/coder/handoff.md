# llm-writer coder handoff (2026-10-16)

Coder: tester (plang-f2). `llm-writer` carries kind-is-type's foundation + stage-2 (parts 1 & 2).

## Where it is

Branch `llm-writer`, HEAD **5ac1bd8a8** (pushed). On it:
- Merged **origin/kind-is-type** (`954527ae0`, decision: Ingi "pull latest from the kind branch") —
  its type-system foundation (stage-4 "a format type owns its bytes", json is item's kind, source/
  use-open; F4.2 a kinded type's face `name<kind>`; host kinds Internal; file.read Type slot).
- **Stage 0** trace; **Stage 1** llm.Writer + `%!llm.setting.length%`=100,000 + full-match cut;
  **Stage 1b** the `BeginText`/`EndText` IWriter door (sink design; surface re-pinned under 800);
  **Stage 2 part 1** lists/dicts → markdown table (columns+types, `%!llm.setting.rows%`=100,
  `--- N more rows ---`; dict → `key (type): value`; nested cell `[list of N]`/`{dict of N keys}`;
  column type is the primitive guess → path family/choice/duration/size `(text)`, branch-A backlog).
- **Stage 2 part 2 (C)** `message.Content` is a plang text VALUE (template, unrendered); OpenAi opens
  it once at its boundary through the llm writer (`RenderContent`); message owns dict→message via its
  1-arg `Create` (context-free `KeyNames`/`Stored`) so Born AND Lower reach it. End-to-end green:
  `RankOrdersEndToEndTests` (rank these %orders% → a table in the body), `DictMessagesPathTests`
  (the builder's set-dict-list → llm.query), conversation-continue, all Modules llm tests.

The (C) WIP branch `llm-writer-wip-part2` is now SUPERSEDED (part 2 is committed here).

## Suite state (against the kind-is-type base)

My changes introduce ZERO new failures. Remaining failures are all INHERITED from the kind-is-type
merge — each verified failing on origin/kind-is-type too (worktree `/workspace/kind-baseline`):
- `ModulePageTests.TheFilePage_IsTheSpecsGolden` (kind's own "branch closed except TheFilePage").
- `HostRenderSpikeTests.LegA/B/C/D` (kind's host-element/property spike).
- `ScopeTests.AKnownVariableOfTheRightType_Passes` (F4.2: a number's face is now `number<long>`).
- 6 Wire snapshot/resume tests (pre-existing, also fail on cf66e2d01).
These are kind-is-type's test debt exposed by the merge — NOT this branch's; they need kind-is-type
(or a follow-up) to fix. `./dev.sh full` was run as the gate (see /tmp/full2.log + per-suite logs).

## Remaining plan
- **Stage 3**: an image/binary in a message → `[image/png, 2.1 MB]`, never inlined (the llm writer's
  Bytes/path rendering).
- **Stage 4**: `%x.trim%` gives the same view (a list → the table view; a text → its own trim).
- Then: tidy, the architect's sign-off.

## Settled (don't re-litigate)
Default length 100,000, rows 100; markdown column type as suffix `id (number)`; dict overflow
`--- N more keys ---`; nested cell `[list of N]`/`{dict of N keys}`; guess+doc for column types
(real-type-without-envelope on branch A); render-once-after-materialize in OpenAi (decision 800/802).
