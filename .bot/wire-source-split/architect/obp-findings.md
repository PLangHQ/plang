# OBP scan — findings over every file this branch touches

Companion to [`plan.md`](plan.md). Full-file scan (14 files, ~3,250 lines), smell catalog applied member-by-member (`Documentation/v0.2/obp-smells.md` names). Three buckets: items folded into this branch's scope (Ingi approved), and pre-existing debt where **the coder has judgment** — fix/rename on this branch if cheap while already touching the file, or leave recorded here for a later pass. Deleting is always preferred over renaming when the member shouldn't exist at all.

## In scope for this branch (Ingi-approved additions)

1. **`type.@this.Convert(string)`'s json arm dies** (`type/this.cs:462-472`) — *fork* (a `Name == "json"` switch on the generic entity, RawFormat's sibling) + *stored twice*: `object/serializer/json.cs`'s doc claims it re-housed this exact decode, yet the inline branch remains; after this branch converts `object/json` to a kind reader the same decode would exist in THREE homes. The kind reader is the one home; the `Convert` json arm deletes with this branch. (The rest of `Convert` — the `FromWire` convention + `TryConvert` tail — stays.)
2. **Receive-door naming collision resolved** — `channel/type/file/this.cs:55` already has a public `Read(byte[], ct)` delegating to `StampReadAsync`. When §9's base door lands as `Read(byte[], …)`, the file channel's overload becomes a public face delegating to the base door (or deletes if nothing external calls it — coder checks callers).
3. **`Text._jsonFallback` dead code deletes** (`Text.cs:21, 27-31`) — field assigned, never read (verified), plus the stale "Falls back to JSON for complex types" class doc. We're in this file anyway for the `Text.Read` orphan check.

## Confirmed dying with the branch (for completeness — plan already covers)

`type.@this.RawFormat` (fork, Name-switch) · `channel.StampReadAsync` (verb+noun + serializer type-switch) · `StampValue`/`StampType` (verb+noun middlemen, one caller each) · `serializer.list.ResolveForWrite` (verb+noun + three-arm fork + `data.Peek()` shape-sniff) · `source._format` (stored twice against the declaration) · the `?? Serializers.Text` coercions (file + channel).

## Pre-existing debt — coder's judgment (fix if cheap while touching, else leave recorded)

### `type/this.cs`
- ctor number-precision collapse (`:129-140`) — name-list *fork* (`lower is "int" or "integer" or "long" …`) duplicating alias-table knowledge; the precision-token→kind fold should have one home.
- `Context { get; set; }` (internal, mutable) + stamp sites like `t.Context = context` — *late stamp*, documented as deliberate; context-never-null-plan territory, NOT this branch.
- `StampPrimitive` — verb+noun.

### `channel/this.cs`
- `Metadata` as public `IDictionary<string, object>` — *naked collection*; self-described "compatibility with v1 Channel surface".
- `ResolveEncoding()` (`:326-332`) — verb+noun AND stringly config re-parsed per call; the `Encoding` string should resolve to the `System.Text.Encoding` object once at construction (same crossing-rule argument as mime).
- `InvokeChannelHandler` — verb+noun private.

### `channel/type/stream/this.cs`
- `IsLineDelimited(mime)` (`:77-79`) — mime-prefix *fork* deciding framing inside the channel; "do I frame by line" reads like the serializer's own fact.
- v1 convenience quartet `ReadAllBytesAsync` / `WriteBytesAsync` / `ReadAllTextAsync` / `WriteTextAsync` (`:161-193`) — verb+noun, header admits "kept for v1 callers".

### `type/item/path/file/this.Operations.cs`
- Verb+noun cluster: `EnsureParentDir`, `PerformTransfer`, `StoreGrant`, `BuildRequest`, `CopyDirectory`, `TryAuthorizeWithoutAsk`.
- `ResolveDestinationPath(source, destination)` (`:322-327`) — *stray helper*: a path question answered beside the path type.
- `Save`'s `raw is binary / raw is text` arms (`:218-229`) — *fork* on value type at the leaf; same through-line as §10 ("the value writes itself") and the natural NEXT cut after this branch — §10 only touches Save's else-arm.
- `BundledTransfer` (`:353-408`) — inline y/n/a prompt loop + grant storage + transfer in one method; *split lifecycle*-adjacent.

### `channel/serializer/list/this.cs`
- Five selection doors for one lookup: `GetByMimeType` (throws) / `this[mime]` (throws) / `GetByType` (null) / `GetOrDefault` (default) / `GetByExtension` — Get-family verb+noun proliferation; a naming + door-count pass candidate (the one-verb rule: `cache.Get`, never the mechanism).

### `data/reader/this.cs`
- `ReadPropertyPrimitive` — verb+noun (the eager raw-CLR properties parse itself is documented design; the name isn't).

### `channel/serializer/plang/this.cs`
- `BuildOptions` — verb+noun private (4 call sites).

### Clean
`json/reader.cs` · `object/serializer/json.cs` · `type/reader/this.cs` (light: `SafeGetTypes`) · `channel/type/http/this.cs` · `channel/type/file/this.cs` (beyond item 2 above).
