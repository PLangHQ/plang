# Codeanalyzer Summary — runtime2-channels

**v1** (2026-05-07): FAIL. 2 bugs + 1 latent crash + 3 issues across Channels, GoalChannel, Services, and the channel.set action. See `v1/result.md`.

**v2** (2026-05-07): FAIL. Coder v5 closed F1/F4/F5/F6. F2 partially closed (GoalChannel path fixed; `RunGoalAsync(GoalCall)` general path not). F3 not closed (logs null-ctx but doesn't return early — handlers still NRE). 2 new items: test quality (sequential test named parallel) + OBP smell (Channel.Events is public List<>). See `v2/result.md`.

**v3** (2026-05-07): see `v3.md`.

**v4** (2026-05-07): PASS on coder v7 — B1 (`_active` static) and L1 (HashSet copy-on-write) closed correctly. See `v4/report.md`.

**v5** (2026-05-07): PASS on coder v10 (Console.* purge → channels). 5 Debug sites + 4 builder/LLM sites + interactive build prompt all routed through channels with behavioral parity. 3 minor coder-pickup notes (App prompt could use existing `Channels.WriteTextAsync` helper; builder/LLM sites discard returned `Data`; plan↔code drift on channel choice — code's choice is right). No OBP violations, no bugs, no latent crashes. Carry-over from v2 still open: `Channel.Events` public-list smell, untouched by v10. See `v5/report.md`.
