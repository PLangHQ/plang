# Build vs. runtime — where types are decided, and who pays

The guiding principle, in one line: **build resolves, runtime runs.** The builder (LLM + `Build()` hooks) decides every type it can at compile time and bakes it — typed and value-native — into the `.pr`. The runtime loads the `.pr` and executes; it does **not** re-derive types or re-parse literal strings on each execution. The only place the runtime parses a string into a typed value is when the string is *genuinely runtime-dynamic* (a file's contents, an HTTP body, terminal input) — never for something the builder already knew.

This file traces a goal through both phases so the split is concrete, and answers the two questions on the spine's "movie" (`plan.md` L47): does `read photo.jpg` stamp a mime at build, and is `set %x% = 3.5` typed `decimal` or `number`?

## The `.pr` already stores values typed and native

This isn't aspirational — it's what the compiler emits today. For `set %total% = 0` and `set %items% = [1, 2, 3]`, the `.pr` records:

```jsonc
{ "name": "Value", "value": 0,         "type": "int"  }   // JSON number 0, not the string "0"
{ "name": "Value", "value": [1, 2, 3], "type": "list" }   // JSON array, not the string "[1,2,3]"
```

The `value` is a JSON-native token; the `type` is the resolved PLang type name. The runtime loads `0` as a number and `[1,2,3]` as a list — no `int.Parse("0")`, no list-string parsing. **The string was parsed once, at build, by the LLM/builder. The result is baked.** Everything below is the generalization of this rule to the new type vocabulary.

## The two phases, side by side

```
GOAL (.goal)                BUILD  (once)                        .pr (artifact)                  RUNTIME (every run)
─────────────────────────   ──────────────────────────────────  ──────────────────────────────  ────────────────────────────────
- set %x% = 3.5             LLM: type number. number.Build(3.5)  { value: 3.5,                   load JSON 3.5 (already numeric)
                            → kind "decimal" (decimal point).     type:"number", kind:"decimal" } → number{Decimal, 3.5m}. NO parse.
                            scope now: %x%(number) kind=decimal

- read photo.jpg,           LLM: picks file.read. file.read       step: file.read                 file.read.Run() reads bytes,
  write to %photo%          .Build() reads ext → return           { Path: {value:"photo.jpg",     builds image{bytes, mime}.
                            type "image"; image.Build("a.jpg")    type:"path"} }                  Type/kind already baked at
                            → kind "jpg". (Run() is bare Data,     return type:"image", kind:"jpg" build — runtime just constructs.
                            polymorphic — type isn't static.)
                            scope now: %photo%(image) kind=jpg

- set %y% = %x% + 1         LLM: sees %x%(number), 1 is int.      step: math.add(%x%, 1)          math.add(3.5m, 1) → number{Decimal}.
                            picks math.add → Data<number>.        then variable.set %y%           promotion in C#, not parsing.
                            scope: %y%(number), kind runtime →    (no kind: decided at runtime)   kind set by the result.
                            absent in .pr

- write out %photo%         LLM: picks output.write (polymorphic  step: output.write(%photo%)     channel + writer.Format → dispatch
                            Data, no type awareness needed)                                       (image, Format) → serializer file
```

Read the columns as a pipeline: the **build** column is where every "what type is this, what kind?" question is answered (the LLM picks the high-level `type`; the type's `Build(value)` sets the `kind`). By the time the `.pr` exists, the runtime never asks again — it loads a baked `{type, kind, value}` or runs a typed C# action that produces a `Data` already tagged. The one case where `kind` is absent is a polymorphic result (`math.add`) whose kind is genuinely decided at runtime by promotion.

## Answering the two questions — one rule, no special case

Both questions resolve to a single rule, so number stops being special:

> **Every type is a high-level type plus an optional `kind` refinement. `type` and `kind` are separate `.pr` fields. The `kind` is set at build by the type's own `Build(value)` method whenever the value determines it.**

`Build(value)` is the build-time sibling of `Resolve(value, context)` (runtime construction): each type owns how it reads a kind off a value, the same way it owns how it constructs one. `number.Build(3.5) → decimal`; `image.Build("photo.jpg") → jpg`; `path.Build("https://…") → http`. The `.pr` stores `type` and `kind` as **separate fields** — never a `type:kind` string, because splitting a string is runtime work and the entire principle is that the runtime does none.

### `- set %x% = 3.5`

```jsonc
{ "name": "Value", "value": 3.5, "type": "number", "kind": "decimal" }
```

The type is `number`; `decimal` is its **kind**, set by `number.Build(3.5)` (decimal point → decimal; `e`/exponent → double; no point → int/long). So **`int` / `decimal` / `double` / `long` are kinds of `number`, not separate top-level types** — `decimal` is to `number` exactly what `jpg` is to `image`. JSON has no decimal-vs-double distinction (`3.5` is just a JSON number), so the `kind` field is what disambiguates: `(number, decimal)` loads as `number{Decimal}`, `(number, double)` as `number{Double}` — no `decimal.Parse("3.5")`, ever.

The LLM **is** shown number's kinds (int/decimal/double/long), because precision is developer-meaningful — `%x% == %y%` and arithmetic reasoning depend on it. A polymorphic result like `math.add` is `{ "type": "number" }` with **no kind** — the kind is decided at runtime by promotion, so the field is simply absent until then.

### `- read photo.jpg, write to %photo%`

```jsonc
// the step's return → %photo%
{ "type": "image", "kind": "jpg" }
```

`file.read.Run()` returns a **bare `Data`** — it's polymorphic by MIME (text→string, image→image, json→structured), so its return type is *not* a static `Data<image>`. The type is determined at build by **`file.read.Build()`** (the action hook, `IClass.Build()`): it peeks the literal path, reads the extension, and resolves it through the registry to the high-level return type `image`. The **kind** (`jpg`) comes from the type's own `image.Build("photo.jpg")` reading the extension (no dot). The LLM is **not** shown image's kinds — it doesn't pick them; the type's `Build()` derives `jpg` silently. So the difference from number isn't a different *rule* — it's the same rule, with the kind derived by the type instead of advertised to the LLM. (At runtime `file.read` may confirm or correct the kind from the actual bytes; the build-time kind is the extension's claim.)

> **Two `Build`s, don't confuse them.** The **action** `IClass.Build()` (compile-time hook on a handler — `file.read.Build()`) decides *that action's return type* when it isn't static. The **type** `Build(value)` (on `app/types/<name>/this.cs`) decides *a value's kind*. They cooperate: for a literal (`set %x% = 3.5`) the builder calls the type's `number.Build(3.5)` directly (no action involved); for an action with a dynamic return (`file.read`) the action's `Build()` picks the high-level type and the type's `Build()` supplies the kind. For an action with a static return (`math.add → Data<number>`) the type is read straight off the signature and the kind is left for runtime. (Naming note: both are "Build" because both are build-time determination; if the collision bites during implementation, the type method can become `KindOf(value)` — flagging it, not blocking on it.)

### `%photo%` is one type with a path facet — composition, not union

`%photo%` answers both `%photo.Exif%` (image data) and `%photo.Path.Exists%` (its source file), but it is **one** `image`, never a `path|image` union — unions are multiple-inheritance-dangerous (which slot does `copy %photo%` target? which serializer fires?). Instead the image carries a `Path` **property** of type `path`. The LLM navigates it because the type catalog is typed-property:

```
path(string)  => Exists, Size, Extension, …
image(path)   => Exif, Width, Height, Path(path)
```

`Path(path)` tells the LLM that member is itself a `path`, so `%photo.Path.Exists%` resolves cleanly down the chain. `Path` is **nullable** — an image decoded from base64 has no source file, so `%photo.Path%` is null there (`.Exists` on null is a typed null, not a crash). Routing key and serialization stay `image`. (The `(path)` after `image` in the catalog is what the type *resolves from* — `image.Resolve(path)` — doubling as "image has a path"; it's distinct from the per-value `kind`.)

## The rule for the coder

1. **`type` + `kind` are separate baked fields; neither is re-parsed at runtime.** Literals land value-native (`3.5` as JSON `3.5`, not `"3.5"`); `type` comes from the producing action (its static `Data<T>` signature, or its `Build()` hook when the return is dynamic like `file.read`), `kind` from the type's `Build(value)`. `Resolve`/`Parse` of a *string* runs only for genuinely runtime-dynamic input — `file.read` of a text file, an HTTP body, terminal input, a `%var%` that resolved to a string — never for a literal the builder already typed.

2. **Each type owns two siblings: `Build(value) → kind` (build-time) and `Resolve(value, context) → @this` (runtime).** The type decides its own kind and its own construction; the runtime just carries `{type, kind, value}` and only the leaf reaches in. That's the [OBP Rule #9](../../../Documentation/v0.2/object_pattern_formal.md) courier principle: nobody re-opens the package to re-derive what build already stamped.

Net: "here is the `.pr`, now run it." Build did the thinking — including which kind — and stored it in a field, not a string to split.
