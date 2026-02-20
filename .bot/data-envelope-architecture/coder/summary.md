# Data Envelope Architecture — Coder Summary

**v1** — Phase 1: Created `Engine.Types` class consolidating PLang name ↔ CLR type, extension → Kind/MIME, and compressibility data into a single live instance on Engine. Additive change — old static TypeMapping untouched. 62 tests pass. See [v1/summary.md](v1/summary.md) for details.
