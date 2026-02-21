# compression-settings — Architect Summary

## v1 — ISettings Design
Designed a general-purpose settings mechanism for Runtime2. Each module declares a strongly typed `ISettings` class; the source generator produces scope-aware property resolution, a settings action handler, and a builder registry. Settings are goal-scoped (inherit to subgoals, reset on completion) with a `Default` flag for engine-level persistence. Navigation is `engine.Module<Archive>().Settings.Max` — strongly typed, no strings. First use case: replace hardcoded 100MB gzip bomb limit in Data.Envelope.cs. See [v1/summary.md](v1/summary.md).
