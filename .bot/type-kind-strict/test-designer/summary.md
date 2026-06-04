# test-designer — type-kind-strict

## Version
v1

## What this is

Failing-test contract for the architect's 5-stage plan (type value model → text + name canonicalisation → kind derivation + canonicalisation → variable.set + strict → LLM representation). The bodies are `Assert.Fail("Not implemented")` / `- throw "not implemented"`; the test names + comments are the spec the coder fills in. The whole branch is net-new behaviour (Strict is brand new; `text` type is brand new; family-`Kind` removal + Data.Kind fold + ClrType internal are mechanical-but-semantic), so the strategy is ceiling-heavy: every new surface gets a pin, every failure-matrix row gets a negative test, every integration cut a dedicated test.

## What was done

User authorised end-to-end execution; v1 written without per-batch approval. The detailed batch plan lives at [`v1/plan.md`](v1/plan.md).

**C# (24 files / 107 tests)** under `PLang.Tests/App/TypeKindStrict/`:

| Folder | Files | Topic |
|---|---|---|
| `TypeValueModelTests/` | 7 | The `type` factory + entity shape after Value→Name + Kind/Strict; the `Data.Kind` fold; wire still two flat keys; `IKindValidatable` marker; `ClrType` reroute; `App.Type.Kinds`→`KindHooks` rename |
| `TextTypeTests/` | 2 | `text.Build` extension hook (null-safe, query-strip, lowercase); `text` shape (no static Kinds, Shape="string") |
| `PrimitiveTableTests/` | 1 | `Canonical[string]=text`; numerics map to `number`; `BuilderNames` includes `text`, excludes string/int/long/decimal/double |
| `KindDerivationTests/` | 5 | `number.Build` regression; kind canonicaliser (markdown→md, jpeg→jpg, unknown passthrough, derived-not-hand-written); `image.ValidateKind` byte-sniff (match, png-as-gif mismatch, garbage, empty); `Format.KindOf`→`FamilyOf` rename; numeric inference convergence (build stamp == runtime mint) |
| `SetAndStrictTests/` | 4 | `variable.set.Type` is `type` entity (not string); strict ValidateBuild paths (six scenarios); strict Run paths (three scenarios); mint-carries-kind regression (the dropped-kind bug guard) |
| `LlmRepresentationTests/` | 2 | `TypeSchemas` renderer (advertised kinds, extension-derived kinds, record/enum back-compat, `type` as constructor); `BuilderNames` catalog-generation |
| `IntegrationCutsTests/` | 3 | Cut 1: typed-set round-trips kind. Cut 2: strict mismatch fails at build for literal, at runtime for `%var%`, builds-and-runs-clean for matching literal. Cut 3: cached system prompt carries `number — kinds:`, `text — kind = extension`, `type(name, kind?, strict?)`; per-step user message drops the `Primitive types:` line |

**PLang `.test.goal` (10 files)** under `Tests/TypeKindStrict/`:

- `SetIntLiteralIsNumberInt` — `5` → `{number, int}`
- `SetAsTextWithMdExtension` — `"readme.md" as text` → `{text, md}` (Cut 1 spine in PLang form)
- `SetAsTextSlashMarkdownNormalises` — `as text/markdown` → kind canonicalised to `md`
- `SetAsImageGifStrictMatching` — `"real.gif" as image/gif strict` → builds + runs (needs real.gif fixture)
- `SetAsImageGifStrictMismatch` — `"photo.png" as image/gif strict` → build error (needs photo.png fixture)
- `SetAsImageGifStrictRuntimeVarMismatch` — `%upload% as image/gif strict` → runtime typed error
- `SetAsTextSlashMarkdownStrictUnverifiable` — text strict degrades to name-accepted (no byte probe)
- `SetAsTextDefaultNoValidation` — `as text` (no strict) builds clean
- `SetAsTextUppercase` — case-insensitive `as TEXT`
- `SetAsImageStrictNoKind` — strict without a kind builds clean (soft-degradation)

**Build:** `dotnet build PLang.Tests` — 0 errors, 347 pre-existing warnings (none introduced).

