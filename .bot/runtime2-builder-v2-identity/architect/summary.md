# Identity Module — Architect Summary

**v1**: Architecture plan for identity module (Piece 1 of builder-v2). Per-actor identity management, `[Sensitive]` attribute, System.DataSource storage, lazy `%MyIdentity%` resolver. Ready for test-designer. See [v1/summary.md](v1/summary.md).

**v2**: Resolved two open questions from test-designer. (1) Duplicate name always errors, even if archived — names are identities. Added `unarchive` and `rename` actions. (2) `[Sensitive]` is serialization-only — dot navigation to PrivateKey works. Updated test expectations: 59 tests (49 C# + 10 PLang). See [v2/plan.md](v2/plan.md).
