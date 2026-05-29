# `plang-types` — typed values that own their leaf behavior

**Branch:** `plang-types` (off `runtime2`).
**Status:** design settled; stage files to be carved next.

## Why

PLang has higher-level kinds of value — `number`, `image`, `code`, `document`, … — that today live as **labels in one corner** (`app/formats/this.cs`, the 30+ Kind enum) and **CLR types in another** (`app/types/this.cs:34`, the flat `Primitives` table). They never got married. Adding a primitive touches six unrelated files; half the entries land asymmetric (DateTimeOffset is accepted by `IsPrimitive` but missing from the name table); category kinds like `image` can't be picked by the LLM at all, even though every other part of the system knows what an image is.

The framing that unlocks it: **the runtime is a courier.** It moves `Data { Type = "image", Value = <Image> }` from action to action without ever dereferencing `Value`. Only **leaves** reach in, and there are exactly two leaf surfaces:

1. **Leaf actions** — `math.add` opens up a `number` and computes; `image.resize` opens up bytes + mime and transforms.
2. **Leaf serializers** — when a value reaches a channel, the value's own per-format renderer decides its wire shape. Image as `text` → a path or placeholder. Image as `json`/`plang` → base64 string. Image as `protobuf` → raw bytes. Same instance, different wire shapes, the type owns the mapping.

