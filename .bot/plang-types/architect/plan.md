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

1. **Every PLang type lives at `app/types/<name>/`** with `this.cs` carrying the value, lifecycle (`static Resolve(string, context)`, value-based equality), and the leaf-dispatch surfaces it owns. Sub-files at the type's discretion (`path` already has `this.Authorize`, `this.Operations`, `this.Derivation`). New types add a folder, register once, and immediately participate in the catalog, the LLM vocabulary, the wire pipeline, and channel routing.

2. **The type owns its serialization — one file per (type, format).** Each type has a `serializer/` subfolder: `app/types/<name>/serializer/<format>.cs`, one `Default.cs` for the uniform rendering plus a file per format that genuinely differs. `image/serializer/text.cs` renders a path placeholder, `image/serializer/protobuf.cs` raw bytes, `image/serializer/Default.cs` base64. No interface on the value, no internal mime switch — the file name **is** the format selector, the folder name **is** the type. The source generator wires the `(typeName, formatToken) → Write` dispatch table; the writer carries its `Format` token and does the lookup. `Data.Normalize` tags registered-type values as a deferred `TypedValueNode`; the reflection walk stays as the fallback for unregistered domain shapes.

3. **The type tag is the routing key.** `Data.Type` is set at construction (by the leaf action that produced the value) and rides untouched through transit. Variable memory stores it. Callstack carries it. Channel routing reads it. The LLM sees `%photo%(image)` in compile scope. No mid-pipeline transformation reads `Value`. Only the two leaf surfaces do.

Detail on the dispatch — the per-(type, format) file shape, how it hooks into `Normalize`/`Wire`/`IWriter`, the runtime-loaded-type path, format-by-format walkthroughs — lives in [plan/dispatch.md](plan/dispatch.md).

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

- **`app/types/path/`** — multi-variant type owning verbs (`this.Operations`), authorization (`this.Authorize`), derivation (`this.Derivation`), per-variant subfolders (`file/`, `http/`). The folder shape we're generalizing to every type. (`this.JsonConverter.cs` is the legacy single-format converter — it gets absorbed into `path/serializer/Default.cs` under the new dispatch.)
- **`app/types/Registry.cs`** — `[PlangType]` discovery already scans assemblies and builds the name↔type index; `RegisterRuntime(name, type)` is the runtime hook; `ResolveType` already favors runtime registrations over built-in. The registry the plan extends is real, not new.
- **`app/data/IBooleanResolvable.cs`** — `Data.ToBooleanAsync` dispatches when the value implements it. The canonical implementer is `path` (truthiness = "does the resource exist", may require I/O). The same dispatch-on-marker shape the `TypedValueNode` serializer hook mirrors.
- **`static Resolve(string, context)`** — the existing factory convention. `path.Resolve` picks scheme variant from the raw string; `app.types.Conversion.TryConvertTo` dispatches.
- **`code.load`** (`PLang/app/modules/code/load.cs`) — load-scan-register a DLL for `ICode` providers; the template the runtime type-loading feature follows.

