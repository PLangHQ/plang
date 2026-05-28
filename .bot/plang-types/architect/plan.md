# `plang-types` — typed values that own their leaf behavior

**Branch:** `plang-types` (off `runtime2`, was `number-type` before the 2026-05-28 reframe — see `summary.md`).
**Status:** plan rewritten; no stages carved.

## Why

PLang has higher-level kinds of value — `number`, `image`, `code`, `document`, … — that today live as **labels in one corner** (`app/formats/this.cs`, the 30+ Kind enum) and **CLR types in another** (`app/types/this.cs:34`, the flat `Primitives` table). They never got married. The result: adding a new primitive touches six unrelated files, half the entries land asymmetric (DateTimeOffset accepted in `IsPrimitive` but missing from the name table), and category kinds like `image` cannot be picked by the LLM at all even though every other part of the system knows what an image is.

The broader insight, surfaced in the 2026-05-28 conversation: the runtime should be a courier. It moves `Data { Type = "image", Value = <Image> }` from action to action without ever dereferencing `Value`. Only **leaves** reach in. There are two leaf surfaces:

1. **Leaf actions** — `math.add` opens up a `number` and computes; `image.resize` opens up bytes + mime and transforms.
2. **Leaf serializers** — when a value reaches a channel, the value tells the channel how to render itself for that channel's format. Image in `text/plain` → path or placeholder. Image in `text/html` → `<img>` markup. Image in `application/plang` or `application/json` → base64 string. Image in `application/protobuf` → raw bytes. Same instance, four wire shapes, the type owns the mapping.

In between — variable memory, callstack frames, goal-to-goal handoff, signing, the `data` envelope — nothing touches the value's content. The type tag rides on `Data.Type`, the routing key. Once that pattern is real, `number` is no longer a special case; it's the first instance of a general shape.

## The architectural decision

**Type-as-router with leaf-owned behavior.** Three commitments:

1. **Every PLang type lives at `app/types/<name>/`** with `this.cs` carrying the value, lifecycle (`static Resolve(string, context)`, value-based equality), and the leaf-dispatch surfaces it owns. Sub-files at the type's discretion (`path` already has `this.Authorize`, `this.Operations`, `this.JsonConverter`, `this.Derivation`). New types add a folder, register once, and immediately participate in the catalog, the LLM vocabulary, the wire pipeline, and channel routing.

2. **The value owns its serialization.** New marker `app.data.IWireWritable`, parallel in spirit to `IBooleanResolvable` (truthiness) and `IRawNameResolvable` (substitution-skip). `Data.Normalize` already walks the value-graph into a uniform tree — today via reflection on arbitrary CLR shapes. We add one branch: if the value implements `IWireWritable`, hand it the active `ISerializer` (carrying format identity — `Type = "application/plang"`, `"text/html"`, etc.) and the `IWriter` (the format encoder), and let the value choose which primitive its content rides as for that format. The writer's primitive vocabulary stays the encoder; the type picks the slot. Today's reflection walk stays as the fallback for plain domain shapes that don't need format-aware rendering.

3. **The type tag is the routing key.** `Data.Type` is set at construction (by the leaf action that produced the value) and rides untouched through transit. Variable memory stores it. Callstack carries it. Channel routing reads it. The LLM sees `%photo%(image)` in compile scope. No mid-pipeline transformation reads `Value`. Only the two leaf surfaces do.

Detail on the dispatch — what `IWireWritable` actually looks like, how it hooks into `Normalize`/`Wire`/`IWriter`, format-by-format walkthroughs — lives in [plan/dispatch.md](plan/dispatch.md).

## The movie — PLang dev to leaf

A developer writes:

```
Start
- read file profile.png, write to %photo%
- write out %photo% to console
- write out %photo% to html
```

