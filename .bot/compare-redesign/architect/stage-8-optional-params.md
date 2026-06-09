# Stage 8 — FOLDED INTO Stage 2.1 Part C

This stage no longer stands alone. The optional-param null model — non-null `Data` (`Data.Uninitialized`, never a C# null), `?` as the optional signal, the generator-stamped `[System.Diagnostics.CodeAnalysis.NotNull]`, `[Default]` firing on null, and the `.Value(fallback)` door overload — is now **Part C of [Stage 2.1](stage-2.1-materialize-to-door.md)**.

**Why it moved (Ingi's call):** Stage 8 and Stage 2.1c rewrite the *same* source-gen Data-property getter emission, and Part A migrates the *same* handler sites. Doing them separately meant rewriting the getter twice and migrating optional-param sites twice (verbose, then clean). Folded together, it's one getter rewrite and one clean migration.

See **Stage 2.1 → Part C** for the full design.
