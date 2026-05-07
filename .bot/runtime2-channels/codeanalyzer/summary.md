# Codeanalyzer Summary — runtime2-channels

**v1** (2026-05-07): FAIL. 2 bugs + 1 latent crash + 3 issues across Channels, GoalChannel, Services, and the channel.set action. See `v1/result.md`.

**v2** (2026-05-07): FAIL. Coder v5 closed F1/F4/F5/F6. F2 partially closed (GoalChannel path fixed; `RunGoalAsync(GoalCall)` general path not). F3 not closed (logs null-ctx but doesn't return early — handlers still NRE). 2 new items: test quality (sequential test named parallel) + OBP smell (Channel.Events is public List<>). See `v2/result.md`.
