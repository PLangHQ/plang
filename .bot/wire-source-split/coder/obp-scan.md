# Coder OBP scan — files `wire-source-split` will touch — for architect

Branch: `wire-source-split`. Read every file the plan edits, in full, against HEAD
(`92e3183e6`). Smells cited by name (`obp-smells.md`). Grouped by what the finding means
for the plan.

Files scanned in full: `type/item/source.cs`, `type/this.cs`, `data/reader/this.cs`,
`data/Wire.cs`, `channel/serializer/plang/this.cs`, `channel/serializer/value/reader.cs`,
`channel/serializer/json/reader.cs`, `channel/this.cs`,
`channel/serializer/list/this.cs`, `type/item/path/file/this.Operations.cs`,
`type/object/serializer/json.cs`, `type/reader/this.cs`, `channel/serializer/Text.cs`.

---

## Findings that change the plan

### A. `ISerializer.Read` is misplaced — only the transport serializer ever answers it
*Smell: fork / misplaced member.*

Grep proof: `source.cs:182` is the **only** caller of the serializer-dispatch
`Read(source, ctx)` today. After the branch, a content source reads via `value.Reader`
(§1's new `source.Read()`, which is literally `Text.Read`'s body moved onto `source`), and
a wire reads via `_reader.Read(this, …)` where `_reader` is **always** the transport/plang
serializer (a `.pr` slot is always captured by transport). So `Text.Read` and `Json.Read`
become genuinely call-orphaned — but they **cannot be deleted**, because `Read` is an
`ISerializer` member and `Text`/`Json : ISerializer`.

The plan's stays-list keeps `ISerializer.Read` ("the wire kind's door, reached by
reference") AND the demolition list says "`Text.Read` orphan — delete when confirmed."
Those two are in tension: while `Read` is on the interface, `Text` must implement it.

→ Clean fix: `Read` is a wire-decode door, not every serializer's job. Narrow it — move
onto `plang.@this` (the transport), or a small `IWireReader` the transport implements.
Then `Text.Read`/`Json.Read` genuinely delete and the "orphan" line is real. This is the
honest version of the plan's Text.Read note.

### B. Asymmetric demolition — the read-side selection twins survive
*Smell: verb+noun + fork + option-bag.*

The plan kills the write side: `list/this.cs` `ResolveForWrite` +
`SerializeAsync(SerializeOptions)` + the `SerializeOptions` carrier (§10). Their exact
mirrors on the **read** side are untouched and live:

- `list/this.cs:182` `ResolveSerializer(ResolveOptions)` — the same three-arm selection.
- `list/this.cs:173` `DeserializeAsync<T>(DeserializeOptions)` + the `DeserializeOptions` /
  `ResolveOptions` carriers.
- One live caller: `channel/list/this.cs:205`.

If write-selection moves to the owners (stream owns its Mime, file its Extension), the read
side carries the identical smell. Either it follows out, or it's a deliberate deferral.

→ Scope question for architect.

### C. `Save`'s three-arm write fork
*Smell: fork (broken-seal-adjacent).*

`file/this.Operations.cs:218-229` — `raw is binary.@this / raw is text.@this /
else→serializer`, branching on the materialized value's CLR type to decide how to persist.
Plan §10 rewrites only the `else` arm (`GetByExtension(Extension) ?? Text`, "the value
writes itself as content"). But `Text.SerializeAsync` already does `data.Output` → "a leaf
renders bare; a container renders via its format text serializer" (`Text.cs:38-42`) — which
is exactly what the `binary`/`text` arms hand-roll. So the whole fork should **collapse**
into the one `Text` path, not just the `else` arm.

→ Confirm the collapse is intended; if the `binary`/`text` arms survive, the fork does too.

---

## Confirmations — the plan kills these correctly

- **`channel` StampReadAsync / StampValue / StampType** — *verb+noun ×3* + *fork*
  (`GetByType(Mime) is plang.@this` type-switch, `channel/this.cs:278`) + *late stamp*
  (`t.Context = context`, `:316`). All die in step 9. ✓
- **`ResolveForWrite`** — *verb+noun* + *fork* (`data.Peek() is string` shape-sniff,
  `list/this.cs:166`). Dies step 10. ✓
- **`object/serializer/json.cs`** — static `json.Read` is a *stray helper* (Of-mode static)
  with a *passthrough fork* (`raw is not (string or byte[]) → return raw`, `:26`); both
  dissolve when it becomes an `ITypeReader` with `Kind => "json"` (issue 1). ✓

---

## Note — conscious deferral, not this branch

- **`type/reader` registry is a 4-dictionary *fork*.** `_generated`/`_runtime` (Of-mode
  delegates) beside `_generatedTyped`/`_runtimeTyped` (ITypeReader pull). Issue 2
  (`TypeOf` also scanning the typed tables, `:133`) patches the seam by **widening a
  naked-dictionary scan** rather than closing the fork. Acceptable for the branch; the real
  fix is Of→Typed everywhere (the plan's issue-1 conversion is one step of it). Flag so the
  widening is a conscious deferral, not drift.
- **`file` ReadText `type.Context = Context` (`:67`)** reads as *late stamp*, but the type
  entity's `Context` is **deliberately** nullable-settable (documented, `type/this.cs:103-110`;
  `app.Type` stamps it on registry resolution). Accepted shape, not a fresh violation.
  However, once §5 passes Context into `type.Create(snapshot, Context)`, line 67's stamp is
  likely **vestigial** — a cleanup once the rewrite lands.

---

## Pre-existing, out of the change (present in touched files, far from our edits)

Recorded for completeness; NOT proposed for this branch:

- `file/this.Operations.cs`: `ResolveDestinationPath` (*stray helper* + verb+noun),
  `CopyDirectory`, `EnsureParentDir`, `PerformTransfer`, `StoreGrant`,
  `TryAuthorizeWithoutAsk` (verb+noun statics/helpers on the Move/Copy machinery).
- `channel/this.cs`: `ResolveEncoding`, `MatchingBindings`, `InvokeChannelHandler`.
- `Text.cs`: `_jsonFallback` (*fork* — "falls back to JSON for complex types").
- `type/this.cs`: `StampPrimitive` (verb+noun, private); `type.list.BuildTypeEntries`
  (referenced at `:23`) is the known verb+noun example in `obp-smells.md`.

---

## Bottom line

Two worth acting on before implementation:

1. **A** — narrow `ISerializer.Read` onto the transport so `Text.Read`/`Json.Read` can
   actually die (resolves the stays-vs-demolition tension in the plan).
2. **B + C** — decide whether the read-side selection twins (`ResolveSerializer` &co) and
   the `Save` binary/text fork are in scope, or explicitly deferred.

Everything else the plan already handles correctly or is documented pre-existing shape.

— coder
