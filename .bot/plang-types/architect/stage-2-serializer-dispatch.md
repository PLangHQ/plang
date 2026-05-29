# Stage 2: Serializer dispatch — per-(type, format) files

**Goal:** Stand up the `(typeName, formatToken) → Write` dispatch so a type renders itself per output format via small files beside it, with `path` as the first mover proving the spine end-to-end.
**Scope.** The serialization half of the spine. *Included:* `TypeSerializers` table (generated + runtime-registerable), `TypedValueNode` marker, the `Normalize` tag-hook, `IWriter.Format`, the writer's `TypedValueNode` case, the `PLNG_SerializerCoverage` build gate, and `path/serializer/Default.cs` + `path.Build` as the proving instance (absorbing `this.JsonConverter.cs`). *Excluded:* `number`/`image`/`code` serializers (their stages), runtime-loaded renderers (Stage 7 — but leave the registration seam).
**Deliverables:**
- `app/types/TypeSerializers.cs` — the dispatch table: `(typeName, formatToken) → static void Write(object value, IWriter writer)`. Generator-populated; exposes a `RegisterRuntime(typeName, formatToken, delegate)` seam for Stage 7. Lookup: specific `(type, format)` ?? `(type, "*")`.
- `app/data/TypedValueNode.cs` — `sealed record TypedValueNode(object Value, string TypeName)`.
- `app/data/this.Normalize.cs` — the tag branch: a value whose CLR type resolves to a `[PlangType]` *and* has ≥1 registered serializer is wrapped as `TypedValueNode` instead of reflected. Unregistered domain objects reflect exactly as today.
- `app/channels/serializers/IWriter.cs` — a `string Format { get; }` property (short token: `"json"`/`"plang"`/`"text"`/…). Implemented on each writer; the serializer registry's mime maps to the token.
- The writer's `Value(object?)` dispatch (`json/writer.cs:113`, plang, Text) — a `case TypedValueNode tv:` that looks up `TypeSerializers.Get(tv.TypeName, Format)` ?? `(tv.TypeName, "*")` and calls it.
- `Generators` — scan `app/types/*/serializer/*.cs`, emit the table; `PLNG_SerializerCoverage` at error severity: every `[PlangType]` has a `Default.cs` *or* covers every registered format token.
- `app/types/path/serializer/Default.cs` — `writer.String(value.Relative)`, absorbing `this.JsonConverter.cs`'s logic; `path.Build("https://…")→"http"` (scheme is path's kind). Delete `this.JsonConverter.cs` once STJ-pathway callers route through the dispatch.
**Dependencies:** Stage 1 (registry + `Build`). `Wire.cs` already routes the value slot through `Normalize` + the writer.

## Design

> **You own the code.** Shapes below are intent; match the codebase.

Full contract and rationale: [plan/dispatch.md](plan/dispatch.md). The essentials:

**The flow.** `Wire.Write` → `data.Normalize(View)` walks the graph; at a registered-type value it returns `TypedValueNode(value, typeName)` instead of reflecting. The writer (which knows its own `Format`) hits that node in its `Value` dispatch, looks up the `(typeName, Format)` static `Write` (falling back to the `"*"`/`Default` entry), and calls it. The file *is* the format selector; no mime switch inside any method.

**Why a marker, not eager render.** `Normalize` stays format-agnostic — it only tags. The writer owns format identity, so the lookup happens there. Adding a future writer (protobuf) gets the whole type vocabulary for free with one `TypedValueNode` case.

**The build gate is what makes runtime lookup total.** Because `PLNG_SerializerCoverage` forces every `[PlangType]` to have `Default.cs` or full coverage, the writer's lookup can never miss for a registered type — no runtime "unknown format" branch needed. (A *runtime-loaded* type without a `"*"` renderer fails at load, Stage 7.)

**`path` as first mover.** `path` already has a `this.JsonConverter.cs` (single-format). Move its `Relative`-string logic into `path/serializer/Default.cs` and add `path.Build`. This proves Stage 1 (registry + Build + kind) and Stage 2 (dispatch) end-to-end on an existing type before any new type lands — a path value should serialize identically before and after, now through the new path.

**Sensitive carve-out.** A type that registers a serializer bypasses the `[Sensitive]` reflection filter (its value isn't decomposed). Rule for the coder: a type carrying secret payload either doesn't register a serializer (falls to reflection so `[Sensitive]` applies) or masks explicitly. Note it where relevant; `path` is not sensitive.
