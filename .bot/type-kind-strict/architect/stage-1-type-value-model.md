# Stage 1: The `type` value model

**Goal:** Give the promoted type entity (`app.type.@this`) the `{Name, Kind, Strict}` identity, fold the separate `Data.Kind` field into it, drop the family-`Kind` accessor, and take `ClrType` off the public surface — without changing the wire shape.
**Scope:** Included — `PLang/app/type/this.cs` (the entity), `Data.Kind` on `PLang/app/data/this.cs`, the `Wire` serialiser (`PLang/app/data/Wire.cs`), the new `IKindValidatable` marker, the 3 public `ClrType` call-sites, and the `App.Type.Kinds` dispatcher rename. Excluded — the `text` type (stage 2), kind derivation/canonicalisation (stage 3), `variable.set` wiring (stage 4), LLM prompt (stage 5).
**Deliverables:** `app.type.@this` with `Name`/`Kind`/`Strict` and a tolerant normalising factory; `Data.Kind` removed as a stored field (sourced from `Type.Kind`); family-`Kind` accessor removed; `ClrType` internal to `app.type.list.@this`; `IKindValidatable` marker in `app/data/`; dispatcher renamed `App.Type.Kinds`→`App.Type.KindHooks`; wire still emits flat `type`/`kind` keys.
**Dependencies:** None.

## Design

See [plan/type-value-model.md](plan/type-value-model.md) for the full narrative. Key points for the coder:

- The entity already exists and carries the folded catalog (`Fields`/`Values`/`Kinds`/`Shape`/`Example`/… via lazy `Promote()`). Leave the catalog props alone; this stage adds the **identity** fields and resolves the kind knot.
- `Name` replaces `Value` as the canonical family/primitive (keep `[JsonPropertyName("name")]` so the wire key is unchanged). `Kind` (subtype) and `Strict` (bool, default false) are new.
- **Three "kind"s to disentangle:** `type.Kind` (family, via `App.Format.KindOf`) → **remove**, the family is the `Name`; `type.Kinds` (advertised vocabulary) → **keep**; `Data.Kind` (subtype) → **fold into** the entity's new `Kind`. And rename the build-hook dispatcher `App.Type.Kinds` (`app.type.kind.@this`) → `App.Type.KindHooks` so the words stop colliding.
- `Data.Kind` is deleted as a stored field; let the `kind` wire key source from `Type.Kind`. A thin delegating accessor is fine; a second stored copy is not. `Data.Type` is non-null now (the `type.@this.Null` sentinel), so the fold has no null-Type edge.
- `ClrType` leaves the public entity (it's already `[JsonIgnore]`; make it non-public). Interior callers use `App.Type.Get`/`.Clr`. The three non-`type/`/`data/` sites — `module/file/read.cs`, `module/variable/set.cs`, `module/settings/Sqlite.cs` — reroute to a registry call or a small `type.IsExit`-style helper.
- The normalising factory accepts the structured form *and* a tolerant single-string slash form (`"text/markdown"` → `name=text, kind=markdown`; multi-slash splits on the first). `string`→`text` canonicalisation lands here (the alias table is wired in stage 2; leave the hook). Don't disturb the `_foldLoaded`/`Context` invariant `Promote()` relies on.
- `IKindValidatable` is the seam strict uses in stage 4 — define it here (sibling to `IBooleanResolvable` in `app/data/`) so the model is complete: `image` will implement it, `text` will not.

Do not change the serialised `.pr`/wire shape: two flat keys, no `type:kind` string, no `"type":"null"`. This stage is invisible on the wire; it's an internal collapse.
