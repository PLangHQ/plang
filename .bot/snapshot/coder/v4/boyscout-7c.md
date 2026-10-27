# Stage 7c: boy-scout list (snapshot 110). List only, no code changed

Patterns: the boy scout rule; smells by catalog name (`Documentation/v0.2/obp-smells.md`).

Scope: every production file 7c touched on `snapshot-7c-wip` (41, base `ca3dcf4d8`).
- Deleted files are skipped.
- So are the four snapshot readers, which are new on this branch.
- Every row is a line that already existed at the base (`git blame` at HEAD shows `^ca3dcf4d8`). The commit is the one that wrote it.
- Lines 7c wrote or fixed are left out. **(locked)** marks a fix that touches a locked surface, which would need a proposal to Ingi.

## call, goal, module/debug

| smell | file:line | commit | smallest fix |
|---|---|---|---|
| flat copy | call/this.cs:119, 216 `_datetime = stack.Datetime` | 6e8f35859 | read `_stack.Datetime`; drop `_datetime` |
| creative name | call/this.cs:160 `_for`, :207 `callee` (an action) | 1dc8a4606 / 56deaa5eb | `_action` / `action` |
| creative name | call/this.cs:35 `_diffSource` (a store) | 4a1f1c1db | `_store` |
| verb+noun | call/this.cs:335 `GetItem<T>`, :345 `SetItem<T>` | 01cfc14f9 | `Item<T>()` / `Item<T>(value)` |
| late stamp | call/this.cs:398–399 `error.Step ??=`, `error.Context ??=` | a1ca19869 | the error is stamped at one door on itself, born with them (`error.Record(frame, context)`) |
| stray helper (+ fork) | call/this.cs:448 `CaptureBefore`, :466 `IsScalar`, :480 `SummaryString` | 01cfc14f9 | capture moves onto `Diff` (its `Before`'s owner); scalar is `item.IsLeaf` |
| flat copy (+ creative name) | call/list/this.cs:57–77 six lazy `_types[typeof(X)]` copies; `Errors` names a type | 7b81db4b3, 6c6f78414 | read `_types[typeof(X)]` where used; delete the fields |
| middleman, no runtime code for tests | call/list/this.cs:96, 97, 102, 103 `Timing()`, `Diff()`, `Tags()`, `History()` | ee0b92be6 | delete; tests read `(await call.Setting()).X` |
| no runtime code for tests | call/list/this.cs:120 `Variables { get; internal set; }` (set only by EventsSinceTests) | fb460e53a | remove; tests pass the store to `Push` |
| creative name | call/list/this.cs:200, 207 `held` (an action) | ee0b92be6 | `action` |
| verb+noun | call/list/this.cs:274 `RestoreCurrent` | 4a1f1c1db | `Leave(frame, previous)` |
| stored twice | call/list/this.Snapshot.cs:16–18 `List<Diff> _streamDiffs` + `_streamLock` beside `call.diff.@this` | b3fef71f2 | a `diff.@this`; drop the lock |
| verb+noun | call/list/this.Snapshot.cs:28 `EnableDiffStream`, :92 `DisableDiffStream` | 6f44b6054 / 401cd080c | inline into `DiffScope` / `Diffing.Dispose` |
| stray helper | call/list/this.Snapshot.cs:246 `DiffsOf`, :235 `WalkTree` | 4a1f1c1db / ad15075ce | `diff.@this.Since(t)`; the walk on the frame |
| creative name | call/list/this.Snapshot.cs:215 `EventsSince` (answers diffs) | b3fef71f2 | `Since(t)` |
| fork, no runtime code for tests | goal/this.cs:258 `ToText()` beside :101 `ToString()` (they disagree on indent; only GoalTests calls it) | d7ba146bd | delete `ToText`; tests use the one writer |
| stored twice | goal/this.cs:214 `FolderPath` = `Folder` (no caller) | 6c08c44ed | delete |
| glued name | goal/this.cs:228 `FullPath` (overlaps `Address`; only itself calls it) | d7ba146bd | delete, or fold into `Address` (it is `[Debug, Default]`, so check the dump) |
| verb+noun | goal/this.cs:497 `ForEachAction` (no caller) | 50f6711de | delete |
| glued name | goal/this.cs:177 `BuilderVersion` | d7a13c8fa | `builder.version` (a .pr key: needs a reader change and a rebuild) |
| creative name | goal/this.cs:117 `Elsewhere` (a parent app) | c8823eb66 | `App` |
| clr leak | goal/this.Item.cs:95 `name.Clr<string>()` in Select (296f15b83 restored the pre-91 text) | pre-91 Select | `Find` takes the text whole |
| fork | module/debug/this.cs:91 `Provider is OpenAi` | 13497c68c | request/response hooks on `ILlm`; drop the cast |
| fork | module/debug/this.cs:432–437 `value switch { IDictionary, IList }` beside the value's own Debug write | 518af3fd4 | delete; the value's write states its count |
| one definition of a reference (CLAUDE.md) | module/debug/this.cs:23 `.Trim('%')` | ee0b92be6 | the parser (`:393` already does) |
| creative name | module/debug/this.cs:124 `step` (an index), :127–128/147 plural `steps`/`goals`/`actions` (each one type), :72 `hold` (the `on` list) | ee0b92be6, 07f35ed1e, 47b8940da | `index`; the type's name; `on` |
| glued name | module/debug/this.cs:27 `_grepRegex` (and a `[JsonIgnore]` on a private field, which does nothing) | b3fef71f2 | `_grep`; drop the attribute |

No older finding in: call/Diff.cs, call/{action,binding,composed,goal,isolated,step}/this.cs, test/this.cs.
- `call.Children`, `call.Errors` and `test.Tags`/`Timings` are left out: the catalog lists them as known drift, not precedent.

## type core

| smell | file:line | commit | smallest fix |
|---|---|---|---|
| stray helper, glued name | type/this.cs:114 `KindOfClass` (private static, a class fact) | 925787b466 | into the per-class facts record (`list.@this.Class`) |
| raw hand-off | type/this.cs:300, 619, 623, 652 kind-name compares / `ToLowerInvariant` | f04e40e714, a56192acae, 6e932de352 | the kind answers its own sameness (`kind.Names(...)`) |
| fork | type/this.cs:660 `Is(string)` beside :648 `Is(@this)` (it ignores kind; its doc says it resolves) | d0c39f5bbc | resolve, then `Is(type)`, as its doc says |
| fork | type/this.cs:500 `Construct`/:582 `CreatableFamily` beside :481 `Make`/:569 `Creatable` | 7188c50e90, 0812b7bbfb | one `Creatable` (the most-derived family) and one thunk **(locked)** |
| glued name | type/this.cs:692–697 `Own*` ×6, :107 `BornKind`, :732 `ConstructorSignature` | 0faa3ca467, 925787b466, dd899b865e | an `own` node; dot paths **(locked)** |
| verb+noun | item/this.cs:288 `EnumerateItems` (+ clr:149), `Rows` (:346) beside it | 1f840645d0 | `Items`; check whether `Rows` folds in **(locked)** |
| fork | item/this.cs:204, 224 `this is property.IOwner` in base Set/Get | 2a019554a5 | a virtual on item that owners override **(locked)** |
| stored twice, stray helper | item/this.cs:521–561 static `_identities`, `IdentityOf`/`NameOf`/`NamespaceOf`/`FamilyOf` beside `list.@this.Class` | 14f29769af, 39345235db | fold `Identity` into the per-class record |
| glued name | item/this.cs:762 `RawText`, :770 `RawBytes` | a3c04874a4, 3f6353bea5 | `raw.text` / `raw.bytes` **(locked)** |
| raw hand-off | source.cs:112 `Type.Name.ToLowerInvariant() is "text"` | fb0c7f7787 | `Type.Names("text")` |
| raw hand-off | `type.kind.IsEmpty ? null : type.kind.Name` at ~10 sites (source.cs:310, View.cs:134, wire/kind/plang:126, property:129, list/this.cs:159/168, table:43, image:151, …) | fb0c7f7787 and others | the kind answers its nullable name once **(locked)** |
| fork | source.cs:238–239 `Type.Name is "dict" or "list"` round-trip guard | c7c7e98162, e267f69760 | the type and the value answer "container" (virtuals) **(locked)** |
| flat copy | clr/this.cs:39 `Kind` + :59 `Type` built from `kind.Name` | 0687f97b78, a556c1e60f | born with the type whose kind it is; route through it |
| fork | clr/this.cs:138 `Enumerate` beside :149 `EnumerateItems` (one caller, OpenAi.cs:854) | 1e8f9221b7 | remove `Enumerate` |
| raw hand-off | property/this.cs:102–106 Task-result unwrap by reflection; the same at clr/this.cs:98–101 and variable/code/Method.cs:94 | 963b65b5ae, 031f6fb563 | one unwrap on `property.@this`; the others go through it |
| fork | reader/this.cs:131–141 `"binary"` narrowing inside the registry's `Reader(...)` | aa7af5fb91 | binary (its kind) answers the type its kind names |
| fork | reader/this.cs:86–93 `Of` (payload) beside `Typed` (token), a migration fallback by its own doc | 7e84623352 | finish the conversion (bigger than a rename; obp-cleanup.md) |
| glued name | reader/this.cs:156 `TypeOf` (proxies `Owner`) | 5233005009 | `Owner` **(locked)** |

No older finding in: type/reader/layer.cs, type/reader/ITypeReader.cs.

## view and serialization

| smell | file:line | commit | smallest fix |
|---|---|---|---|
| creative name | View.cs:45, 167, 249 a `data.@this` named `record` | b0166864c4, d60c08ceb6 | `data` |
| glued name | View.cs:266 `MaxDepth`; json.cs:17 `MaxDepth` | 862ae884d2, c41048eb4b | `Max` / `Depth` |
| stray helper | reflection/Tagged.cs:11, 25 static `Tagged.PropertiesFor(type, view)` (the view's answer, cached outside it) | be13786f59, 8036e244a5 | the cache and lookup on `View` **(locked)** |
| creative name | Tagged.cs:18, 25 a View named `mode` | 8036e244a5 | `view` |
| glued name | Tagged.cs:16 `Entry.WireName` | 432239a575 | `Name` |
| fork | reflection/this.cs:108 token read onto a host beside :167/:176 slots read | de7d9729ee, 6787226da3, 86d82048c0 | the slots read through the token read (`format.item.Reader` over the dict) |
| fork | reflection/this.cs:226, 232, 252 `ReadValue` type-switch (path, choice) + generic fallback | 6c08c44ed5, fecaf5bdb2, 71cd6d2686 | any item type is read by its type's reader, as View.Make does |
| verb+noun, glued name | reflection/this.cs:209 `ReadValue`, :256 `ReadDataList`, :282 `ElementTypeOf` | de7d9729ee, ad70ab7b74 | `Read` overloads; `Element` |
| fork | reflection/this.cs:16 static `Fallback` reflection kind outside any app (used by dict/this.cs:361) | 925787b466 | `dict.Clr` through the app's reflection kind; delete `Fallback` |
| creative name | property/list/this.cs:92 `written`, :94 `made`, :111 `data` (a data reader) | 2a019554a5, 955b852dc0 | `dict`, `list`, `reader` |
| verb+noun, glued name | json.cs:107 `ParseRaw`, :57 `RawEntry`, :67 `StringSlot`, :182 `RawSlot`, :209 `TextLeaf` | a5348131b4, 71cd6d2686, 990faa11ee | `Parse` / `Entry` / `Slot` overloads |
| fork | json.cs:67–71 beside :209–220 (two paths to a template text) | 990faa11ee, 8c52c469c0 | `TextLeaf` calls `StringSlot` |
| fork | json.cs:101–102 `Typed` beside :195–198 `RawSlot` (one "marked or typed entry is a Data" test, twice) | 87b66b5285, 71c21e3024 | one shared method over the element |
| creative name | wire/this.cs:28 `_reader` (holds a kind) | 9c76c8cb49 | `_kind` |
| raw hand-off | `types["wire"].kind["plang"]!` + cast at 9 sites (wire/this.cs:52, wire/kind/plang/writer.cs:22, snapshot/serializer/Reader.cs:43, …) | 327ea4ba35, 6ff57ceb4d, 4611e3ef4f | one typed door on the wire type for its plang kind |
| fork | wire/this.cs:77 `Decoded` (kind.Parse, else the type reader) | aa7af5fb91 | one decode door, `Read(context)` |
| opened box | wire/kind/plang/this.cs:44 `StoreView = view == View.Store` to sign, rebuilt as a View at Ed25519.cs:36 | 9c76c8cb49 | `sign` takes the view whole |
| fork | wire/kind/plang/this.cs:82, 100, 105 `view == View.Store/Out` branches (window, unsigned refusal, name) | ee0b92be67, a666595e34, 819ff02787 | the view's own answers (virtuals on View) **(locked)** |
| broken seal | wire/kind/plang/writer.cs:67–70 `kvp.Value is data.@this` and wrapping in a courier | d5f3f500eb, cde50a338c | `Properties` holds Data from birth; the writer calls `Output` |
| creative name | snapshot/serializer/Reader.cs:48 `parser` (a serializer.json) | e5865c9855 | `json` |
| raw hand-off | the `CreateItemDeclined` message `(raw as item)?.Type.Name ?? raw?.GetType().Name` at 12 Create sites (snapshot/this.Wire.cs:35, property/list/this.cs:100, …) | 4611e3ef4f | one owner builds the decline (the type's Create door) |

No older finding in: format/item/reader.cs (the `is source` exception is on the backlog), wire/template.cs.

## Checked and left out

- property.list `Copy()` beside `Clone()`: `Copy` has a production caller (data/attachment.cs:41), and the catalog has no smell for twins.
- debug `Write`'s `Get(Debug)` → `Get(Error)` fallback: the debug channel is not a guaranteed default, so a miss is real.

## How sure

- **Most defensible:** the zero-caller deletions (`FolderPath`, `ForEachAction`, `ToText`, the stack's `Timing()`/`Diff()`/`Tags()`/`History()`, `Variables`); the late stamp; the flat copies; the creative names; the reader-registry and `IOwner` forks.
- **Design calls, not quick fixes, because each is documented as deliberate:** `Construct`/`Make`, `Of`/`Typed`, the reflection kind's two reads, and `Tagged` moving onto View.

Method: three reviewers read the files whole, one area each. I re-blamed a sample from each area at HEAD (18 lines) and checked the zero-caller claims by grep.
