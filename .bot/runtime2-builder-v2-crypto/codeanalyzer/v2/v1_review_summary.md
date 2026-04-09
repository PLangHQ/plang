# v1 Review Summary

## Review source
Direct feedback from Ingi (project owner).

## What was flagged
**Critical miss: "Behavior methods never throw" rule not applied to providers.**

My v1 analysis caught the redundant algorithm validation in `DefaultProvider.Verify` but completely missed the deeper problem: `ICryptoProvider.Hash()` returned `byte[]` and `ICryptoProvider.Verify()` returned `bool` — both throwing `NotSupportedException` for unsupported algorithms. This violates the "behavior methods never throw" rule that's explicitly in CLAUDE.md and my own memory.

The handlers caught these exceptions in `try/catch` blocks and converted them to `Data.FromError(...)`, but the correct design is for the provider itself to return `Data` and never throw.

## What the coder changed
1. `ICryptoProvider.Hash()` now returns `Data` (not `byte[]`)
2. `ICryptoProvider.Verify()` now returns `Data` (not `bool`)
3. `DefaultProvider` returns `Data.FromError()` instead of throwing `NotSupportedException`
4. `DefaultProvider.Verify` simplified — calls `Hash()`, checks `.Success`, returns `Data.Ok(bool)`
5. Handler `try/catch` blocks removed (except `Convert.FromHexString` in verify — correct boundary catch)
6. Tests updated: `ThrowingCryptoProvider` replaced with `FailingCryptoProvider` that returns `Data.FromError`
7. Tests verify error key propagation, not exception types

## What I should have caught
The "behavior methods never throw" rule applies to **all domain code returning Data-compatible results**, not just handler `Run()` methods. Provider interfaces are part of the domain — they should return `Data` so errors flow through the normal pipeline with descriptive keys and status codes. Exceptions should only appear at system boundaries (framework calls like `Convert.FromHexString`, file I/O, etc.).
