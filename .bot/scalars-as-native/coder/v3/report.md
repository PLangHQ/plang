# Coder v3 — `where T : item` FLIP fully landed; both suites 100% green

Supersedes v2 (which closed at "7 separate-subsystem failures remain"). All of those are now
resolved. Ready for code-analyzer / security review.

## Final state

- **PlangConsole compiles with `where T : item` ON** — 0 errors.
- **C# suite: 4165 / 4165 green.**
- **PLang runtime suite: 272 / 272 green** (was 206/272 at the flip-merge baseline → +66, zero
  net regressions introduced).

HEAD: `41d066a0f` on `scalars-as-native`, pushed.

## Arc of the branch (v1/v2 → v3)

v1/v2 landed the constraint flip + the As<T> test redesign + the first wave of production gaps
(`variable.set` mint, `dict`/`list` `ToRaw` scalar-unwrap, context-free `convert.OfStatic`,
`datetime` returnWrapper + OwnedClr kind, `Data.Type.ClrType` raw-mate, schema `choice<T>` names,
error-precedence at the TypeMismatch leaf). v3 closed the remaining items, all per Ingi's calls:

### 1. `variable.set` `as <type>` reconstruction (`822d3ba44`, `6fec7e875`)
The bare-Data Type slot serves the `{name,kind?,strict?}` wire dict. The handler reconstructs it
via `TypeFromWire` for the dict shape, `FromName` for a bare name (keeping a kindless name's
ClrType the raw mate, not the wrapper). `TypeFromWire` now unwraps a born-native `@bool` for
`strict` (a wrapped `true` was reading false and dropping the flag).

### 2. `goal.getTypes` — value type, strong-or-item (`83ae7282a`, `3d0de1797`)
Ingi: "aim for strongly typed always if possible, else item." The build-time type snapshot read
the Type param as a string (born-native serializes a type entity → fell to `"object"`). Now it
reads the entity name and maps through the one format→type mapping: csv→`table`, txt→`text`,
json→`item`, and the legacy universal `object` folds to `item`. Then, per Ingi's `{object,json}`
→ `{item,json}` call, `TypeFromMime` itself folds `object`→`item` at the source; the json reader
was re-housed under `(item, json)` (delegates to the object reader) and `Wire.cs`'s
deferrable-shape / raw-verbatim json paths learned `item` so the **lazy invariant holds** (an
untouched `%cfg%` stays the raw json string; navigation still materializes). 11 C# `{object,json}`
/ `{object,xml}` expectations + the ReadConfigJson goal updated to `item`.

### 3. Navigation on a type-unknown value → `item`, never raises (`f6ab3fd9e`)
Per Ingi, navigating into an unknown-shape value is typed `item` and resolves to its value (no
"errors / ask-as-type"). Test rewritten off the old semantics.

### 4. `list.sort` returns an error, never throws (`7c0309f73`)
Ingi: "in PLang we always return error; exceptions are the unexpected." Sorting a list-of-dict
(unorderable) now returns `Data.FromError` instead of throwing `NotOrderableException` (a throw
escapes the `on error` pipeline). C# test `SortOnListOfDict_Throws` → `…_ReturnsError`.

### 5. `DictIsItemKeepsNoOrder` / the whole on-error-rebuild surface (`9004c21c4`, `41d066a0f`)
The deepest one. A freshly-built `error.handle.Actions` (`Data<step.actions>`) reconstructed to a
recovery chain whose **nested params came back null** → `goal.call` NRE'd on a null `GoalName`.
Root: the born-native wire marks records with `@schema:data`, but `As<step.actions>`'s
`ToRaw→JSON` round-trip strips the marker the Data re-read needs. Fix, OBP-clean (the catalog
stays arm-free per its own contract — no `GetListElementType`/`MakeListSink` central type-switch):
- `step.actions.@this.Convert` (a catalog hook) owns reconstructing the chain;
- it delegates each row to **`action.@this.FromWire(value, context)`** — a named factory on the
  owner (read-side mirror of `AsData`), rebuilding field-by-field via `FromWireShape` (reads
  `{name,value,type}` slots directly, no marker needed). Kept a *named factory*, not a global
  Convert hook, so it doesn't hijack the generic `list<action>` element path.

The first cut put the rebuild in a `BuildAction` verb+noun helper on the collection; per Ingi's OBP
note and `obp-smells.md` ("verb+noun method names are a smell; does this behavior belong on the
owner?") it moved onto `action.@this` as `FromWire`.

## Files of note (for the reviewer)
- `PLang/app/module/variable/set.cs` — `as <type>` reconstruction (TypeFromWire/FromName split).
- `PLang/app/data/this.cs` — `TypeFromWire` (`@bool`/strict unwrap), `WireSlot`/`FromWireShape` (now `internal`), `Type.ClrType` scalar-leaf unwrap.
- `PLang/app/data/Wire.cs` — `item` added to deferrable-shape + raw-verbatim json (lazy invariant).
- `PLang/app/format/list/this.cs` — `TypeFromMime` `object`→`item` fold.
- `PLang/app/type/item/serializer/json.cs` — `(item, json)` reader (delegates to object).
- `PLang/app/module/goal/getTypes.cs` — value-type snapshot (`TypeNameOf` query, `ToValueType` mapping).
- `PLang/app/type/convert/this.cs` — `OfStatic` (context-free family Convert).
- `PLang/app/type/catalog/Conversion.cs` — error value stays primary at the TypeMismatch leaf.
- `PLang/app/module/list/sort.cs` — returns error, doesn't throw.
- `PLang/app/goal/steps/step/actions/this.Convert.cs` + `action/this.FromWire.cs` — on-error chain reconstruction.

## Known-but-deliberate / for the reviewer's awareness
- **Guid / byte[] via the Text serializer**: `guid` has no `: item` wrapper, so a guid rides the
  text channel as text; `byte[]` is the `binary` type (its text form is base64). Tests reflect
  this born-native reality (noted in `TextStreamSerializerTests`).
- **`dict<K,V>` is NOT generic yet** — only `list<T>` is. Tracked as the next enhancement (memory
  `project_generic_list_t` / discussion with Ingi); non-generic `dict` is `: item` so the
  constraint is satisfied today.
- Pre-existing OBP backlog item `Conversion.GetListElementType` (verb+noun) is untouched here —
  it's on the OBP-cleanup doc, not introduced this branch.

— coder
