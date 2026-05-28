# docs — data-normalize summary

**Version:** v1

## What this is

`data-normalize` lands the structural-normalize pipeline on `app.data.@this`:
a uniform `primitive | byte[] | Data | List<>` tree walker (`Normalize`)
feeds an `IWriter` format-encoder protocol (JSON impl first; protobuf/CBOR
later as siblings) and a reverse walker (`Reconstruct<T>`) with per-type
hooks. Replaces the old "every domain type gets its own JsonConverter"
pattern. `WireJsonConverter` renamed to `Wire`. New `[Masked]` attribute and
`View.Store` carve-out for local persistence.

This docs pass propagates that landing into the canonical docs and sweeps
the stale `WireJsonConverter` name out of the repo.

## What was done

- **New architecture section** — `Documentation/Runtime2/data-spec.md §16a`
  "Wire shape: `Normalize` → `IWriter` → bytes". Covers all six new surfaces
  with dispatch tables and the typed `NormalizeException.Key` matrix.
- **New rule** — `Documentation/v0.2/good_to_know.md` "Domain types ride
  the wire as property bags, not bespoke JSON converters". Codifies the
  pattern future contributors must follow when adding a wire-bound type.
- **Rename sweep** — `WireJsonConverter` → `Wire` across CLAUDE.md (the
  "data is not enveloped" anchor), `architecture.md` (×3), `app-tree.md`,
  `callbacks.md` (×4), `data-spec.md` (×2), `good_to_know.md` (×1). Every
  remaining occurrence is now an explicit "renamed from" breadcrumb.
- **XML doc fix** — `PLang/app/data/Wire.cs:6-9` said four-field; emits
  five-field. Aligned and noted the Normalize pipeline.
- **CLAUDE.md anchor** — line 23 updated with `Wire` class name, the
  Normalize → IWriter pipeline note, and a cross-reference to the new
  good_to_know.md rule.

No CLAUDE.md proposals or character proposals were filed on this branch.

## Code example

The wire-shape change for domain types — `path` previously had a bespoke
`path.JsonConverter` that wrote a bare string; now it ships through
`Normalize` as a tagged property bag:

```csharp
// Before (deleted on data-normalize):
public class PathJsonConverter : JsonConverter<path.@this> { /* …bespoke… */ }

// After: just tags on the type
public abstract class @this
{
    [Out] public string Scheme   { get; }   // ships
    [Out] public string Relative { get; }   // ships
    public string Absolute       { get; }   // does not ship (would leak fs)
}
```

Wire output:
```json
{ "name": "p", "type": "path", "value": { "scheme": "file", "relative": "foo/bar" } }
```

Reconstruction picks up `path`'s built-in hook in
`this.Reconstruct.cs` — reads `relative`, calls `path.Resolve(relative, ctx)`,
returns the scheme-correct subclass.

## Files modified

- `CLAUDE.md`
- `PLang/app/data/Wire.cs` (XML doc only)
- `Documentation/Runtime2/data-spec.md`
- `Documentation/v0.2/app-tree.md`
- `Documentation/v0.2/architecture.md`
- `Documentation/v0.2/callbacks.md`
- `Documentation/v0.2/good_to_know.md`

## Verdict

**PASS** — branch ready to merge. Auditor v1 already PASSed the code; docs
flagged no missing PLang examples or unclear-intent code, so no further bot
work is needed.
