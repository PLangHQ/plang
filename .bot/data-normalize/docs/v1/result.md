# docs v1 result — data-normalize

## Verdict: PASS — ready to merge.

Auditor v1 flagged no missing PLang examples and no unclear-intent code; the
work for docs was propagation of new surfaces and a sweep of stale class names.
Both done.

## What changed

### Docs additions
- **`Documentation/Runtime2/data-spec.md` §16a** — new section
  "Wire shape: `Normalize` → `IWriter` → bytes" covering `Normalize` (depth +
  cycle bounds, typed errors), `IWriter` + `json.Writer`, `Tagged` `(type, View)`
  filter, the `View.Store` carve-out + why signing stays Out-canonical even on
  Store paths, `[Masked]`, and `Reconstruct<T>` with its full
  `NormalizeException.Key` matrix. Two scoped follow-ups (positional-ctor
  required-param default, longest-ctor comment drift) recorded as known
  non-blocking.
- **`Documentation/v0.2/good_to_know.md`** — new rule "Domain types ride the
  wire as property bags, not bespoke JSON converters", with the smell and the
  carve-out (path's inbound bridge).
- **`Documentation/v0.2/app-tree.md`** — entries for `this.Normalize.cs` /
  `this.Reconstruct.cs` next to the Wire converter.
- **`Documentation/v0.2/architecture.md`** — directory tree updated with
  Normalize/Reconstruct filenames and the `Wire.cs` rename.

### XML doc fixes
- `PLang/app/data/Wire.cs:6-9` — class summary said four-field, code emits
  five-field. Aligned the summary with the implementation and pointed at the
  Normalize+IWriter pipeline.

### Rename sweep: `WireJsonConverter` → `Wire`
Auditor's directive. Updated every cross-reference in the docs so
`WireJsonConverter.cs` / `WireJsonConverter.Write` / `WireJsonConverter`
become `Wire.cs` / `Wire.Write` / `Wire`. Every remaining occurrence of the
old name is now an explicit "renamed from" / "was named" breadcrumb for
discoverability — none are stale.

Files touched:
- `CLAUDE.md` (line 23 — the "data is not enveloped" canonical anchor)
- `Documentation/v0.2/architecture.md` (lines 399, 507, 531)
- `Documentation/v0.2/app-tree.md` (line 170)
- `Documentation/v0.2/callbacks.md` (lines 77, 89, 90, 140)
- `Documentation/v0.2/good_to_know.md` (line 588)
- `Documentation/Runtime2/data-spec.md` (lines 224, 254)

### CLAUDE.md anchor update
The "data is not enveloped" bullet now points at `app.data.Wire`, the
five-field shape, and the new "domain types ride the wire as property bags"
rule.

## User-visible changes (CHANGELOG-style)

- **`Wire.cs`** is the new name for `WireJsonConverter.cs` (`app.data.Wire` /
  `app.data.WireJsonConverter`). Cross-references in code or scripts that
  grep on the old name need updating.
- **`[Masked]` attribute** (`app/View.cs`) — new. Property name ships visibly,
  value ships as `"****"`. Honored under `View.Out` and `View.Debug`; ignored
  under `View.Store`.
- **`View.Store`** — local-persistence view that ignores `[Sensitive]` and
  `[Masked]`. Used by `Sqlite.Set` to round-trip Identity (with PrivateKey)
  through local storage; the signature is still computed over the `View.Out`
  canonical bytes so post-load verification works.
- **`Data.RawSignature` removed.** All four callers migrated to `Signature`
  (which no longer has the legacy lazy-populate side effect).
- **Wire shape change.** Domain types that previously had bespoke JSON
  converters now ride as `[Out]`-tagged property bags via `Normalize`:
  - `path` ships as `{scheme, relative}` (was: bare string).
  - `Identity` ships as `{name, publickey}` only.
  - `setting` ships as `{key, value: "****"}`.
  Pinned-shape tests on the wire format need re-pinning to the new shape;
  consumers parsing the JSON of these types need updating.
- **`IWriter` + `json.Writer`** — new format-encoder protocol. The wire path
  no longer reflects per-type; `Normalize` decomposes once and the writer
  walks the tree. Protobuf / CBOR can ship later as sibling `IWriter` impls.
- **`Reconstruct<T>` typed errors** — every reconstruction failure now
  carries a `NormalizeException.Key` (`NormalizeCycleDetected`,
  `NormalizeMaxDepthExceeded`, `NormalizeNoReconstructionStrategy`,
  `NormalizeContextRequired`, `NormalizeMissingRelative`,
  `NormalizeReconstructFailed`, `NormalizeUnexpectedLeafType`,
  `NormalizeGetterThrew`).

## CLAUDE.md / character proposals

None received on this branch.

## Auditor follow-ups recorded

The two auditor minors are noted in `data-spec.md` §16a "Known follow-ups"
rather than filed as separate issues — they describe shape-level concerns
with no current callers triggering them. If a positional-record domain type
ever joins the wire vocabulary, the fix lands then.
