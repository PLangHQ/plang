# Docs v1 plan — `data-serialize-cleanup`

**Date:** 2026-05-28
**Branch:** `data-serialize-cleanup`
**Entering:** codeanalyzer v2 PASS, tester v2 PASS, security v2 PASS, auditor v1 PASS. Coder folder absent (process gap already flagged by tester + auditor — not docs's job to escalate again).

## What changed on this branch (the surface I need to document)

Five staged shifts in serialization, all behavioural-shape changes that touch published docs:

1. **Stage 1** — `ISerializer` input tightened: takes `Data`, not `object? + Type?`. `ContentType` → `Type`, `FileExtension` → `Extension`. Channel hooks renamed `WriteCore`/`ReadCore`/`AskCore` → `Write`/`Read`/`Ask`. `SerializeOptions.ContentType` → `Type`. Registry: `GetByContentType` → `GetByType`.
2. **Stage 2** — `application/plang+data` merged into `application/plang`. `Envelope` class deleted from `plang/Data.cs`; file deleted. Signing moved into `app.data.WireJsonConverter` (sign-if-missing, idempotent, per-Data on the walk). Canonicalization in `crypto.Hash` now uses `plang.@this.OutboundOptions` — hash bytes ≡ wire bytes.
3. **Stage 3** — `Compress`/`Decompress` flattened: archived `Value` is a `byte[]` directly, no inner `gzip` Data, no `RehydrateNestedData` walk. Routing goes through the registered `application/plang` serializer.
4. **Stage 4** — `Properties` is `IDictionary<string, object?>` with primitive-only insertion gate. New wire field `properties` (omitted when empty) — nested object, not flat top-level. New `%x!key%` operator dereferences Properties (vs. `%x.field%` for Value). `variable.set %x!key% = value` writes Properties entries. `Variable.IsMalformed` flag rejects shapes like `%x!!cost%`, `%x.y!cost%`, `%!x!cost%`.
5. **Stage 5** — vocabulary sweep: "envelope" removed from the public surface. `this.Envelope.cs` → `this.Transport.cs`. Doc comments rewritten. `Wrap`/`Unwrap` stay (they describe category wrapping, not enveloping).

Auditor additions worth noting in docs:
- `plang.@this.ContextLessFallback` — static fallback serializer when no context is available.
- `WireJsonConverter`'s per-reader `MaxDepth=64` is the load-bearing depth cap; the `AsyncLocal<int> _readDepth` ladder added by coder is defense-in-depth.
- `LiftDataIfShaped` heuristic — fires only inside a Data's `value` slot, never inside `properties`. A user dict with both `"name"` and `"value"` keys inside `Value` *will* lift back as a Data on round-trip. By-design.

## Documentation gaps — by file

### `Documentation/v0.2/io-channels.md`
- L48 `WriteAsync(string, object?, string?, ...)` is stale — signature is now `WriteAsync(channelName, Data data, string? type = null, ...)`.
- L64 paragraph names the abstract hooks `WriteCore`/`ReadCore`/`AskCore` — renamed to `Write`/`Read`/`Ask`.
- L85-87 abstract member signatures need rename.
- L95 lifecycle paragraph mentions the renamed hooks.
- L104 ASCII tree (`AskCore returns Suspend`) — rename to `Ask`.
- L131, L209 "AskCore" prose mentions — rename.
- L135 "Data envelope lands in the goal as `%!data%`" — kill envelope vocab; "the Data lands as `%!data%`".
- L264 Stage-9 paragraph references `MigrationEnvelope` (a deleted prototype) — vocabulary check; the historical name is fine inside the parenthetical history note but should be quoted/code-fenced.

### `Documentation/v0.2/callbacks.md`
- L3 envelope vocab.
- L61 "vet the envelope".
- L73 mentions `_options` on `PlangDataSerializer` + `SensitivePropertyFilter.Strip` against `Data.@this._envelopeJsonOptions` — both names are gone. Rewrite around the merged `application/plang` serializer with `Transport.ForOutbound` + `Sensitive.Strip` modifiers on the composed Json engine.
- L81-88 entire "`application/plang+data` mimetype" section — collapse. Merged into `application/plang`. The wire serializer is `app.data.WireJsonConverter`. EnsureSigned now fires inside the converter walk (sign-if-missing), not at the serializer's Write entry.
- L88 wire JSON example needs the new `properties` field.
- L113 `app.Callback.Signature.Expires` comment "envelopes" — vocab.

### `Documentation/v0.2/architecture.md`
- L392 "Each channel has a Stream, Direction (Input/Output/Bidirectional), and ContentType." — the property is still `Mime` on the channel config (verified in source); ContentType wording dates to an older revision but isn't load-bearing. Confirm and leave or rename `ContentType` → `Mime`.
- L507 "wire serializer is `application/plang+data` (`PlangDataSerializer`)" + "reading `data.Signature` on Write triggers lazy signing only when the wrapped value is an `ICallback`" — both stale. New: `application/plang` is the single wire serializer; signing fires sign-if-missing on every Data the converter walks. Drop the ICallback carve-out.
- L529 directory tree `this.Envelope.cs` → `this.Transport.cs`.

### `Documentation/v0.2/good_to_know.md`
- L396 RehydrateNestedData throw-converts-to-Data.FromError example — `RehydrateNestedData` no longer exists. Replace with a still-valid example or delete the bullet (the rule it illustrates still stands; the named method is gone).
- L477, L481 `Data.Envelope Compress` and `_envelopeJsonOptions` — both renamed. Update to `Data.Transport Compress` and "the merged `application/plang` serializer's options chain (`Transport.ForOutbound` + `Sensitive.Strip` modifiers)".
- L574, L576, L584, L588 "signed data envelope" / "envelope's Algorithm" / "envelope carries its own identity" / "Data.Signature holds the `SignedData` envelope" — kill all four "envelope" references. The thing on the wire is the Data itself; the cryptographic signature is a sub-record (`Signature`) attached to it.
- L1278 paragraph still lists `(Json, Text, plang, plang+data)` — drop `plang+data`.
- L1290 `Serializers.GetByContentType` — rename to `GetByType`.

### `Documentation/v0.2/app-tree.md`
- L156 "universal result envelope" — change to "universal result wrapper" (or just "Data").
- L165 tree entry `├── Envelope wrapping  (this.Envelope.cs)` → `├── Transport pipeline (this.Transport.cs)`.
- Add the new files alongside `Data` if I find them missing: `WireJsonConverter.cs`, `Properties.cs` (existed, but the shape changed).

### `Documentation/Runtime2/data-spec.md`
- L196 "§15. Envelope Pipeline" → "§15. Transport Pipeline".
- L202 "Wrap: Creates a category envelope." → "Wrap: Creates a category wrapper Data."
- L219 "Unwrap: If Value is a Data, returns it (strips envelope)." → "(unwraps the outer Data)."
- Add a section on Properties wire scope (the nested `properties` object, the `!` operator, primitive-only insertion gate) — this is the biggest user-visible doc gap.
- Add note on flat Compress shape (archived `Value` is `byte[]`, no inner Data).

### `Documentation/v0.2/variables.md`
- Document `%x!key%` Properties access. Today this file documents `!`-prefixed infrastructure variables (`%!data%`, `%!error%`) but says nothing about the new mid-expression `!` for Properties dereference. Add a short section: "`!` between identifier and key reads from Properties; `.` continues into Value." Mention the malformed shapes that fail validation. Cross-reference data-spec §Properties.

### `Documentation/v0.2/todos.md` / `Documentation/Runtime2/todos.md`
- Spot check for items completed by this branch; don't restructure.

## CLAUDE.md proposal — decision

There is one proposal on this branch (coder, 2026-05-27):

> **Target:** /PLang/App/CLAUDE.md
> **Rule:** "Data is not enveloped." — adds a paragraph forbidding parallel wrapper types ("Envelope", "Wire", "Wrapper") for Data's serialization shape; directs people to add `[Out]`-aware filters and let `app.data.WireJsonConverter` handle wire layout.

**Decision: apply.** This is genuinely canonical: the `Envelope` class on `plang/Data.cs` was a real load-bearing example of the smell, deleted on this branch. The rule applies to all future serialization work, not just this branch. It pairs naturally with the existing app-tree rules and with the truthiness / System.IO / Console.* prohibitions already in `/PLang/App/CLAUDE.md`.

One minor wording fix from coder's draft: the proposal says "the existing `app.data.Json` converter handles wire layout" — but the converter that actually handles the wire shape is `app.data.WireJsonConverter` (`Json.cs` is the STJ wrapper, not the Data wire converter). I'll fix the path when applying.

No character proposals to evaluate.

## What I write (in order)

1. `v1/plan.md` (this file). Set `report.plan` path on docs session.
2. `Documentation/v0.2/io-channels.md` edits.
3. `Documentation/v0.2/callbacks.md` edits.
4. `Documentation/v0.2/architecture.md` edits.
5. `Documentation/v0.2/good_to_know.md` edits.
6. `Documentation/v0.2/app-tree.md` edits.
7. `Documentation/Runtime2/data-spec.md` edits (with new Properties wire-scope section).
8. `Documentation/v0.2/variables.md` edits (new `!` operator section).
9. Apply the CLAUDE.md proposal to `/PLang/App/CLAUDE.md`.
10. `v1/result.md` — CHANGELOG-style summary of what shipped on this branch (user-visible).
11. `docs-report.json`, `v1/verdict.json`, `summary.md`, finalize `report.json`.
12. `git add -A && git commit && git push`.

No PLang `.goal` examples need to be written — the tester wrote Properties / `!` operator coverage already (`Tests/Llm/LlmProperties.test.goal`, `Tests/Serialization/NegationPrefixStillParses.test.goal`, the Compress round-trip suite). My job is the prose explaining them.

## Verdict prediction

PASS, assuming I find no missing PLang examples or unclear coder intent during the doc walk. If I hit one, I'll fail back to the right bot per character workflow.
