# coder — purge-systemio-from-actions

## v1 — 2026-05-25

Implemented all 7 stages of the architect's plan as a coherent shipped
state with two deliberate holes:

- Stages 1–4 + 7 fully landed.
- Stage 5 lifted the brief's headline offender (`test/discover` —
  AuthGate now gates `--test=/etc` instead of the homebrewed
  `StartsWith(rootPrefix)` check) plus `test/report`. The remaining
  ring-2 handlers (sqlite, OpenAI image, DLL loading via Execute
  verb + `path.LoadAssemblyAsync`, ui/Fluid + http/Default file
  providers, debug traces, modules.MarkdownTeaching) deferred —
  each needs new verb-surface infrastructure that's its own scope.
- Stage 6 (flip PLNG002 to error) deferred until Stage 5 finishes the
  ~142 remaining warning sites.

JsonConverter wiring chose AsyncLocal `DeserializationScope` over the
architect's post-pass approach, per Ingi's review — Path lands
fully Context-wired the moment it deserialises.

Suite: 92 / 3025 failing — about 75 of those are test-designer stubs
for Stage 5/6 behaviour, the rest are ~17 real regressions around
Goal serialise/deserialize round-trip + GoalCall name-resolution
edge cases. PLang and PlangConsole build clean. Details in
`v1/handoff.md`.
