# Survives-the-model audit — pre-model tail work (C# 306→23 path)

Stage 9 dependency note: "mark which of those fixes survive the model before
redoing the core, so the 280 already-fixed sites don't get re-litigated."
Audited commit-by-commit against `data-value-model.md` + the demolition
worklist. Verdicts:

## SURVIVES — keep, the rebuild rides on it

- **`item.Clr(Type)` / `Clr<T>` (internal) + `ClrConvert` + number tower
  loss-policy** (b26176949) — the sanctioned lowering; demolition "Stays" list
  names it explicitly.
- **Per-type `Convert` hooks + catalog Conversion dispatch** (00e2c1afa
  Conversion.cs work) — `Value<T>()`'s mechanics ride on them ("Stays" list).
- **Truthiness door**: steps/if/elseif verdicts via `IsTruthy`/
  `AsBooleanAsync`, fixture modernized to IBooleanResolvable (b26176949,
  c9dc...) — "Stays" list.
- **text-leaf navigation gate** (TypeUnknown teaching error, 00e2c1afa) —
  exactly the model's "cannot navigate text" honest error.
- **tstring validator accepts text wrapper** (b09cc73cf) — entry-side
  acceptance of typed instances; consistent with born-typed.
- **Test-contract fixes / +193 asserts migrated to string-face comparison**
  (ea2cdb8de, b89f53ddb, 8e8a3f0c0) — asserting through a value's rendered
  face is the right direction for tests; individual asserts only get
  re-touched if slice 1/2 changes the face they read.
- **bool canonical lowercase pins, numeric pins via BoxedValue/Clr<T>**
  (05461be3c, b09cc73cf) — pin outcomes, not mechanics.
- **list/dict public surface takes number; Round number end-to-end; typed
  handler reads** (2f11d18ea) — plang→plang keeps plang types; model-aligned.

## DIES with slice 1 (was scaffolding on the old shape — expected, not re-litigated)

- All `As<T>` / `AsT_Impl` / `As(kind)` behavior changes (ea2cdb8de,
  c9c...): `As<T>` is removed wholesale; each site re-judges to `Value<T>()`
  or the type's own lowering.
- Text serializer routing through `As<T>` (c9dc...) — Write becomes the
  type streaming itself (slice 3 completes this).
- Any site leaning on `Materialize()` / `NarrowReference()` semantics.

## DIES with slice 2

- **`Data.Clr<T>()` / `Clr<T>(fallback)`** (b26176949) and its call-site sweep
  (http/run/sleep/event/channel/signing/llm) — the courier-level exit door is
  a slice-2 kill; each site re-routes to `(await x.Value())` + the item's own
  `Clr<T>`. The *item-level* door it built stays (above).
- `getTypes via Peek source-forms` — re-judge when Peek's contract tightens.

## TRANSITIONAL — committed, marked, do not extend

- Courier constructions via `SetValueDirect` bypass (ea2cdb8de, b89f53ddb):
  wire reader / UnwrapJsonArray / Normalize-dict / http-callback / compress
  Wrap. Schema branch deletes them. No new callers.

## The 5-then-2 known fails

v6 reported ~5 fails on the disposable path; current baseline shows 2
(`Data_PropertyAccess_UsesDeclaredTypeForMaterialization`,
`TypedSnapshotString_NavigateEditResume_PersistsEdit`). Both sit on
Materialize-era semantics — expected to be fixed or rewritten by slices 1–2,
not patched beforehand.