Everything between — variable memory, callstack frames, goal-to-goal handoff, signing, the `data` envelope — sees only the package, reads `Data.Type` to route, and never touches the content. (This is now [OBP Rule #9](../../../Documentation/v0.2/object_pattern_formal.md): only leaves touch `Data.Value`.) Once that pattern is real, `number` is no longer a special case — it's the first instance of a general shape.

## The architectural decision

**Type-as-router with leaf-owned behavior.** Three commitments:

1. **Every PLang type lives at `app/types/<name>/`.** `this.cs` carries the value, lifecycle (`static Resolve(string, context)`, value-based equality), and the leaf surfaces it owns. Sub-files at the type's discretion (`path` already has `this.Authorize`, `this.Operations`, `this.Derivation`). A new type is a new folder; it registers once and immediately participates in the catalog, the LLM vocabulary, the wire pipeline, and channel routing.

2. **The type owns its serialization — one file per (type, format).** Each type has a `serializer/` subfolder: one `Default.cs` for the uniform rendering, plus a file per format that genuinely differs. `image/serializer/text.cs` renders a path placeholder, `image/serializer/protobuf.cs` raw bytes, `image/serializer/Default.cs` base64. No interface on the value, no mime switch inside a method — the file name **is** the format selector, the folder name **is** the type. The source generator wires a `(typeName, formatToken) → Write` table; the writer carries its `Format` token and does the lookup.

3. **The type tag is the routing key.** `Data.Type` is set at construction by the leaf action that produced the value, and rides untouched through transit. Variable memory stores it, the callstack carries it, channel routing reads it, the LLM sees `%photo%(image)` in compile scope. No mid-pipeline step reads `Value` — only the two leaf surfaces do.

The dispatch mechanism — the per-(type, format) file shape, the `Normalize` tag-hook, the writer-side lookup, the runtime-loaded-type path — is in [plan/dispatch.md](plan/dispatch.md).

## The movie — PLang dev to leaf

```
Start
- read file profile.png, write to %photo%
- write out %photo%
```

The `.goal` says nothing about console vs. web — **the runtime state picks the serializer.** A CLI run uses the text serializer; a web-request handler uses the html serializer. Same goal, same step.

**Builder.** The LLM picks `file.read` for step 1. The return signature is `Data<image>` — the extension `.png` resolves to `image` via the registry, and `file.read.Build()` stamps the action's return type. Scope after step 1: `%photo%(image)`. For step 2 the LLM picks `output.write`, which takes a polymorphic `Data` and needs no type-specific awareness.

**Runtime, step 1.** `file.read.Run()` reads bytes and returns `Data<image>.Ok(new image.@this(bytes, mime: "image/png"))`. The Image owns `Bytes` and `Mime`; the Data wrapper carries `Type = "image"`. Memory stores `%photo% = Data{Type="image", Value=<Image>}`.

**Runtime, step 2 — CLI.** `output.write` hands the Data to the active channel (`stdout`, text serializer). The channel never looks inside `Value`. The wire pipeline keys on `(Data.Type=image, Format=text)` → `image/serializer/text.cs` → a path placeholder. The console gets a readable line.

**Runtime, step 2 — web request.** Same step, same `output.write`, but the active channel is the http-response writer (json serializer today; an html serializer later). The pipeline keys on `(image, json)` → `image/serializer/Default.cs` → base64. The browser gets a value it can decode.

Same Image instance, same step in the same goal, two channels, two wire shapes. The value was never re-materialized, the format mapping never lived in the channel, and the goal author never had to know about text vs. web — just "write out the photo."

**Build resolves, runtime runs.** Every type the builder can decide, it bakes into the `.pr` — typed and value-native, as two separate fields `type` + `kind` (`set %x% = 3.5` lands as JSON `3.5` with `type:"number", kind:"decimal"`, not the string `"3.5"`; `read photo.jpg` stamps `type:"image", kind:"jpg"` — the `kind` set by the type's own `Build(value)` method). The runtime loads typed values and runs; it parses a string into a value only when the string is genuinely runtime-dynamic (a file's contents, an HTTP body, terminal input), never for a literal the builder already typed. The full build→`.pr`→runtime trace, the `type`+`kind`+`Build()` model, and why `%photo%` composes a `path` rather than union-typing it, are in [plan/build-vs-runtime.md](plan/build-vs-runtime.md).

## What's already there

The pattern is partial in the codebase today; the branch completes it on real foundations:

- **`app/types/path/`** — a multi-variant type owning verbs (`this.Operations`), authorization (`this.Authorize`), derivation (`this.Derivation`), and per-variant subfolders (`file/`, `http/`). The folder shape we generalize. Its `this.JsonConverter.cs` is the legacy single-format converter; it folds into `path/serializer/Default.cs`.
- **`app/types/Registry.cs`** — `[PlangType]` discovery already scans assemblies and builds the name↔type index; `RegisterRuntime(name, type)` is the runtime hook; `ResolveType` already favors runtime registrations over built-in. The registry is real, not new.
- **`app/data/IBooleanResolvable.cs`** — `Data.ToBooleanAsync` dispatches to the value when it implements the marker (canonical implementer: `path`). The same dispatch-on-tag shape the serializer hook uses.
- **`static Resolve(string, context)`** — the existing factory convention; `app.types.Conversion.TryConvertTo` dispatches to it.
- **`code.load`** (`PLang/app/modules/code/load.cs`) — load-scan-register a DLL for `ICode` providers; the template the runtime type-loading feature follows.

Missing, and built here: the per-(type, format) serializer files + the `TypeSerializers` dispatch table + the `Normalize` tag-hook; folding the flat `Primitives` table into the registry; and the proving instances below.

## Vocabulary — what ships this branch

Three proving instances, each exercising a distinct leaf-access pattern:

- **`number`** — tagged-union value (`int`/`long`/`decimal`/`double`), leaf-action-heavy (`math.*`), uniform across formats (every writer can emit a numeric primitive). Deep dives: [plan/storage.md](plan/storage.md) (the value type) and [plan/policy.md](plan/policy.md) (arithmetic policy).
- **`image`** — `byte[]` + mime, the format-asymmetric case (base64 vs raw bytes vs path placeholder) and construction-asymmetric (file read vs URL fetch vs base64 decode). The hardest proof for the dispatch.
- **`code`** — string + language tag, text-shaped but content-aware (plain string in most formats; `<pre><code>` wrap once an HTML writer exists).

Plus four mechanical cleanups that retire half-states: `datetime` → `DateTimeOffset` (DateTime banished), `date` → `DateOnly`, `time` → `TimeOnly`, `duration` → `TimeSpan` (a clean name for the old `time`/`timespan` overload). `datetime` and `duration` get folders (parse/format complexity worth owning); `date` and `time` stay table-only.

Deferred to later branches, by design — each needs a driving action surface before the type earns its place:
- **`video`, `audio`, `document`, `archive`, `font`, `executable`** — structurally identical to `image`; lift them when there's a `video.thumbnail` / `document.extract-text` / `archive.list` to consume them.
- **`BigInteger` / arbitrary precision** — a fifth `NumberKind` slot the umbrella is designed to absorb; add it when a real arithmetic consumer needs >28 digits ([plan/storage.md](plan/storage.md)).
- **`rational`** (`- set %x% = 7/8` kept exact) — a *separate sibling type* under `app/types/rational/`, not a `NumberKind` (its arithmetic rules differ: GCD, lowest-terms, no IEEE).
- **`quantity`** (`5kg`, `30m/s`) — number + unit, converting by fixed ratios. The sibling-type shape `rational` also follows.
- **`money`** — number + currency code + locale formatting. Adjacent to `quantity` but distinct: currencies convert by *external, time-varying rates*, so it drags in a rate-provider subsystem. It's the natural home for "always 2 decimals" — currency wants it, so `money` defaults to it rather than taxing every `number`. Needs a rate source and an action surface before it earns a folder.

Number itself stays **lossless and arithmetic-pure**: division and inexact ops keep full precision (no default rounding — `1/1000000` must not silently become `0`), and the wire formats round-trip exactly. **Human-facing formatting** — decimal places shown on print, decimal/thousands separators, currency symbols — is a single locale-tied concern deferred to the culture/formatting pass (`app.Culture`). In-branch, `number` renders shortest-round-trip invariant; `number/serializer/text.cs` (the human path, already separate from the lossless `Default.cs`) is where a display-decimals setting lands when that pass ships. We don't split decimal-count off from the rest of formatting now.

Per-type detail, ownership matrix, and registration shape: [plan/types.md](plan/types.md).

## Extending the vocabulary at runtime

The built-in set is the *starting* vocabulary, not a closed one. A developer adds or overrides types by loading a DLL, and the infrastructure already exists:

```
- load mynumbers.dll
```

`code.load` already does load → scan-for-interface → register for `ICode` providers. Type-loading is the same shape pointed at types:

1. **Add a type** — scan the DLL for `[PlangType]` classes → `Registry.RegisterRuntime(name, clrType)`. Resolvable by name everywhere after.
2. **Wire its renderings** — the DLL ships one `ITypeRenderer` per format; the loader registers them into the same `TypeSerializers` table the generator feeds. (A loaded type can't be generator-wired — the generator already ran.)
3. **Overwrite a built-in** (redefine `int`) — works at the resolution layer for free, because `ResolveType` checks runtime registrations before the built-in table.

**The honest limit.** Runtime registration changes *resolution and rendering* — what a name maps to, how a value serializes. It cannot rewrite what the source generator already baked at build: PLNG slot validation, the `Data<int>` slots on compiled handlers, the type stamps in shipped `.pr` files. So `- load myint.dll` changes `int` going forward, but a handler compiled against the built-in `int` still sees the built-in at its typed slot. **Adding** types is unconstrained; **overwriting** built-ins is "new resolution + rendering, same compiled slots." Detail in [plan/dispatch.md](plan/dispatch.md).

## Folder tree after the work

Delta from today, unrelated trees omitted:

```
PLang/
  app/
    data/
      this.cs                  (existing) — Data, the courier (Value is object → boxes value types)
      this.Normalize.cs        (modified) — tag registered types as TypedValueNode; reflect the rest as today
      Wire.cs                  (existing) — value slot already routes through Normalize + writer
      TypedValueNode.cs        (new) — sealed record (object Value, string TypeName); the deferred marker
      IBooleanResolvable.cs    (existing)
    types/
      this.cs                  (modified) — Primitives table folds into the registry
      Conversion.cs            (modified) — dispatches through registry; + Resolve(byte[]) branch
      Registry.cs              (existing) — [PlangType] discovery + RegisterRuntime already present
      PlangTypeAttribute.cs    (existing)
      TypeSerializers.cs       (new) — (typeName, formatToken) → Write delegate; generated + runtime-registered
      ITypeRenderer.cs         (new) — interface a loaded DLL implements per format (mirrors ICode)
      path/                    (existing) — gains serializer/, sheds this.JsonConverter
        this.cs  this.Authorize.cs  this.Operations.cs  this.Derivation.cs
        file/  http/  scheme/  permission/
        serializer/Default.cs  (new) — writer.String(Relative); absorbs this.JsonConverter
      number/                  (new — sealed class @this, immutable value)
        this.cs                  NumberKind enum, storage slots, IEquatable, IBooleanResolvable
        this.Parse.cs            Parse / TryParse / Resolve(string,ctx) / Build(value)→kind
        this.Operators.cs        + - * / % == != (lenient default)
        this.Arithmetic.cs       Add/Sub/Mul/Div/Mod/Pow (policy-aware, Data-returning)
        this.Equality.cs         lenient Equals + ExactEquals + canonical GetHashCode
        Config.cs                : IConfig — OverflowMode, PrecisionMode
        NumberPolicy.cs          the resolved struct passed into Arithmetic
        serializer/Default.cs    (number, *) → writer.Int/Long/Decimal/Double
      image/                   (new)
        this.cs                  Bytes, Mime, Path(path, nullable), IBooleanResolvable
        this.Parse.cs            Resolve(string) path/data-url/base64; Resolve(byte[]); Build("a.jpg")→"jpg"
        serializer/
          text.cs                (image, text)     → path placeholder
          protobuf.cs            (image, protobuf) → raw bytes (when that writer ships)
          Default.cs             (image, *)        → base64 (covers json + plang)
      code/                    (new)
        this.cs                  Source, Language, IBooleanResolvable
        this.Parse.cs            Resolve(string) with language detection
        serializer/Default.cs    (code, *) → writer.String(source)   (html.cs later)
      datetime/                (new) this.cs wraps DateTimeOffset; this.Parse.cs ISO-8601 tz-aware
      duration/                (new) this.cs wraps TimeSpan; this.Parse.cs 1.02:03:04 + ISO-8601
      (date, time, string, int, long, decimal, bool, bytes, guid, …) — table-only in app/types/this.cs
    modules/
      math/                    (existing — actions grow Config + use NumberPolicy)
        add.cs                 (modified) — returns Data<number>; reads config via app.config.For<number.Config>
        … subtract/multiply/divide/modulo/abs/ceiling/floor/round/max/min/power/sqrt/random
        intdiv.cs              (new) — truncating integer division (opt-in; see storage.md)
      file/
        read.cs                (modified) — Build() stamps Data.Type from extension via registry
        write.cs               (modified) — accepts Data<number|image|code|…> uniformly
      output/write.cs          (existing) — already polymorphic; unchanged
      code/load.cs             (modified) — also registers [PlangType] classes + ITypeRenderers from the DLL
    channels/serializers/
      IWriter.cs               (modified) — gains a Format token property ("json"/"plang"/"text"/…)
      this.cs                  (existing) — registry stays
      serializer/json.cs, plang/this.cs, Text.cs  (modified — TypedValueNode case in Value dispatch)
      filters/                 (existing, untouched)
  Generators/
    Discovery/this.cs          (modified) — scans [PlangType] + serializer/*.cs
    Emission/                  (modified) — emits the TypeSerializers (type, format) table
    PLNG_SerializerCoverage.cs (new) — gate: each [PlangType] has Default.cs or covers every format token
```

Every PLang type is a folder under `app/types/`: the value (`this.cs`), the parse-in factory (`this.Parse.cs`), an optional `Config.cs` (when the type has policy axes), and a `serializer/` subfolder. Module action folders consume types via typed slots and carry no per-type-format knowledge. The leaf file shapes may shift during stage-carving; the topology is what to expect.

## Cross-cutting decisions

- **`type` + `kind`, as separate fields.** Every value carries a high-level `type` (the routing key) and an optional `kind` refinement, stored as a **separate `.pr` field** (never a `type:kind` string — splitting it would be runtime work). The `kind` is set at build by the type's own `Build(value)` method — `number.Build(3.5)→decimal`, `image.Build("a.jpg")→jpg`, `path.Build("https://…")→http` — the build-time sibling of `Resolve`. So **`int`/`decimal`/`double` are kinds of `number`, `jpg`/`png` kinds of `image`**: number isn't special. The LLM is shown a type's kinds only when they're developer-meaningful (number's precision); otherwise `Build()` derives the kind silently and the LLM never picks it. Full trace in [plan/build-vs-runtime.md](plan/build-vs-runtime.md).
- **Multi-faceted values compose; they don't union.** A file-backed `image` carries a `Path` property of type `path` (nullable — base64-decoded images have none); `%photo.Path.Exists%` navigates to the path's own members. No `path|image` union (multiple-inheritance-dangerous: ambiguous action slot, ambiguous serializer). The type catalog is typed-property (`image(path) => …, Path(path)`) so the LLM can navigate the chain. Routing key stays single (`image`).
- **Channel never branches on type; type never knows about channels.** The bridge is the writer's `Format` token. Adding a channel/writer doesn't force every type to grow a renderer; adding a type doesn't force every writer to change.
- **Unregistered types fall back to reflection.** A value whose CLR type isn't a `[PlangType]` is reflected into a property bag exactly as today. Only registered types are tagged and dispatched to serializer files. Identity, Signature, user records — untouched, backwards-compatible.
- **The registry subsumes the flat `Primitives` table.** The dictionary at `app/types/this.cs:34` folds into the `[PlangType]` registry. `app.formats` becomes the extension→name helper that the parse-in side uses to stamp `Data.Type`, not a parallel universe.
- **`number` is a `sealed class`** named `@this` — a *value* semantically (immutable, value equality) but a class for codebase consistency (every other `app/types/` entry is a class; a struct's only win is C#-internal allocation that mostly boxes away when stored in `Data.Value`, so it's not worth the one-off shape this early — reversible later). Lenient-by-default equality; error model throws inside C# but returns `Data.Error` at the handler boundary. [plan/storage.md](plan/storage.md).
- **Arithmetic policy reuses `app.config`** (`number.Config : IConfig`) — context→parent→app-default walk, no `Goal`-stored state, no ambient `AsyncLocal`. [plan/policy.md](plan/policy.md).
- **`/` and `^` promote out of the integer kinds.** `7 / 2 → 3.5`, not integer-divide `3` — the integer-division footgun is the wrong default for a non-programmer audience. Truncating division is the explicit `math.intdiv`. (Architect's call; flag on read-over if you'd rather keep C# integer semantics.)
- **HTML deferred.** No HTML writer ships yet (`text/html` aliases JSON today); the `<img>`/`<pre>` markup renderings are footnotes until one does.

## Stages (to carve next)

The design is settled; these are the imperative units of work, in dependency order. Stage files come after your read-over.

1. **Registry + dispatch spine.** Fold the `Primitives` table into the `[PlangType]` registry; add `TypeSerializers`, `TypedValueNode`, the `Normalize` tag-hook, `IWriter.Format`, the writer's `TypedValueNode` case, the `PLNG_SerializerCoverage` gate, the separate **`kind` field** on `.pr` parameters, and the per-type **`Build(value)→kind`** hook (with the typed-property catalog so the LLM can navigate `.Path` etc.). No new types yet — `path` adopts `serializer/Default.cs` as the first mover and proves the path end-to-end.
2. **`number` the value type.** The `sealed class`, storage, parse, operators, lenient/exact equality, `IBooleanResolvable`, `serializer/Default.cs`. [storage.md](storage.md).
3. **`number` arithmetic + policy.** `NumberPolicy`, `number.Config : IConfig`, the `app.config` resolver, the `math.*` retype to `Data<number>`, `math.intdiv`. [policy.md](policy.md).
4. **`image` + `code`.** The two non-numeric proving instances, their parse/serializer files, `file.read` type-stamping.
5. **The cleanups.** `datetime`/`date`/`time`/`duration` rebinds and the two folders.
6. **Runtime loading.** `code.load` extension for `[PlangType]` + `ITypeRenderer` registration, and the overwrite-precedence wiring.

Test strategy and coverage are written once the stages are carved (`plan/test-strategy.md`, `plan/test-coverage.md`).
