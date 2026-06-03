# Stage 1: The reader registry — consolidate the read side, no behavior change

> **Note for coder:** every signature, file path, and method name below is a **suggestion** that captures architect intent — not a contract. You own the implementation. Reshape, rename, restructure, or replace anything as the real constraints demand. Push back on the design itself if you find it wrong.

**Goal:** Build one symmetric reader registry that mirrors `app.type.renderer.@this`, and fold the three-plus scattered read mechanisms into it. This is pure consolidation — **no behavior change**. The same bytes turn into the same values; they just reach the decoding through one door instead of `type.Convert` / `Convert` hooks / `FromWire` / `path.JsonConverter` / `type.json` / a pile of `JsonConverter<T>`.

The point of doing this first: it's the floor every other stage stands on (lazy Data materializes through it, numbers read toward their kind through it, the channel boundary produces values through it), and it deletes the format-coupled names in one pass.

**Scope:**
- New `app/type/reader/this.cs` — `app.type.reader.@this`, the registry. Mirror of `app/type/renderer/this.cs` (read that file first; copy its shape — two-tier dictionary, `Of`, discovery, `Register`).
- New `app/channel/serializer/IReader.cs` — the format-decode abstraction, mirror of `IWriter.cs`.
- New `app/channel/serializer/json/reader.cs` — the json decoder, mirror of `json/writer.cs`.
- A static `Read` added next to `Write` in each `app/type/<name>/serializer/Default.cs` (text, number, path, image, datetime, duration, code, choice — whichever have a `Write` today).
- `app/type/convert/this.cs` — `OwnerOf`'s `clr → (family, kind)` switch distributes onto each family.
- Deletions/folds: `app/type/path/this.JsonConverter.cs`, `app/type/this.json.cs`, and the domain-coupled `JsonConverter<T>` set (`ErrorWire`, `signing.HashDataConverter`, `TimeSpanIso8601`); `FromWire`/`WireReader` on `app/type/this.cs` and `crypto.hash`.

**Dependencies:** None. This is the floor.

**Out of scope:**
- Lazy materialization (Stage 3) — Stage 1 keeps the existing eager call sites; they just call the registry now.
- Number breadth (Stage 2) — `number.Read` here mirrors today's behavior; the full tower lands in Stage 2.
- The channel boundary (Stage 4) and access-driven resolution (Stage 5).
- **Snapshot carve-out:** leave `snapshot.FromWire` (`app/snapshot/this.Wire.cs`) and `app.Snapshot*` (`app/this.SnapshotWire.cs`) signatures untouched — another branch owns snapshot. Adjust their internals only if needed to compile.

**Deliverables:**

1. **`app.type.reader.@this` registry.** Mirror `renderer/this.cs`: two `ConcurrentDictionary<(string Type, string Kind), Read>` tiers — `_generated` (reflection-discovered at startup from `serializer/Default.cs` static `Read` methods) and `_runtime` (registered dynamically via `Register`, for `code.load`'d types). `Of(typeName, kind)` precedence: runtime-exact → generated-exact → runtime-`"*"` → generated-`"*"`. The `"*"` wildcard is `Default.cs`. Drop `_hasAny` — that's a write-side gate (`Normalize` tag-or-reflect); the read side dispatches on a stamped `(type, kind)` directly, no gate. Suggested delegate:
   ```csharp
   public delegate object? Read(object raw, string? kind, ReadContext ctx);
   public Read? Of(string typeName, string? kind);
   ```
2. **`IReader` + `json/reader.cs`.** `IReader` is the format-decode surface (mirror of `IWriter`): turn bytes → a structure/token stream. `json/reader.cs` is the first implementation, mirror of `json/writer.cs`. This is layer 1; the type's `Read` is layer 2 (value materialize). For a raw file/http payload there's no wire structure, so `raw` is the source form and the type reads it per `kind`.
3. **Per-type `Read`.** Each `serializer/Default.cs` gains a static `Read` beside `Write`. **Re-house, don't reimplement:** the json reader still uses the existing System.Text.Json pipeline; `path.Read` is the body of today's `path.JsonConverter.Read` (resolve via `path.Resolve`); `number.Read` is today's number parse; `FromWire` bodies become the owning type's `Read`. The logic moves behind one door — it is not rewritten.
4. **Distribute `OwnerOf`.** Replace the central `if u == typeof(int) … typeof(float) …` ladder (`app/type/convert/this.cs:58`) with a per-family declaration — each family states which CLR types it owns and the kind for each (`number` → int/long/…; `text` → string; `path` → its subclasses). `OwnerOf`/the registry composes the routing from those declarations. This is what lets Stage 2 add numeric kinds by editing only `number`.
5. **Delete the format-coupled names.** `path.JsonConverter` deletes — path's read is now its `Read`; the 6 registration sites (`Diagnostics/Format.cs:31`, `channel/serializer/Json.cs:47`, `channel/serializer/plang/this.cs:51`, `module/builder/this.cs:50`, `this.cs:420`, `type/list/Conversion.cs:42,64`) stop wiring a path-specific converter. `type.json` deletes (folds into the registry). The domain-coupled `JsonConverter<T>` (`ErrorWire`, `HashDataConverter`, `TimeSpanIso8601`) become `Read` entries; genuinely STJ-shape plumbing for JSON itself stays inside `json/reader.cs`.
6. **Residual plumbing stays.** The generic branches in `TryConvert` (`app/type/list/Conversion.cs`) that are *not* type-owned reads — nullable unwrap, the assignable fast-path, list element-walk — stay as the registry's fallback. Only the type-owned branches move onto types.
7. **Error handling.** A `Read` that fails (malformed json, wrong shape) produces an `Error` rather than throwing into a courier. Surfacing flows through `As<T>()` / navigation (which return `Data` and carry an `Error`). The bare `.Value` getter is best-effort.

## Design

**Mirror the renderer, but key on `(type, kind)`, not `(type, format)`.** The renderer encodes a value *into* a channel, so it keys on the channel format. The reader decodes the value's *own* raw form, so it keys on `kind` — the encoding within the type's shape: `json`/`xml`/`yaml` for `object`, `csv`/`xlsx` for `table`, `int`/`uint` for `number`, `png`/`jpg` for `image`, `md`/`plain` for `text`. Same discovery, same wildcard, same precedence — different key. The channel wire format (how the surrounding `application/plang` container is encoded) stays the channel serializer's job and isn't this registry's axis.

**Validate the keying with a round-trip before building the rest:** write a `path` into json via the renderer, read it back via the reader. If `(type, kind)` round-trips a path, an image, a number, and a text/json document, the keying holds.

**Two layers, mirroring the write side.** Layer 1 (`IReader`, `json/reader.cs`) decodes bytes → structure; layer 2 (`type.Read`) materializes the value from that. Same split `IWriter` / `type.Write` already has. Don't collapse them — `path.Read` shouldn't know it came from json.

**What "no behavior change" means here.** Stage 1 does not make anything lazy and does not change any type stamp (numbers still collapse `float→double`, json still stamps whatever it stamps today — those change in Stages 2 and 4). The call sites that eagerly convert today still convert eagerly; they just call `reader.Of(type, kind).Read(...)` instead of the old fan-out. Prove it by running the full suite green before and after with no expected-output edits.

**`Read` discovery.** Copy `renderer.IndexAssembly` almost verbatim: scan for static classes in an `…serializer` namespace, map the parent folder to the type name and the file name to the kind token (`Default` → `"*"`), find a static `Read` with the agreed signature, register it in `_generated`. The discovery is the renderer's with `Write`→`Read`.
