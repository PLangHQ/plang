# Security v1 — `data-serialize-cleanup`

**Date:** 2026-05-28
**Branch:** `data-serialize-cleanup`
**Base:** merge with `origin/runtime2` at `e41d650a`
**Range reviewed:** 80 commits, 91 production C# files changed.

## Scope

This branch consolidates PLang's wire serialization:

- Stage 1: `ISerializer` input/return tightened to `Data` (away from `object? + Type`).
- Stage 2: merge `application/plang` serializers, move sign-if-missing into the wire converter, canonicalize hash bytes through the same outbound options.
- Stage 3: flatten `Compress`/`Decompress` to a single `{type=archived, value=byte[]}` outer.
- Stage 4: `Properties` get a nested wire scope (`"properties": {...}`) + the `!` operator for variables.
- Stage 5: vocabulary sweep (drop "envelope").

Codeanalyzer v2 PASSED yesterday after coder addressed 11 findings; tester v2 PASSED with mutation verification. My job is to look for security-class gaps the structural review/test suite would not catch — depth bombs, asymmetric round-trips, sign-skip carve-outs, sensitive-leak surfaces, hash/wire divergence.

## Process

1. Read the wire converter, canonicalization (crypto.Hash), the merged plang serializer, the `!` operator parser, Properties' insertion gate, Compress/Decompress, and the Sensitive filter wiring.
2. Run `scripts/semgrep-scan.sh` (the standing baseline).
3. Reason about: DoS via deserialization (depth, expansion, recursion), signing carve-outs, [Sensitive] field exposure on the transport serializer, asymmetric round-trips through Properties.
4. Validate the v1 fixes still match the v2-PASS code by direct reading.

## Findings preview

- F1 (medium) — StackOverflow DoS via nested-Data wire deserialization. `LiftDataIfShaped` round-trips through `GetRawText()` → `Deserialize<@this>(string, options)`, resetting STJ `MaxDepth` on every Data level. Stack overflow is unrecoverable.
- F2 (info / standing) — sign-if-missing silently no-ops when `Context?.Actor == null`. Documented carveout; receive-side verify must catch.
- F3 (low / standing) — `application/plang` serializer intentionally omits the `[Sensitive]` filter. Stage 2 widens the surface of channels relying on plang transport.
- F4 (info) — `Properties.EnsureSupportedValue` top-level only; smuggled Data inside `IDictionary`/`IEnumerable` round-trips asymmetrically (writes as signed Data, reads back as dict).

See `security-report.json` for the full machine-readable form.