## Independent additions

16 rows beyond the architect's matrix, ~35% of the final count. Highlights:

- **Case insensitivity at the factory** — `type("Text")` and `as TEXT` round-trip to canonical `text`. Tests in `TypeFactoryTests.Factory_CaseInsensitiveName` and the `SetAsTextUppercase.test.goal`.
- **Empty Name rejected** — `type("")` throws. The LLM should never be able to emit `{"name":""}` silently.
- **Null-safety on `text.Build`** — mirrors image's existing null-safety; pinned explicitly so a regression at the stage-2 hook is caught.
- **Path-shaped string → extension hook** — `text.Build("../report.md")` → `"md"`. The hook is dumb extension extraction, no path-traversal worry at this layer.
- **`Data.Kind` setter contract pinned** — "writes through to `Type.Kind`" (the least surprising choice for callers used to the mutable field). The alternative (throws on direct set) is acceptable but the contract must be one or the other, not silently no-op.
- **Wire backward-compat** — legacy `.pr.json` with separate `type`/`kind` top-level keys deserialises (the fold is internal; the wire shape is unchanged).
- **Strict without a kind** — `as image strict` (no `/gif`) is **not** an error; nothing to validate against, soft degradation. The alternative — "strict requires a kind" — is a hard contract; whichever the coder picks, the test pins it.
- **Dispatcher rename is a no-op semantically** — `App.Type.KindHooks.Of(clrType, value)` returns the same string `App.Type.Kinds.Of(...)` did. Catches a sloppy rename that changes semantics.
- **`number` doesn't implement `IKindValidatable`** — negative-presence. Strict path on a number type must skip cleanly, not throw "not implemented".
- **Build stamp == runtime mint convergence** — `5` at build and the runtime mint of a CLR `int` both produce `{number, int}`. Architect lists this as design intent; it gets a direct test.
- **Cut 3 sibling: `type(name, kind?, strict?)` reaches the LLM** — trace-based assertion that the constructor teaching survives the prompt restructure, not just the kind vocabulary.

## Open items (coder, flip if needed)

1. **`Data.Kind` setter contract** — pinned to "writes through to `Type.Kind`". If the right answer is "throws on direct set", flip `DataKindFoldTests.Data_KindSetter_WritesThroughToTypeKind` into `Data_KindSetter_Throws`. One contract pinned, not silent no-op.
2. **`Factory_EmptyName_Rejected`** — pinned to "throws at factory". If a soft path exists (warn at build-validate, allow construction), demote the throw to a warn and add the build-validate test.
3. **`SetAsImageStrictNoKind`** — pinned to "build clean, no validation". The alternative is "build error: strict requires a kind"; flip the goal test + add a C# row if so.
4. **PLang fixture binaries** — `Tests/TypeKindStrict/` needs `real.gif` and `photo.png`. Convention: 1×1 transparent images committed alongside the `.test.goal`. Coder generates these when implementing.
5. **Cut 3 trace path** — assertions read from `.build/traces/<id>/`. If the implementation lands a different trace layout, adjust the path but keep the assertions.

## Code example

C# test signature (the contract for the coder):

```csharp
[Test] public async Task Factory_String_CanonicalisesNameToText()
{
    // type("string") → Name == "text". Aliases still accept "string" input;
    // canonical render is "text". Wires into primitive.Canonical change.
    Assert.Fail("Not implemented");
    await Task.CompletedTask;
}
```

PLang test goal:

```
Start
/ A bare integer literal binds Type to {name:number, kind:int} — the int
/ primitive is no longer a top-level name; it surfaces as a kind of number.
- set %x% = 5
- throw "not implemented"
- assert %x.Type.Name% equals "number"
- assert %x.Type.Kind% equals "int"
```

## Next

Run **coder** next — implement and make the tests pass. Stage order is strict per `architect/plan.md`: 1 (type value model + fold + marker) → 2 (text + name canonicalisation) → 3 (kind derivation + canonicaliser + renames) → 4 (variable.set takes `type`, strict ValidateBuild) → 5 (LLM representation). Each stage must leave both suites green before the next.
