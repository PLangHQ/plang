# coder — purge-systemio-from-actions

## v1 — 2026-05-25

Implemented Stages 1–4 + 7 fully and Stage 5 partial (test/discover —
the brief's headline — plus test/report). The deferred ring-2 handlers
needed new verb-surface infrastructure (Execute verb,
`path.LoadAssemblyAsync`, `path.ReadAsBase64`) that would be a
substantial own scope. Stage 6 (flip PLNG002 to error) deferred until
Stage 5 finishes the ~142 remaining warning sites. Suite: 92/3025
failing. Details in `v1/handoff.md`.

JsonConverter wiring used AsyncLocal `DeserializationScope` initially —
flagged for redesign by Ingi's review.

## v2 — 2026-05-25

All seven stages now landed.

- Stage 3 design pivoted per Ingi: AsyncLocal scope removed,
  `PathJsonConverter` now takes Context in its constructor. Per-Actor
  `channels.serializers` bakes a Context-bound converter into its
  options; `Conversion.TryConvertTo` builds a one-shot Context-bound
  options bag per call. No ambient state.
- Stage 5 closed — settings/Sqlite (D9b), llm/OpenAi (D9a content-shape
  via new `path.ReadAsDataUri`), module/add + code/load + code/Snapshot
  (D8 with new `Execute` permission verb + `path.LoadAssemblyAsync`),
  ui/Fluid + http/Default file providers, debug traces,
  modules.MarkdownTeaching root, plus AppGoals' Load/Walk path lifts
  and goal-format-for-llm template read.
- Stage 6 — PLNG002 flipped to **Error** severity. PLang and
  PlangConsole build clean with zero PLNG002 warnings. Analyzer
  allowlist extended to cover pure path-arithmetic methods
  (Path.Combine etc.) — they're string transformations, not IO.

Suite: 101/3025 failing — ~75 test-designer stubs whose surfaces now
exist and need real bodies, ~26 test fixtures needing Path-typed
wiring. PLang and PlangConsole build clean. The "purge" promise —
every filesystem reach in production C# routes through AuthGate — is
enforced by compilation. Details in `v2/handoff.md`.
