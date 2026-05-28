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
- write out %photo%
```

The .goal text says nothing about console vs html — **the runtime state decides which serializer**. CLI run: text serializer. Web-request handler: html serializer. Same goal, same step, the channel context determines the format.

**Builder.** LLM picks `file.read` for step 1. Stage-typed return signature is `Data<image>` (the extension `.png` resolves to `image` via the type registry; `file.read.Build()` stamps the action's return type). LLM scope after step 1: `%photo%(image)`. For step 2, LLM picks `output.write` — no type-specific awareness, `output.write` takes a polymorphic `Data`.

**Runtime, step 1.** `file.read.Run()` reads bytes, returns `Data<image>.Ok(new app.types.image.@this(bytes, mime: "image/png"))`. The Image instance owns `Bytes` and `Mime`. The Data wrapper carries `Type = "image"`. Memory stores `%photo% = Data{Type="image", Value=<Image>}`.

**Runtime, step 2 — CLI run.** `output.write` grabs `%photo%` and hands the Data to whatever the active channel is — for a CLI run that's `stdout` with the text serializer. The channel does **not** look inside `Value`. It hands the Data + serializer identity (`text/plain`) into the wire pipeline; the pipeline dispatches based on `Data.Type` (the routing key) and the serializer's format identity. Image's text rendering picks the path placeholder. Console gets a readable line.

**Runtime, step 2 — web-request run.** Same step, same `output.write`. But now the active channel is the http-response writer with the html serializer. The same Data flows in; the dispatch keys on `Data.Type=image` and serializer `text/html`. Image's html rendering picks the path form by default (a static-files-served `<img src="/uploads/profile.png">` when the server allows it; base64 inline as a fallback). The browser gets markup.

Same Image instance, same step in the same goal, two channels, two wire shapes. The value was never re-materialized. The format mapping never lived in the channel. The goal author never had to know about HTML or text — just "write out the photo."

## What's already there

Three precedents in code that prove the pattern is partial today:

- **`app/types/path/`** — multi-variant type owning verbs (`this.Operations`), authorization (`this.Authorize`), derivation (`this.Derivation`), per-variant subfolders (`file/`, `http/`). The folder shape we're generalizing to every type. (Note: `this.JsonConverter.cs` in the path folder is the legacy single-format converter — it gets absorbed by the new dispatch shape when path adopts per-(type, format) serializer files, so this file moves out and a `path/serializer/` folder appears instead. Tracked in the open dispatch-shape question.)
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

## Folder tree after the work

What the codebase looks like at the end of this branch (delta from today, ignoring unrelated trees):

```
PLang/
  app/
    data/
      this.cs                          (existing) — Data, the courier
      this.Normalize.cs                (existing) — walk fallback, untouched for IWireWritable types
      Wire.cs                          (modified) — adds (Data.Type, writer.Format) dispatch hop
      IBooleanResolvable.cs            (existing)
      IWireWritable.cs                 (new — IF the single-method shape wins;
                                       see open question)
    types/
      this.cs                          (modified) — Primitives table becomes a discovered registry
      Conversion.cs                    (modified) — dispatches through registry
      Registry.cs                      (existing) — gains @PlangType discovery hooks
      PlangTypeAttribute.cs            (new)
      path/                            (existing) — gains serializer/ subfolder, sheds this.JsonConverter
        this.cs
        this.Authorize.cs
        this.Operations.cs
        this.Derivation.cs
        file/
        http/
        scheme/
        permission/
        serializer/                    (new — pending dispatch shape decision)
          json.cs                      (new) — replaces this.JsonConverter for json
          plang.cs                     (new)
          text.cs                      (new)
      number/                          (new — the first proving instance)
        this.cs                          NumberKind enum, storage slots, IBooleanResolvable
        this.Parse.cs                    Parse / TryParse / Resolve(string, context)
        this.Operators.cs                +, -, *, /, %, ==, != (policy-free lenient)
        this.Arithmetic.cs               Add/Sub/Mul/Div/Mod/Pow (policy-aware)
        this.Equality.cs                 Equals + canonical GetHashCode
        Config.cs                        : IConfig — OverflowMode, PrecisionMode
        NumberPolicy.cs                  the resolved struct passed into Arithmetic
        serializer/                    (pending dispatch decision)
          json.cs
          plang.cs
          text.cs
      image/                           (new — the second proving instance)
        this.cs                          Bytes, Mime, sourcePath, IBooleanResolvable
        this.Parse.cs                    Resolve(string) — path / data-url / base64
        this.ParseBytes.cs               Resolve(byte[]) — raw-bytes construction
        serializer/                    (pending dispatch decision)
          json.cs                        base64 string
          plang.cs                       base64 string (same as json today)
          text.cs                        path placeholder
          protobuf.cs                    raw bytes (future when writer ships)
      code/                            (new — the third proving instance)
        this.cs                          Source, Language, IBooleanResolvable
        this.Parse.cs                    Resolve(string) with language detection
        serializer/                    (pending dispatch decision)
          json.cs                        the raw source string
          plang.cs                       same
          text.cs                        same
      datetime/                        (new — folder for the parse complexity)
        this.cs                          wraps DateTimeOffset
        this.Parse.cs                    ISO-8601 tz-aware
      duration/                        (new — folder for the parse complexity)
        this.cs                          wraps TimeSpan
        this.Parse.cs                    1.02:03:04 + ISO-8601
      (date, time, string, int, long, decimal, bool, bytes, guid, …) — table-only entries
                                       in app/types/this.cs; no folders
    modules/
      math/                            (existing — actions grow Config and use NumberPolicy)
        add.cs                         (modified) — returns Data<number>; reads config via app.config.For<number.Config>
        … (subtract, multiply, divide, modulo, abs, ceiling, floor, round,
            max, min, power, sqrt, random — same shape)
      file/
        read.cs                        (modified) — Build() stamps Data.Type from extension via registry
        write.cs                       (modified) — accepts Data<number|image|code|…> uniformly
      output/
        write.cs                       (existing) — already polymorphic; nothing changes
    channels/
      serializers/
        IWriter.cs                     (modified) — gains a Format identity property
        this.cs                        (existing) — registry stays
        serializer/
          json.cs                      (modified — value-slot dispatch through type registry)
          plang/this.cs                (modified — same)
          Text.cs                      (modified — same)
        filters/                       (existing, untouched)
  Generators/
    Discovery/this.cs                  (modified) — scans [PlangType], emits registration table
    Emission/                          (modified) — generates the (type, format) dispatch table
    PLNG_TypeNotRegistered.cs          (new) — gate for handlers declaring Data<T> where T isn't a registered PlangType
