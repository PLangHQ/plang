# docs v1 — result

## Summary

Final docs gate for `collections-are-data`. All upstream bots PASS. Added four
new sections to `type-system.md`, one to `wire-serialization.md`, updated
the `good_to_know.md` index, and filed a three-bullet CLAUDE.md proposal
covering the new cross-cutting patterns. No C# docstring drift found — all
changed files carried fresh, accurate comments from the coder.

## Changes

### `Documentation/v0.2/type-system.md` — three new sections appended

**`dict.@this` and `list.@this` — native PLang collection types**

- Symmetric peers: `dict` = key-lookup + serialize as `{}`, `list` = index
  navigation + serialize as `[]`.
- Collections hold Data end-to-end — elements keep their type-tag and
  signature; nothing is decomposed to a raw CLR scalar on entry.
- `[JsonConverter]` governs raw-STJ view only (not the wire path); clarified
  this does not violate "domain types ride the wire as property bags."
- `dict.ToRaw()` / `list.ToRaw()` are read-out form only, not mutation paths.
- `dict` implements `IBooleanResolvable` + `IEquatableValue` (equality-only);
  `list` also implements `IOrderableValue` and `IListLeaf`.

**`Compare` — single typed-compare mediator**

- `app.data.Compare` is the one place both condition operators and `list.sort`
  route through — they cannot drift.
- Owns: null policy (null sorts last), coercion (`NormalizeTypes`), dispatch.
- `IEquatableValue` / `IOrderableValue` — value owns its compare; `ScalarComparer`
  (internal) is the one legal type-switch for CLR scalars.
- `NotOrderableException` for equality-only types or mismatched value types.
- Footgun called out: do not add `is MyNewType` arms to `Compare` — implement
  the interface and recurse back through `Compare` for children.

**List chunk/row model and `IListLeaf`**

- `list.@this` stores rows; public surface (`Count`, `Items`) is the flat view.
- `Add` is O(1) — appends a row without reading existing rows.
- `IListLeaf` = value-side "dissolve into my container list"; only `list.@this`
  implements it — dict/table/scalar stay whole. No type-switch in the container.
- `CopyStructure` on `add`/`set` prevents list-in-list write-through aliasing;
  nested dicts share by reference (intentional, auditor O1, future branch).

### `Documentation/v0.2/wire-serialization.md` — new section

**`@schema:"data"` marker — Data self-identifies on the wire**

- Written first by `Wire.Write`; recognized by `LiftDataIfShaped`,
  `LiftArrayElements`, and `@this.IsDataMarked`.
- Replaces the old name+value+type shape-sniff: a user map with those keys
  but no marker stays a plain dict.
- Two write paths (outer `Wire.Write` + list-element arm), three read paths.
- Depth-capped at `MaxReadDepth = 64` to prevent stack overflow on
  marker-bombed payloads.
- `name` key excluded from signing; `@schema:data` marker IS in the signed
  region — changing the marker string breaks all existing signatures.

### `Documentation/v0.2/good_to_know.md` — four new index entries

- `dict.@this` and `list.@this` → `type-system.md`
- `Compare` (IEquatableValue / IOrderableValue / ScalarComparer) → `type-system.md`
- List chunk/row model and `IListLeaf` → `type-system.md`
- `@schema:"data"` marker → `wire-serialization.md`

### `.bot/collections-are-data/claude-md-proposals.md` — filed

Three new CLAUDE.md bullets:
1. Extend "Truthiness" paragraph to note that the same pattern extends to
   `IEquatableValue`/`IOrderableValue`; `Compare` is the single mediator.
2. New "Equality and ordering" bullet covering `Compare`, `IEquatableValue`,
   `IOrderableValue`, `ScalarComparer`, the footgun.
3. New "Native collection types" bullet covering `dict.@this`/`list.@this`,
   end-to-end Data, `IListLeaf`, `CopyStructure`.

## Outstanding (non-blocking, carrying from auditor/security)

| Item | Status |
|---|---|
| Merge gate: hold behind `signature-as-schema-wrapper` (security + auditor) | carries |
| O1 dict-in-list aliasing via path-set (auditor) | documented as intentional + todo filed |
| O2 `HasSkipTag` regression tests (auditor, coder added follow-ups) | coder v7 addressed |
| Security F2 `text.Convert` default `JsonSerializerOptions` (low) | carrying |
| Security F3 `CopyStructure` no explicit depth guard (low) | carrying |

## Verdict

**PASS.** All gaps filled. Branch is ready to merge into `runtime2` (behind `signature-as-schema-wrapper` gate).
