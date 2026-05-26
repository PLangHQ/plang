# Plan — v2 (verification of coder's response to v1)

## Goal

Verify the three fixes land cleanly and there's no new attack surface.

## Steps

1. Read coder's two commits' diffs end-to-end.
2. **F1** — read `file.@this`'s ctor + `Canonicalize`; trace each
   FilePath construction site (Resolve, JsonConverter, implicit operator,
   direct construction) and confirm rooted-with-`..` always reaches
   `PathHelper.GetFullPath`. Note the deliberate skips (empty,
   non-rooted, `//`-prefixed) and reason about whether any of them
   re-introduce the bypass. Read the new regression test.
3. **Mutation test (F1)** — temporarily neuter `Canonicalize` so it
   returns its input verbatim; confirm all three F1 regression tests
   fail; revert. Announce before, revert immediately.
4. **F2** — read the new `MarkdownTeaching.cs`; confirm every disk
   touch is a `path.@this` verb. Read `ResolveMarkdownTeachingRoot` —
   `string` override now routes through `path.@this.Resolve(System.Context)`.
5. **PathHelper contract** — read PathHelper.cs; confirm body is pure
   name math only (no `File.*`, `Directory.*`, no IO). Confirm PLNG002's
   `Path.*` carve-out points at PathHelper.cs alone.
6. **PLNG002 narrowing** — read the new Plng002.cs; confirm no
   whole-file exemptions remain (no `MarkdownTeaching.cs`,
   `app/this.cs`, or `AllowedSystemIoPathMembers`). Confirm the two
   carve-outs are the only IsScannedFile-style exits.
7. Write verdict + summary + commit.

## Findings status going into v2

- F1 (HIGH) — should close.
- F2 (LOW, latent-Medium) — should close.
- F3 (LOW, narrow-the-scope finding from v1 update) — should close.

If all three close cleanly and mutation test confirms F1, verdict is **PASS**.
