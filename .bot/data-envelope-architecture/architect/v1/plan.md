# Data Envelope Architecture — Design Spec

## Why

PLang App needs Data to be self-describing at IO boundaries — wrapping, compression, encryption as transparent layers. This requires Type to know its Kind, and Kind to drive pipeline decisions. Today type knowledge is scattered across three static/instance lookups with no object ownership. Data itself lacks context, preventing it from navigating to the services it needs.

---

## Phase 1: Engine.Types

**Goal:** Replace static `TypeMapping` and `FileModule.TypeMapping` with `Engine.Types` — a live instance on the engine that owns all type knowledge.

**What Engine.Types owns:**
- PLang name ↔ CLR type (absorbed from static TypeMapping)
- Extension → Kind ("image", "spreadsheet", "text", "encrypted", "archived", ...)
- Extension → MIME content type
- Kind → compressible (derived: image/video/audio/archive are not)
- Kind → processing layer (encrypted, archived are processing, not content)
- Add/Remove for runtime extensibility

**What moves:**
- `PLang/App/Utility/TypeMapping.cs` static dictionaries → `Engine.Types` instance
- `PLang/Modules/FileModule/TypeMapping.cs` category + MIME data → `Engine.Types`
- Pure logic (`ConvertTo`, `IsPrimitive`) is stateless — can stay as static helpers or move, coder's call

**What dies (eventually):**
- `Utils/MimeTypeHelper.cs` — third copy of MIME data, redundant
- FileModule's TypeMapping instance — delegates to Engine.Types

**Key decisions:**
- Types is an instance on Engine, not static — allows runtime Add/Remove
- Fix `.key` extension conflict (both "presentation" and "certificate" in old code — keep "certificate")

**Source files to read:**
- `PLang/App/Utility/TypeMapping.cs` — current static type mapping (absorb into Types)
- `PLang/Modules/FileModule/TypeMapping.cs` — category + MIME tables (absorb data)
- `PLang/Utils/MimeTypeHelper.cs` — MIME data (redundant, dies)
- `PLang/App/this.cs` — Engine root, where Types property goes

---

## Phase 2: Type gets context + lazy derivation

**Goal:** Type becomes a navigator. It lazily reaches Engine.Types through context. Data gets context and stops eagerly creating Types.

### Type changes

- Type gets a `PLangContext` reference (cheap pointer)
- New lazy properties that navigate through context:
  - `.Kind` → `Context.Types.Kind(Value)` — "image", "spreadsheet", "encrypted", ...
  - `.Compressible` → `Context.Types.Compressible(Kind)`
  - `.ClrType` → `Context.Types.ClrType(Value)`
- Everything lazy — zero cost until accessed
- Type used in static/test contexts without context — null-safe (Kind returns null if no context)
- Serialization unchanged — Type still serializes as plain string via TypeJsonConverter
- Static convenience properties (`Type.String`, `Type.Int`, etc.) survive — they're contextless, Kind/Compressible/ClrType just return null on them

### Data gets context (late-bound)

Context is a settable property on Data. Stamped by whoever creates or receives Data and has context:

- **Variables.Set** — stamps context (Variables has it)
- **CodeGeneratedExecuteAsync** — stamps context (receives it as parameter)
- **Action.RunAsync** — stamps context on result Data before return variable binding
- **Data.GetChild** — child inherits context from parent
- **DynamicData** — created by Variables/PLangContext, gets context there
- **Data.Ok() / Data.FromError()** — creates contextless Data (static factories survive unchanged, context stamped later by pipeline)

### Type derivation becomes lazy

Today: Value setter eagerly creates `new Type(TypeMapping.GetTypeName(...))` on every set.

New behavior:
- Constructor: `_type = type;` — just stores explicit type if given, null otherwise
- Value setter: `_type = null;` — invalidates cached type, doesn't re-derive
- Type getter: lazily derives from Value's CLR type through context when accessed

Three states:
- **Explicit** — someone passed a Type, respected as-is
- **Derived** — no explicit Type, lazily derived from Value's CLR type through context
- **None** — no explicit Type, Value is null, Type is null

**Source files to read:**
- `PLang/App/Memory/Data.cs` — Data + Type + DynamicData (all three classes)
- `PLang/App/Memory/TypeJsonConverter.cs` — Type serialization (unchanged)
- `PLang/App/Context/` — PLangContext, where context is created
- `PLang/App/Memory/Variables` — where Data is stored and context is available

---

## Phase 3: Data restructure + Out view