What's missing: the per-(type, format) serializer files + the `TypeSerializers` dispatch table + the `Normalize` tag-hook; folding the flat `Primitives` table at `app/types/this.cs:34` into the registry; and concrete proving instances beyond `path`.

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
      this.cs                          (existing) — Data, the courier (Value is object → boxes value types)
      this.Normalize.cs                (modified) — tag registered types as TypedValueNode; reflect the rest as today
      Wire.cs                          (existing) — value slot already routes through Normalize + writer
      TypedValueNode.cs                (new) — sealed record (object Value, string TypeName); the deferred marker
      IBooleanResolvable.cs            (existing)
    types/
      this.cs                          (modified) — Primitives table folds into the discovered registry
      Conversion.cs                    (modified) — dispatches through registry; +Resolve(byte[]) branch
      Registry.cs                      (existing) — [PlangType] discovery + RegisterRuntime already present
      PlangTypeAttribute.cs            (existing) — already in the codebase
      TypeSerializers.cs               (new) — (typeName, formatToken) → Write delegate; generated + runtime-registered
      ITypeRenderer.cs                 (new) — interface a loaded DLL implements per format (mirrors ICode)
      path/                            (existing) — gains serializer/, sheds this.JsonConverter
        this.cs  this.Authorize.cs  this.Operations.cs  this.Derivation.cs
        file/  http/  scheme/  permission/
        serializer/
          Default.cs                   (new) — writer.String(Relative); absorbs this.JsonConverter
      number/                          (new — first proving instance; readonly struct @this)
        this.cs                          NumberKind enum, storage slots, IEquatable, IBooleanResolvable (struct)
        this.Parse.cs                    Parse / TryParse / Resolve(string,ctx) / Resolve(byte[],ctx)
        this.Operators.cs                +, -, *, /, %, ==, != (lenient default)
        this.Arithmetic.cs               Add/Sub/Mul/Div/Mod/Pow (policy-aware, Data-returning)
        this.Equality.cs                 lenient Equals + ExactEquals + canonical GetHashCode
        Config.cs                        : IConfig — OverflowMode, PrecisionMode
        NumberPolicy.cs                  the resolved struct passed into Arithmetic
        serializer/
          Default.cs                     (number, *) → writer.Int/Long/Decimal/Double
      image/                           (new — second proving instance; format-asymmetric)
        this.cs                          Bytes, Mime, SourcePath, IBooleanResolvable
        this.Parse.cs                    Resolve(string) path/data-url/base64; Resolve(byte[])
        serializer/
          text.cs                        (image, text)     → path placeholder
          protobuf.cs                    (image, protobuf) → raw bytes (when that writer ships)
          Default.cs                     (image, *)         → base64 string (covers json + plang)
      code/                            (new — third proving instance; text-semantic)
        this.cs                          Source, Language, IBooleanResolvable
        this.Parse.cs                    Resolve(string) with language detection
        serializer/
          Default.cs                     (code, *) → writer.String(source)
                                         (html.cs added later when an HTML writer exists)
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
        intdiv.cs                      (new, pending divide decision) — truncating integer division
      file/
        read.cs                        (modified) — Build() stamps Data.Type from extension via registry
        write.cs                       (modified) — accepts Data<number|image|code|…> uniformly
      output/
        write.cs                       (existing) — already polymorphic; nothing changes
      code/
        load.cs                        (modified) — also registers [PlangType] classes + ITypeRenderers from the DLL
    channels/
      serializers/
        IWriter.cs                     (modified) — gains a Format token property ("json"/"plang"/"text"/…)
        this.cs                        (existing) — registry stays
        serializer/
          json.cs                      (modified — TypedValueNode case in Value dispatch)
          plang/this.cs                (modified — same)
          Text.cs                      (modified — same)
        filters/                       (existing, untouched)
  Generators/
    Discovery/this.cs                  (modified) — scans [PlangType] + serializer/*.cs
    Emission/                          (modified) — emits the TypeSerializers (type, format) table
    PLNG_SerializerCoverage.cs         (new) — gate: each [PlangType] has Default.cs or covers every format token
```

The new top-level shape is: every PLang type is a folder under `app/types/`. The folder holds the value (`this.cs`), the parse-in factory (`this.Parse.cs`), optionally a config record (`Config.cs`) when the type has policy axes, and a `serializer/` subfolder with one `Default.cs` (uniform rendering) plus a file per format that genuinely differs. Module action folders consume the types via typed slots; they carry no per-type-format knowledge.

This is a sketch — the leaf-by-leaf file shapes may shift during stage-carving — but the overall topology is what to expect.

## Cross-cutting decisions

- **LLM scope shows the bare type, not subtype.** `%photo%(image)`, not `%photo%(image/png)`. Subtype precision lives at the runtime registry layer (the Image instance carries `Mime = "image/png"` for the serializer to use) but is hidden from the compile prompt. Confirmed 2026-05-28.
- **Channel never branches on type; type never knows about channels.** The bridge is the writer's `Format` token. The writer looks up `(Data.Type, Format) → serializer file`; the type's serializer file sees only the writer's primitives and emits. Adding a new channel/writer doesn't force every type to grow a method; adding a new type doesn't force every writer to grow a renderer.
- **Fallback for unregistered types.** If a value's CLR type isn't a registered `[PlangType]`, Normalize reflects it into a property bag exactly as today. Registered types (number, image, code, path) get tagged as `TypedValueNode` and dispatched to their serializer files. Backwards-compatible by default; arbitrary domain objects (Identity, Signature, user records) are untouched.
- **The type registry subsumes the flat `Primitives` table.** `app/types/this.cs:34`'s dictionary folds into the existing `[PlangType]` registry (`Registry.cs` — already real, already scans assemblies). Adding a new type means a new folder, not an edit in six places. `app.formats` becomes the extension-to-name helper (the parse-in lookup `read file profile.png` uses to stamp `Type=image`), not a parallel universe.
- **`path` gets a `serializer/Default.cs`** this branch, absorbing its `this.JsonConverter.cs` (the OBP smell Ingi flagged). Path-as-protobuf and path-as-html then work through the same dispatch as every other type — no per-format migration later.

## Extending the type vocabulary at runtime

The built-in types are discovery-time (source generator). But the vocabulary isn't closed — a PLang developer can add or override types at runtime by loading a DLL, and the infrastructure for this is **already in the codebase**:

```
- load mynumbers.dll
```

`code.load` (`PLang/app/modules/code/load.cs`) already does the load-scan-register dance for `ICode` providers: load a DLL, scan `GetExportedTypes()` for an interface, register each. The type-loading feature is the same shape applied to types:

1. **Add a type.** Scan the DLL for `[PlangType]`-bearing classes → `Registry.RegisterRuntime(name, clrType)` (the existing hook at `Registry.cs:103`). The new type is now resolvable by name everywhere.
2. **Wire its renderings.** A loaded type can't be generator-wired (the generator already ran). So the DLL ships one `ITypeRenderer` instance per format it supports, and the loader registers them into the same `TypeSerializers` table the generator feeds. This is the type-system analogue of `ICode`.
3. **Overwrite a built-in** (e.g. redefine `int`). Works at the resolution layer for free: `Registry.ResolveType` checks runtime registrations before the discovery-time table (`Registry.cs:85`), so `RegisterRuntime("int", customType)` shadows the built-in. The serializer table follows the same precedence.

**The honest limit on overwriting built-ins.** Runtime registration changes resolution and serialization — what a name maps to, how a value renders. It cannot rewrite what the **source generator already baked at build**: PLNG parameter-slot validation, the `Data<int>` slots on already-compiled handlers, the type stamps in shipped `.pr` files. So `- load myint.dll` makes `int` resolve and render differently going forward, but a handler compiled against the built-in `int` still sees the built-in at its typed slot. **Adding** new types is unconstrained; **overwriting** built-ins is "new resolution + new rendering, same compiled slots." Detail in [plan/dispatch.md](plan/dispatch.md) "Runtime-loaded and overwritten types".

This is what keeps PLang's type system open — the same way `code.load` keeps the provider system open. The built-in set is a starting vocabulary, not a closed one.

## Settled in review

- **Dispatch shape: per-(type, format) serializer files.** `app/types/<name>/serializer/<format>.cs` — one `Default.cs` (uniform rendering) plus a file per format that genuinely differs. Source generator wires the `(typeName, formatToken) → Write` table; the writer carries its `Format` token and does the lookup. No interface on the value, no internal mime switch. Signed off 2026-05-29. Full contract in [plan/dispatch.md](plan/dispatch.md).
- **Type registry shape: discovery-time.** Source generator scans `app/types/<name>/` (`[PlangType]` + `serializer/*.cs`), emits the registration + dispatch tables at compile time. Matches `[Action]` discovery. The runtime path (`RegisterRuntime`) layers on top for loaded DLLs. Confirmed 2026-05-28/29.
- **`number` is a `readonly struct`.** Named `@this` (convention intact). Value semantics, no Context. Note: the allocation rationale is weaker than it looks — `Data.Value` is `object`, so a struct boxes on store; the real basis is value-semantics honesty. Detail + the correction in [plan/storage.md](plan/storage.md) "The shape" and [plan/review-opus-4-8.md](plan/review-opus-4-8.md).
- **`number` equality is lenient by default, `ExactEquals` opt-in.** `0.1 == 0.1` is true regardless of storage kind; the careful caller reaches for `ExactEquals`. Caveat (non-transitive at the precision boundary) documented in storage.md.
- **`number` error model: throws at the C# boundary, `Data` at the handler boundary.** Operators/private internals throw like any CLR numeric; module surface returns `Data.Error`.
- **HTML deferred.** HTML serializer isn't real yet (today `text/html` aliases JSON); the `<img>`/`<pre>` markup cases are footnotes until an HTML writer ships on a separate branch.
- **Number's arithmetic policy stays on this branch.** Leaf-action concern via `app.config.For<number.Config>(context)`, not the spine. See [plan/policy.md](plan/policy.md).

## Open questions for you

- **Divide / Power promotion.** `7 / 2` currently resolves to integer-divide `3` (Divide shares Add's promotion rule). Opus 4.8 flagged this as a footgun for a non-programmer audience. My recommendation: `/` always promotes out of integer kinds (`7/2 → 3.5`), `^` promotes on negative/fractional exponents, truncating division moves to a named `math.intdiv`. Pending your call — see [plan/storage.md](plan/storage.md) Arithmetic note and [plan/review-opus-4-8.md](plan/review-opus-4-8.md) point 3.
- **Rational numbers** (`- set %x% = 7/8` stays exact). Discussed in [plan/review-opus-4-8.md](plan/review-opus-4-8.md): you leaned toward a separate `rational` sibling type (Option B), added later, not this branch. Recorded; nothing to build now.

## How this lands vs. the old plan

The old `plan.md` (now overwritten — see git history) treated `number` as *the* deliverable with arithmetic policy as the architectural spine. Under this reframe, `number` is one of three proving instances and the spine is the dispatch pattern (type registry + per-(type, format) serializer files + Normalize tag-hook). The `plan/storage.md` and `plan/policy.md` files survive as deep dives on `number`'s leaf-action internals — referenced from `plan/types.md`, not from the spine.

The `plan/primitive-vocabulary.md` discussion file is retired — its substance (confirmed renames, the three-concept collision, the two-arc vs one-arc fork) is now folded into this spine and `plan/types.md`. The fork itself is resolved: neither arc, exactly. The dispatch pattern ships with three real instances; the wider format-kind lift (video, document, …) is later work using the same shape.
