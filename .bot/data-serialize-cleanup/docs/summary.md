# docs — `data-serialize-cleanup`

**Version:** v1
**Status:** PASS — ready to merge.

## What this is

`data-serialize-cleanup` is a five-stage branch that cleaned up PLang's Data
serialization pipeline. The architect's plan (`architect/stage-1` …
`stage-5`) tightened `ISerializer`'s input to `Data`, merged the two PLang
serializers into a single `application/plang`, flattened the
Compress/Decompress shape, gave Properties their own wire scope with a new
`!` access operator, and swept the word "envelope" out of the vocabulary.
Coder, codeanalyzer (v2), tester (v2), security (v2), and auditor (v1) all
passed before I started.

My job (the docs gate) was: walk every public doc that mentions the
surface this branch touched, fill the gaps, evaluate the one CLAUDE.md
proposal, and decide whether the branch is merge-ready.

## What was done

Eight files edited:

- `Documentation/v0.2/io-channels.md` — channel-hook renames
  (`WriteCore/ReadCore/AskCore` → `Write/Read/Ask`), `WriteAsync` takes
  typed `Data`, envelope vocab swept.
- `Documentation/v0.2/callbacks.md` — replaced `application/plang+data`
  section with the merged `application/plang`; replaced the
  ICallback-only lazy-signing carve-out with "Sign-if-missing — the
  converter does it"; wire JSON example now five fields.
- `Documentation/v0.2/architecture.md` — channel `Mime` (not
  `ContentType`); registry uses `GetByType`; `this.Envelope.cs` →
  `this.Transport.cs` in the directory tree; callbacks section
  re-anchored.
- `Documentation/v0.2/good_to_know.md` — five spot edits scrubbing
  `Data.Envelope`, `_envelopeJsonOptions`, `GetByContentType`,
  `plang+data`, and "signed data envelope" vocab.
- `Documentation/v0.2/app-tree.md` — Data tree updated.
- `Documentation/Runtime2/data-spec.md` — §15 "Envelope Pipeline" →
  "Transport Pipeline" (flat Compress shape, Properties round-trip,
  signature round-trip); **new §15a "Properties — sidecar metadata"**
  (insertion gate, `.` vs `!` access table, write surface, malformed
  shapes); §16 expanded with sign-if-missing + canonicalization rules.
- `Documentation/v0.2/variables.md` — Properties section rewritten to
  the new shape + `!` operator + malformed-shape gate.
- `CLAUDE.md` (repo root) — applied coder's "Data is not enveloped"
  proposal alongside the System.IO/Console.* bans.

Plus the v1 deliverables:
- `.bot/data-serialize-cleanup/docs/v1/plan.md`
- `.bot/data-serialize-cleanup/docs/v1/result.md` (CHANGELOG)
- `.bot/data-serialize-cleanup/docs/v1/verdict.json`
- `.bot/data-serialize-cleanup/docs-report.json`

## Code example — the new wire shape and `!` operator

The single biggest user-facing change is Properties getting their own wire
scope and a new operator. Two stores on the same Data, two operators:

```plang
- llm system "you are a translator", user "%text%", write to %resp%
- log "translation: %resp.text%"           / Value.text (existing . operator)
- log "tokens used:  %resp!TotalTokens%"   / Properties["TotalTokens"] (new ! operator)
- log "model name:   %resp!Model%"         / Properties["Model"]
- set %resp!Reviewed% = true               / writes Properties["Reviewed"]
```

On the wire, the Data shape is five fields:

```json
{
  "name": "resp",
  "type": "data",
  "value": { "text": "...", ... },
  "properties": { "TotalTokens": 1500, "Model": "claude-opus-4-7" },
  "signature": { ... }
}
```

`properties` is omitted when empty; `signature` is omitted when null. The
historical "envelope" mental model is gone — Data IS the wire shape.

## CLAUDE.md proposal

One proposal (from coder, 2026-05-27): "Data is not enveloped." Decision:
applied. Target was specified as `/PLang/App/CLAUDE.md` but no such file
exists in the tree — the project's canonical CLAUDE.md is at the repo
root, so it landed there. One wording fix when applying: `app.data.Json`
→ `app.data.WireJsonConverter` (Json.cs is the STJ wrapper; the wire
shape is owned by WireJsonConverter).

## What's next

Merge. Audit finalized PASS, security PASS, tester PASS, codeanalyzer
PASS, docs PASS. No PLang example gaps to flag — tester wrote
`Tests/Llm/LlmProperties.test.goal`, `Tests/Serialization/NegationPrefixStillParses.test.goal`,
and the Compress round-trip suite during their pass.