**Builder.** LLM picks `file.read` for step 1. Stage-typed return signature is `Data<image>` (the extension `.png` resolves to `image` via the type registry; `file.read.Build()` stamps the action's return type). LLM scope after step 1: `%photo%(image)`. For step 2, LLM picks `output.write` for the console channel — no type-specific awareness, `output.write` takes a polymorphic `Data`. Same for step 3 with the html channel.

**Runtime, step 1.** `file.read.Run()` reads bytes, returns `Data<image>.Ok(new app.types.image.@this(bytes, mime: "image/png"))`. The Image instance owns `Bytes` and `Mime`. The Data wrapper carries `Type = "image"`. Memory stores `%photo% = Data{Type="image", Value=<Image>}`.

**Runtime, step 2.** `output.write` grabs `%photo%`, hands the Data to the console channel's writer with serializer identity `text/plain`. The channel does **not** look inside `Value`. It calls `data.Normalize(...)` to walk the graph; Normalize encounters the Image instance, sees `IWireWritable`, asks it to render for `serializer.Type = "text/plain"`. Image's implementation dispatches on mime: text/plain → `writer.String("[image: image/png 1.2MB]")` (or the originating path, if held). Console gets a readable line.

**Runtime, step 3.** Same flow, serializer identity `text/html`. Image dispatches: text/html → `writer.String("<img src=\"data:image/png;base64,…\">")`. HTML channel gets the markup. (Today `text/html` is aliased to `application/json`'s writer in the registry — HTML markup ships as a JSON string field. When HTML grows its own writer, the raw-string slot is the one the type's `IWireWritable.WriteTo` already targets; no type-side change needed.)

Same Image instance, three uses: stored, rendered for console, rendered for html. The value was never re-materialized in memory. The format mapping never lived in the channel.

## What's already there

Three precedents in code that prove the pattern is partial today:

- **`app/types/path/`** — multi-variant type owning verbs (`this.Operations`), authorization (`this.Authorize`), serialization (`this.JsonConverter`), derivation (`this.Derivation`), per-variant subfolders (`file/`, `http/`). The folder shape we're generalizing to every type.
- **`app/data/IBooleanResolvable.cs`** — `Data.ToBooleanAsync` dispatches when the value implements it. The canonical implementer is `path` (truthiness = "does the resource exist", may require I/O). The exact dispatch pattern `IWireWritable` mirrors.
- **`static Resolve(string, context)`** — the existing factory convention. `path.Resolve` picks scheme variant from the raw string; `app.types.Conversion.TryConvertTo` dispatches.

What's missing: the `IWireWritable` interface and Normalize hook; a type registry that subsumes the flat `Primitives` table at `app/types/this.cs:34`; and concrete proving instances beyond `path`.

## Vocabulary commitments

Three proving instances on this branch, picked because each exercises a distinct leaf-access pattern:

- **`number`** — tagged-union storage, leaf-action-heavy (`math.*`), uniform across all formats (every wire format knows how to write a number primitive via `IWriter.Int`/`Long`/`Decimal`/`Double`). Plan detail in [plan/storage.md](plan/storage.md) (still valid) and [plan/policy.md](plan/policy.md) (arithmetic policy, still valid — collapses to a leaf-action concern under this reframe, not the spine).
- **`image`** — `byte[]` + mime, format-asymmetric (raw bytes in protobuf vs base64 string in JSON/plang vs `<img>` markup in HTML vs path placeholder in text), construction-asymmetric (file read vs URL fetch vs base64 decode). The hardest proof case for the dispatch.
- **`code`** — string + language tag (`csharp`, `python`, …), text-shaped but content-aware (HTML wraps in `<pre><code>` with syntax highlighting; plain text passes through; JSON/plang stores as string-with-language-tag).

Plus the confirmed cleanups already settled with you on 2026-05-28: `datetime` rebinds to `DateTimeOffset` (no more DateTime), `date` → `DateOnly`, `time` → `TimeOnly`, `duration` → `TimeSpan` (replaces the `time`/`timespan` overload). These are mechanical compared to the three above — they shed the asymmetric half-states without needing format-aware rendering.

Out of scope for this branch: `video`, `audio`, `document`, `archive`, `font`, `executable` (the rest of the formats Kind enum). They slot into the same shape once it's proven; carving them now is speculative work without driving action surfaces.

Per-type details, ownership matrix, and registration shape in [plan/types.md](plan/types.md).

## Cross-cutting decisions

- **LLM scope shows the bare type, not subtype.** `%photo%(image)`, not `%photo%(image/png)`. Subtype precision lives at the runtime registry layer (the Image instance carries `Mime = "image/png"` for the serializer to use) but is hidden from the compile prompt. Confirmed 2026-05-28.
- **Channel never branches on type; type never knows about channels.** The bridge is the format identity (`ISerializer.Type` — the mime string). Channel passes its serializer through; type sees only the mime and dispatches. Adding a new channel doesn't force every type to grow a method; adding a new type doesn't force every channel to grow a renderer.
- **Fallback for non-implementers.** If a value does not implement `IWireWritable`, Normalize falls back to today's reflection walk into a property bag. Image, number, code adopt the new interface; arbitrary domain objects (Identity, Signature, user records) keep riding as today. Backwards-compatible by default.
- **The type registry replaces the flat `Primitives` table.** `app/types/this.cs:34`'s dictionary becomes a discovered registry: each `app/types/<name>/this.cs` registers (name, CLR type, optional MIME family) at App construction. Adding a new type means a new folder, not an edit in six places. `app.formats` becomes the extension-to-name helper (the parse-in lookup `read file profile.png` uses to stamp `Type=image`), not a parallel universe.
- **`path` retroactively gains `IWireWritable`** during this branch — it already has a JSON converter; adding the marker means path-as-protobuf and path-as-html will work the right way without a future migration. Cost is one method on `path.@this`.

## Open questions for you

1. **Interface location and signature.** Leaning `app/data/IWireWritable.cs` (sits next to `IBooleanResolvable`, since `Data.Normalize` is the dispatcher). Signature: `void WriteTo(IWriter writer, ISerializer serializer)` — gives the value both the encoder and the format identity. Sync because format encoding shouldn't reach I/O. Alternative: pass just the mime string instead of the serializer instance — leaner, but loses the ability to delegate sub-fragments back through the serializer (which Image might want for properties).

2. **Type registry shape.** Two options. *Discovery-time* (the source generator scans `app/types/<name>/this.cs` and emits a static registration table at compile time — matches how `[Action]` discovery already works). *App-construction-time* (App scans loaded assemblies for `[PlangType]` once at startup — matches how `path.scheme.@this` populates today). Discovery-time is faster and lets PLNG-style gates catch missing registrations at build; App-time is simpler and matches the existing scheme registry. I lean discovery-time but it's not a hill.

3. **Number's arithmetic policy.** Under this reframe, the `NumberPolicy` system in [plan/policy.md](plan/policy.md) is a leaf-action concern (math handlers consult settings, call policy-aware overloads). The plan still holds for number, but it's no longer the architectural spine — it's an internal detail of one type. Confirm you still want it scoped this way, vs. deferring policy entirely and shipping number with single-mode arithmetic first.

4. **HTML as its own writer vs. JSON-aliased.** Today `text/html` aliases `application/json` in the serializer registry. The `<img>` markup case for image works around this by riding as a JSON string field. If we let HTML grow its own writer on this branch, every type's `IWireWritable.WriteTo` for `text/html` writes raw markup (no JSON escaping). Cleaner long-term; ships HTML as a real format. But it widens scope. Defer to a follow-up?

## How this lands vs. the old plan

The old `plan.md` (now overwritten — see git history) treated `number` as *the* deliverable with arithmetic policy as the architectural spine. Under this reframe, `number` is one of three proving instances and the spine is the dispatch pattern (type registry + `IWireWritable` + Normalize hook). The `plan/storage.md` and `plan/policy.md` files survive as deep dives on `number`'s leaf-action internals — referenced from `plan/types.md`, not from the spine.

The `plan/primitive-vocabulary.md` discussion file is retired — its substance (confirmed renames, the three-concept collision, the two-arc vs one-arc fork) is now folded into this spine and `plan/types.md`. The fork itself is resolved: neither arc, exactly. The dispatch pattern ships with three real instances; the wider format-kind lift (video, document, …) is later work using the same shape.