```

The new top-level shape is: every PLang type is a folder under `app/types/`. The folder holds the value (`this.cs`), the parse-in factory (`this.Parse.cs`), optionally a config record (`Config.cs`) when the type has policy axes, and a `serializer/` subfolder per render-out format. Module action folders (`app/modules/math/`, `app/modules/image/`, …) consume the types via typed slots; they don't carry any per-type-format knowledge.

This is a sketch — the leaf-by-leaf file shapes may shift during stage-carving — but the overall topology is what to expect.

## Cross-cutting decisions

- **LLM scope shows the bare type, not subtype.** `%photo%(image)`, not `%photo%(image/png)`. Subtype precision lives at the runtime registry layer (the Image instance carries `Mime = "image/png"` for the serializer to use) but is hidden from the compile prompt. Confirmed 2026-05-28.
- **Channel never branches on type; type never knows about channels.** The bridge is the format identity (`ISerializer.Type` — the mime string). Channel passes its serializer through; type sees only the mime and dispatches. Adding a new channel doesn't force every type to grow a method; adding a new type doesn't force every channel to grow a renderer.
- **Fallback for non-implementers.** If a value does not implement `IWireWritable`, Normalize falls back to today's reflection walk into a property bag. Image, number, code adopt the new interface; arbitrary domain objects (Identity, Signature, user records) keep riding as today. Backwards-compatible by default.
- **The type registry replaces the flat `Primitives` table.** `app/types/this.cs:34`'s dictionary becomes a discovered registry: each `app/types/<name>/this.cs` registers (name, CLR type, optional MIME family) at App construction. Adding a new type means a new folder, not an edit in six places. `app.formats` becomes the extension-to-name helper (the parse-in lookup `read file profile.png` uses to stamp `Type=image`), not a parallel universe.
- **`path` retroactively gains `IWireWritable`** during this branch — it already has a JSON converter; adding the marker means path-as-protobuf and path-as-html will work the right way without a future migration. Cost is one method on `path.@this`.

## Settled in review

- **Type registry shape: discovery-time.** Source generator scans `app/types/<name>/this.cs`, emits a static registration table at compile time. Matches `[Action]` discovery. Confirmed 2026-05-28.
- **HTML deferred.** HTML serializer isn't real yet (today `text/html` aliases JSON in the registry); the `<img>` markup case stays a footnote and the work to give HTML its own writer is a separate branch.
- **Number's arithmetic policy stays on this branch.** It's a leaf-action concern (math handlers consult settings via `app.config.For<number.Config>(context)`), not the architectural spine — see [plan/policy.md](plan/policy.md) for the rewritten shape using the existing `app.config` walk. Not deferred.

## Open question for you

**Dispatch shape.** The earlier proposal: one method on the value (`IWireWritable.WriteTo(IWriter, ISerializer)`) that switches on `serializer.Type` internally. Ingi pushed back on 2026-05-28: that switch-inside-method is a smell from far enough away, and OBP says distinct (type × format) combinations get distinct files. The alternative: `app/types/<name>/serializer/<format>.cs` per (type, format), each file owns one rendering, source generator wires the dispatch table. Image-as-json lives at `app/types/image/serializer/json.cs`; image-as-text at `…/serializer/text.cs`; image-as-protobuf at `…/serializer/protobuf.cs`. The writer (json.Writer / future protobuf.Writer) carries its format identity; the wire pipeline looks up `(Data.Type, writer.Format) → static method` and calls it. Normalize stays as the fallback walk for non-registered types. My proposal lives in the open comment thread on [plan/dispatch.md](plan/dispatch.md); reading welcomed before I rewrite the dispatch deep-dive around it.

## How this lands vs. the old plan

The old `plan.md` (now overwritten — see git history) treated `number` as *the* deliverable with arithmetic policy as the architectural spine. Under this reframe, `number` is one of three proving instances and the spine is the dispatch pattern (type registry + `IWireWritable` + Normalize hook). The `plan/storage.md` and `plan/policy.md` files survive as deep dives on `number`'s leaf-action internals — referenced from `plan/types.md`, not from the spine.

The `plan/primitive-vocabulary.md` discussion file is retired — its substance (confirmed renames, the three-concept collision, the two-arc vs one-arc fork) is now folded into this spine and `plan/types.md`. The fork itself is resolved: neither arc, exactly. The dispatch pattern ships with three real instances; the wider format-kind lift (video, document, …) is later work using the same shape.
