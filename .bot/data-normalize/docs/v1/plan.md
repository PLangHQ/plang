# docs v1 — data-normalize

**Auditor verdict:** PASS. Next-bot directive: propagate the wire-shape additions
(`[Masked]`, `View.Store`, `IWriter`, `Normalize`/`Reconstruct<T>` tree-walker,
`Tagged` filter) into `Documentation/Runtime2/data-spec.md` + `good_to_know.md`,
and align the "data is not enveloped" canonical example with the new `Wire.cs`
filename.

No CLAUDE.md proposals on this branch. No character proposals on this branch.

## Gaps found

### 1. Stale `WireJsonConverter` references — file/class renamed to `Wire`
Auditor flagged this. Every doc reference to `WireJsonConverter.cs` /
`WireJsonConverter` is stale. Inventory:
- `CLAUDE.md:23` — the load-bearing "data is not enveloped" anchor.
- `Documentation/v0.2/architecture.md:399, 507, 531`
- `Documentation/v0.2/app-tree.md:170`
- `Documentation/v0.2/callbacks.md:77, 89, 90, 140`
- `Documentation/v0.2/good_to_know.md:588`
- `Documentation/Runtime2/data-spec.md:224, 254`

### 2. Stale XML doc on `Wire.cs` itself
`PLang/app/data/Wire.cs:6-9` summary still says "canonical four-field shape
`{name, type, value, signature}`" — the implementation emits the five-field
shape (line 400 emits `properties`). Fix the summary.

### 3. Missing architecture doc for the structural-normalize landing
None of `Documentation/v0.2/` or `Documentation/Runtime2/` currently describes
the new pipeline. Five new surfaces need docs:
- **`[Masked]` attribute** — masking sibling to `[Sensitive]`, value replaced
  with `"****"`, name still visible. Canonical case `setting.value`.
- **`View.Store`** — local-persistence view that ignores `[Sensitive]`/`[Masked]`
  so Identity can round-trip its PrivateKey through Sqlite while `View.Out`
  excludes it from the wire. Threaded into `Wire` per-instance, not AsyncLocal.
- **`IWriter` + `json.Writer`** — format-encoder protocol. `Normalize()` walks
  the in-memory tree into uniform `primitive | byte[] | Data | List<>`, then
  the writer emits format-specific bytes with no reflection. JSON is the first
  impl; protobuf / CBOR ship later as siblings.
- **`Tagged` filter** — `(type, View)`-cached property selector. Replaces the
  old hard-coded `View.Out` discipline; `View.Store` carve-out lives here.
- **`Normalize` + `Reconstruct<T>`** — paired tree-walkers with explicit
  `NormalizeException.Key`-typed errors (`NormalizeCycleDetected`,
  `NormalizeMaxDepthExceeded`, `NormalizeNoReconstructionStrategy`,
  `NormalizeContextRequired`, `NormalizeMissingRelative`,
  `NormalizeReconstructFailed`, `NormalizeGetterThrew`,
  `NormalizeUnexpectedLeafType`). Bounded by 128-depth cap + visited-set.

### 4. Wire-shape change: domain types now ride as property bags
Old `path` JSON converter wrote a bare string; new wire writes
`{scheme, relative}`. `Identity` ships `{name, publickey}`. `setting`
ships `{key, value: "****"}`. Callers reading wire JSON or pinning shape need
to know.

### 5. `Data.RawSignature` deletion
Removed; all callers migrated to `Signature`. No doc mentions it today (good),
but the `Data` API description in `data-spec.md` should not regress to "Signature
has lazy-populate semantics" — confirm it doesn't.

### 6. Auditor's two minor findings — surface in `good_to_know.md`?
- Reconstruct positional-ctor silent default on missing required ref params.
- Comment vs behaviour on longest-ctor pick.

These are scoped, narrow, no current callers hit them. Document as a
follow-up note inside the new architecture section, not as a separate
`good_to_know.md` rule (would over-state the size of the footgun).

## Plan

### A. Sweep `WireJsonConverter` → `Wire` (10 sites across 6 files)
Mechanical rename. Where the prose currently says "`WireJsonConverter.Write`"
it becomes "`Wire.Write`" (the class is `Wire`, method stays `Write`).
File references `WireJsonConverter.cs` become `Wire.cs`.

### B. Fix `Wire.cs` XML doc summary
Four-field → five-field. Sentence in line 8 specifically.

### C. Write the architecture doc for structural-normalize
Add a new top-level section to `Documentation/Runtime2/data-spec.md`:
**"§17 — Structural normalization: `Normalize` → `IWriter` → wire bytes"**.
Covers the five new surfaces (above), the per-domain-type wire shapes that
flow from `[Out]` tagging, and the round-trip via `Reconstruct<T>` with
its typed error keys.

### D. Add `good_to_know.md` rule
**"Domain types ride the wire as property bags, not bespoke JSON forms."**
Why: explains what changed and where bespoke converters (path) went.
How to apply: a new domain type added to the wire only needs `[Out]` tags
on the properties that should ship — no separate `JsonConverter`.

### E. Add v0.2 architecture-doc landing entry
A short reference in `Documentation/v0.2/architecture.md` pointing at the new
data-spec section, so the v0.2 tree is discoverable.

### F. Update CLAUDE.md "data is not enveloped" anchor
Per auditor's direct instruction. The bullet on line 23 references
`app.data.WireJsonConverter` and `WireJsonConverter.Write`; both become
`app.data.Wire` / `Wire.Write`.

### G. CHANGELOG entry
No CHANGELOG file exists in the repo today. Capture user-visible changes in
`v1/result.md` per the docs character spec.

### H. Verify
- Re-grep for any remaining `WireJsonConverter` references after the sweep
  (must be zero outside `.bot/`).
- Run `Documentation/v0.2/scripts/check-app-tree.sh` — the app-tree update
  on app-tree.md should not break the checker.

## Verdict shape
Auditor flagged no missing PLang examples or unclear code, only doc propagation.
Expected verdict: **PASS** — branch ready to merge once docs land.
