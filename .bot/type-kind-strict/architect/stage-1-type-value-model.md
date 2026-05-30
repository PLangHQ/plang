# Stage 1: The `type` value model

**Goal:** Turn `app.data.type` from a flat string wrapper into a structured `{Name, Kind, Strict}` value, fold the separate `Data.Kind` field into it, and take `ClrType` off the public surface — without changing the wire shape.
**Scope:** Included — `app/data/this.cs` (`type` class + `Data.Kind`), the `Wire` serialiser, the `IKindValidatable` marker, and rerouting the ~6 `ClrType` call-sites. Excluded — the `text` type (stage 2), kind derivation/canonicalisation (stage 3), `variable.set` wiring (stage 4), LLM prompt (stage 5).
**Deliverables:** `type` with `Name`/`Kind`/`Strict` and a tolerant normalising factory; `Data.Kind` removed as a stored field (sourced from `Type.Kind`); `ClrType` internal to the registry; `IKindValidatable` marker; wire still emits flat `type`/`kind` keys.
**Dependencies:** None.

## Design

See [plan/type-value-model.md](plan/type-value-model.md) for the full narrative. Key points for the coder:

- `Name` replaces `Value` as the canonical family/primitive. `Kind` (subtype) and `Strict` (bool, default false) are new. The old `type.Kind` (family-from-formats) disappears — the family *is* the `Name`.
- `Data.Kind` is the single thing that moves: delete the stored field, let the `kind` wire key be sourced from `Type.Kind`. A thin delegating accessor is fine; a second stored copy is not.
- `ClrType` leaves the public `type`. Interior callers use `App.Types.Get(name)`/`.Clr(name)`. The two non-`types/`/`data/` call-sites — `file/read.cs` (`read.Type?.ClrType.Exit()`) and the builder's `IsClrTypeName` — reroute to a registry call or a small `type.IsExit`-style helper.
- The normalising factory accepts the structured form *and* a tolerant single-string slash form (`"text/markdown"` splits to `name=text, kind=markdown`). `string`→`text` canonicalisation lands here (the actual alias table is wired in stage 2; leave the hook).
- `IKindValidatable` is the seam strict uses in stage 4 — define it here so the type model is complete: `image` will implement it, `text` will not.

Do not change the serialised `.pr`/wire shape: two flat keys, no `type:kind` string. This stage is invisible on the wire; it's an internal collapse.
