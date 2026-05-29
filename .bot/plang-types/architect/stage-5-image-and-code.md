# Stage 5: `image` + `code` — the non-numeric proving instances

**Goal:** Prove the dispatch across the two remaining shape categories — binary-asymmetric (`image`) and text-semantic (`code`) — including a composed `path` facet and `file.read` resolving an extension to a high-level type.
**Scope.** *Included:* `app/types/image/` and `app/types/code/` (value, `Resolve`, `Build`, serializer files), `file.read.Build()` resolving extension → high-level return type, `file.read.Run()` constructing an `image`/`text` value. *Excluded:* `image.*` / `code.*` leaf-action modules (resize, run, …) — follow-up branches; an HTML writer (so `code`/`image` HTML renderings stay deferred).
**Deliverables (per [plan/types.md](plan/types.md)):**
- `app/types/image/` — `this.cs` (`Bytes`, `Mime`, `Path` (type `path`, **nullable**), lazy `Width`/`Height`, `IBooleanResolvable` = bytes.Length>0); `this.Parse.cs` (`Resolve(string)` path/data-url/base64, `Resolve(byte[])`); `this.Build.cs` (`"a.jpg"→"jpg"`); `serializer/text.cs` (path placeholder), `serializer/protobuf.cs` (raw bytes — stub until that writer ships), `serializer/Default.cs` (base64). `[PlangType("image")]`.
- `app/types/code/` — `this.cs` (`Source`, `Language`, `IBooleanResolvable` = source non-empty); `this.Parse.cs` (`Resolve(string)`); `this.Build.cs` (language detect → `"csharp"`/… or `"text"`); `serializer/Default.cs` (`writer.String(Source)`). `[PlangType("code")]`.
- `file/read.cs` — `Build()` (action hook) changes from "stamp the extension *as* the type" to "resolve extension → high-level type (image/text/…) via the registry/formats"; the type's own `Build()` supplies the kind. `Run()` constructs the typed value (an `image` for image MIMEs) rather than returning raw bytes/string.
**Dependencies:** Stages 1–2 (registry, kind, Build, dispatch). Follows the patterns Stage 3 set on `number`.

## Design

> **You own the code.** [plan/types.md](plan/types.md) + [plan/dispatch.md](plan/dispatch.md) hold the per-type detail; intent, not dictation.

**`image` is the hardest proof** — one instance, genuinely different wire shapes: `serializer/text.cs` → a path placeholder, `protobuf.cs` → raw bytes, `Default.cs` → base64 (covers json + plang). If the per-file dispatch works for image, every future binary type (video/audio/document) slots in by analogy. The `protobuf.cs` can be a thin stub returning bytes until a protobuf writer exists — its presence proves the (type, format) table handles a non-string primitive.

**The `Path` facet is composition, not union.** `image.Path` is a `path` property (nullable — a base64-decoded image has no source). `%photo.Path.Exists%` navigates via the typed-property catalog from Stage 1; there is no `path|image` union. Routing key / serializer stay `image`. (See [plan/build-vs-runtime.md](plan/build-vs-runtime.md) "composition, not union".)

**`file.read` is where the action `Build()` meets the type `Build()`.** Today `file.read.Build()` stamps the *extension* as the type (`"jpg"`), gated on whether that extension is a registered type (`read.cs:46`). Under the model it resolves the extension to the **high-level type** (`.jpg`→`image`, `.txt`→`text`) via the registry/formats, and the type's `image.Build()` supplies the **kind** (`jpg`). At `Run()`, construct the typed value (an `image.@this(bytes, mime)`), not raw bytes — so `%photo%` is an `image`, and the build-stamped type/kind match what `Run()` produces.

**`code` proves the text-semantic third pattern** — mostly a uniform string render (`Default.cs`), with HTML wrapping deferred until an HTML writer exists. `code` ships as a vocabulary entry (the LLM can pick `code` over `string` for snippets); its leaf actions (`code.run`/`validate`/`format`) are follow-ups.
