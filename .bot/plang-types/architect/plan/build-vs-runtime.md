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
GOAL (.goal)                BUILD  (once)                        .pr (artifact)               RUNTIME (every run)
─────────────────────────   ──────────────────────────────────  ───────────────────────────  ────────────────────────────────
- set %x% = 3.5             LLM: decimal-point literal → decimal { value: 3.5, type:           load JSON 3.5 (already numeric)
                            bakes value as JSON number 3.5        "decimal" }                   → number{Decimal, 3.5m}. NO parse.
                            scope now: %x%(decimal)

- read photo.jpg,           LLM: picks file.read.                 step: file.read              file.read.Run() reads bytes,
  write to %photo%          file.read's signature is              { Path: { value:"photo.jpg", builds image{bytes, mime:
                            Data<image> → return type = image.    type:"path" } }              "image/jpeg"} — mime derived
                            ext .jpg → image via registry.        return type image (from      HERE, from the file. Data.Type
                            scope now: %photo%(image)             the Data<image> signature)   = "image" comes from the signature.

- set %y% = %x% + 1         LLM: sees %x%(decimal), 1 is int.     step: math.add(%x%, 1)       math.add(3.5m, 1) → number{Decimal}.
                            picks math.add → Data<number>.        then variable.set %y%        promotion happens in C#, not parsing.
                            scope now: %y%(number)

- write out %photo%         LLM: picks output.write (polymorphic  step: output.write(%photo%)  channel + writer.Format → dispatch
                            Data, no type awareness needed)                                    (image, Format) → serializer file
```

Read the columns as a pipeline: the **build** column is where every "what type is this?" question is answered. By the time the `.pr` exists, the runtime never asks that question again — it either loads a baked value or runs a typed C# action that produces a `Data` already tagged by its signature.

## Answering the two questions

### `- read photo.jpg, write to %photo%` → type `image`; mime is *not* in the `.pr`

The builder stamps two things:
- The **input** parameter: `{ Path: { value: "photo.jpg", type: "path" } }`.
- The **return type** for `%photo%`: `image`. It doesn't sniff the file — it reads `file.read`'s C# signature (`Task<Data<image>>` once the extension resolves to `image`) via `Modules.Describe()`. Scope for the next step shows `%photo%(image)`.

The **mime (`image/jpeg`) is deliberately not baked**, for three reasons:
1. The LLM vocabulary is the bare type (`image`), not the subtype (`image/jpeg`) — that's a settled cross-cutting decision. Baking a subtype the LLM never sees would be dead weight in the `.pr`.
2. The file isn't read at build. Its real content type is only known when `file.read` actually opens it.
3. Mime is a property of the *value*, not the *plan*. At runtime `file.read.Run()` constructs `image.@this(bytes, mime)`, deriving the mime from the extension (or a content sniff) at that one moment. The `image` instance carries `Mime`; the `.pr` carries only `Type = image`.

So: **`.pr` → `image`** (the routing key, baked at build, drives LLM scope and serializer dispatch). **value → `image/jpeg`** (a runtime property, set when the bytes are read). Deriving mime from an extension at runtime is a dictionary lookup, not the "parse a string on every execution" cost the principle is about — and it happens at the unavoidable moment we're touching the file anyway.

### `- set %x% = 3.5` → type `decimal` (concrete), not `number`, not `number/decimal`

`3.5` is an unambiguous decimal literal, so the builder stamps the **concrete kind**:

```jsonc
{ "name": "Value", "value": 3.5, "type": "decimal" }
```

- Not `number`. `number` is the umbrella for the case where the kind is *only knowable at runtime* — `math.add(int, decimal)` can't say which kind it returns until it runs, so its signature is `Data<number>`. A literal's kind is known at build, so we bake the precise kind. Stamping `number` here would *throw away* information the builder already has.
- Not `number/decimal`. We don't use hierarchical subtype tags in the LLM-facing vocabulary (same decision as bare `image` over `image/png`). The kind *is* the type name here: `decimal`.

JSON has no decimal-vs-double distinction — `3.5` is just a JSON number. The `type: "decimal"` field is what disambiguates: `(3.5, decimal)` loads as `number{Decimal}`, `(3.5, double)` would load as `number{Double}`. That's exactly why the literal-shape rule matters at build (decimal-point → `decimal`, exponent/`e` → `double`): it sets the `type` field once, and the JSON number carries the magnitude. **The runtime reads `(3.5, decimal)` and wraps it into a `number` — no `decimal.Parse("3.5")`, ever.**

The clean rule that falls out: **literals carry their concrete kind (`int`/`decimal`/`double`); `number` appears only as the declared return of a polymorphic action.** A literal is never `number` — its kind is always decided at build.

## The rule for the coder

Two invariants keep the runtime cheap:

1. **Literals are baked value-native + concrete-kind, never as a string to re-parse.** A number literal lands in the `.pr` as a JSON number plus a `type` of `int`/`decimal`/`double` (per the literal-shape rule). `number.Parse(string)` exists for *runtime-dynamic* strings only — `file.read` of a text file, an HTTP body, terminal input, a `%var%` that resolved to a string. It is never on the hot path for a literal the builder already typed.

2. **A value's PLang type comes from the action's typed signature, not from runtime inspection.** `file.read`'s `Data<image>` makes the result `Type = image` for free; `math.add`'s `Data<number>` makes its result `Type = number`. The runtime never reflects over a value to ask "what are you?" — the producing action's signature already said so, and that flowed into both the `.pr` (next-step scope at build) and the `Data.Type` tag (at runtime). This is the [OBP Rule #9](../../../Documentation/v0.2/object_pattern_formal.md) courier principle in action: the type tag rides on the package; nobody re-opens it to re-derive it.

Net: "here is the `.pr`, now run it" is the right mental model. Build did the thinking; runtime does the doing.
