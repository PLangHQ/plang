# Action Modifiers — Architect Summary

**v1** — Designed action modifiers pattern: onError/cache/timeout become regular `module.action` records with `[Modifier(Order)]` attribute. LLM outputs flat list, builder groups on save, runtime folds via `IModifier.Wrap()`. Per-action precision, composable, extensible. Includes detailed 4-phase implementation roadmap for test-designer and coder. See [v1/summary.md](v1/summary.md) and [v1/roadmap.md](v1/roadmap.md).