**Goal:** Split Data into partial classes by concern. Add `Out` view for transport serialization.

### Partial class split

- **`Data.cs`** — core: Name, Value, Type, Path, Parent, Context, construction, CleanName, BuildPath, UnwrapJsonElement
- **`Data.Result.cs`** — result role: Error, Success, Handled, Warnings, Ok(), FromError(), implicit bool
- **`Data.Navigation.cs`** — GetChild, GetChildValue, ValueNavigators integration
- **`Data.Envelope.cs`** — transport role: Signature, Verified, Wrap, Compress, Encrypt, Decrypt, Decompress, Unwrap

Four files, one class. Each focused. Envelope surface only activates at IO boundaries.

### Out view

New serialization view for Data leaving the system:

```csharp
public enum View { Default, Store, LlmBuilder, Debug, Out }

[AttributeUsage(AttributeTargets.Property)]
public sealed class OutAttribute : Attribute { }
```

Properties tagged with `[Out]`:
- `Properties` — only serializes when Data is going out (envelope metadata like keyId, nonce)
- `Signature` — only relevant on the wire

Inside the runtime (Store, LlmBuilder, Debug, Default views), these stay invisible.

**Source files to read:**
- `PLang/App/View.cs` — existing View enum + attributes
- `PLang/App/Memory/Data.cs` — what goes where in the split

---

## Phase 4: Envelope pipeline

**Goal:** Data gains Wrap/Compress/Encrypt/Decrypt/Decompress/Unwrap — the two-Data envelope pattern.

### New properties on Data (in Data.Envelope.cs)

- `Signature: byte[]?` — `[Out]`, computed on serialization
- `Verified: bool?` — `[JsonIgnore]`, set on deserialization (true/false/null = verified/failed/unsigned)

### Pipeline methods

Each method navigates through context to action handlers. OBP — inspects itself, decides whether to act or pass through:

- `.Wrap()` — wraps content with Kind envelope from Type
- `.Compress()` — checks Type.Compressible, no-op if not compressible, otherwise calls compress through context
- `.Encrypt()` — calls encrypt through context, wraps in encrypted envelope
- `.Decrypt()`, `.Decompress()`, `.Unwrap()` — reverse pipeline

### The envelope pattern

```
Outer Data { type = "encrypted", value =
    Inner Data { type = "ed25519", value = encryptedBytes, Properties = [...] }
}
```

- Outer type = routing (which handler)
- Inner type + properties = handler-specific details
- Pipeline peels layers by checking Type.Kind — processing layers keep peeling, content layer stops

### Pipeline activation

- `data.Wrap().Compress().Encrypt()` on io.write — only for plang/data-aware receivers
- `data.Decrypt().Decompress().Unwrap()` on io.read
- Regular HTTP to browsers — no envelope, plain serialization

**Source files to read:**
- `DATA_ENVELOPE_ARCHITECTURE.md` in this folder — full design with examples
- `DataEnvelopePipelineExample.cs` in this folder — code examples for the pipeline

---

## Phase ordering

1 → 2 → 3 → 4. Each phase independently shippable and testable.

- Phase 1 is foundation — Engine.Types must exist before anything navigates to it
- Phase 2 depends on 1 — Type navigates to Engine.Types
- Phase 3 is independent of 2 but logical to do after (Data restructure before adding envelope surface)
- Phase 4 depends on 2 and 3 — envelope uses Type.Kind, Type.Compressible, Out view, context

---

## OBP rules for the coder

These were established during the design session. Violating them means the review will bounce it back.

1. **No verbs/prepositions in APIs** — no `Get`, `Resolve`, `From`, `Is`. Objects navigate to knowledge.
2. **Navigate through context** — object has context → context has engine → engine has the graph. No static lookups.
3. **Lazy navigation** — properties resolve on first access through the graph. Zero cost until asked.
4. **Static classes break OBP** — use instances on engine. `Engine.Types` not `static class TypeMapping`.
5. **Names are nouns, one word** — `Types` not `TypeMapping`. Same pattern as `Engine.Goals`, `Engine.Actions`.
6. **Factories are outside-in** — avoid `Type.FromX()`. Construct simply, discover lazily through navigation.

See `learnings/data-envelope-architecture/architect/v1/learnings.md` for full rationale.

---

## Open questions (deferred)

1. **Verification failure behavior** — reject tampered Data or pass through with Verified=false?
2. **Inner serialization format** — JSON vs binary for compressed/encrypted inner bytes
3. **Encryption key resolution** — handled by the encrypt action, not Data's concern
