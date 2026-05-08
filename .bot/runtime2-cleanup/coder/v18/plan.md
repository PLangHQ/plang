# Stage 18 — coder plan (`mime-table-split`)

Split format characteristics out of Types into their own home, and keep
the MIME→CLR resolution as a static method on Types.

## Files

- **NEW** `App/Formats/this.cs` — instance class. Owns
  `_extensionToKind`, `_extensionToMime`, `_notCompressible`,
  `_allKinds`, `_mimeToKind`. Methods: `Kind`, `Mime`, `Compressible`,
  `KindOf`, `Add`, `Remove`. Lifted verbatim from
  `Types/this.cs:14, 215, 322–340, 384–465`.
- **DELETED** `App/Utils/MimeTypes.cs` — both functions migrated:
  `GetMimeType` → `app.Formats.Mime` (instance);
  `TryGetClrType` → `App.Types.@this.ClrFromMime` (static).
- `App/Types/this.cs` — stripped of MIME block (file shrinks 472 → ~80
  lines). Gains `public static System.Type? ClrFromMime(string mimeType)`
  with the same body MimeTypes.TryGetClrType had. Static is fine because
  the MIME-family logic is hardcoded — no instance state needed.
- `App/this.cs` — gain `public Formats.@this Formats { get; } = new();`.
- `App/Utils/TypeMapping.cs:135` — `MimeTypes.TryGetClrType(typeName)` →
  `App.Types.@this.ClrFromMime(typeName)`. Stays in the same `GetType`
  fallback chain.
- `App/Utils/TypeMapping.cs:142` — `GetMimeType(extension)` forwarder
  deleted. Three callers updated:
  - `App/modules/file/providers/DefaultFileProvider.cs:28, 45` → `action.Context.App.Formats.Mime(path.Extension)`.
  - `App/FileSystem/Path.cs:128` → `Context?.App?.Formats?.Mime(Extension) ?? "application/octet-stream"`.
- `App/Data/this.cs:35, 40` — `Context?.App.Types.KindOf` / `.Compressible` → `.Formats.KindOf` / `.Compressible`.
- `PLang.Tests/App/Types/EngineTypesTests.cs` — `_types.Kind/Mime/...` calls swept to a new `_formats = new global::App.Formats.@this()` field.

## Subtle data merge

The original `MimeTypes.cs` had a few extension→MIME mappings the
`Types/this.cs` table didn't, plus differed slightly:

- `.goal` → `text/plain`, `.pr` → `application/plang-goal` — only in
  MimeTypes; Types didn't have them. **Added to Formats** (without
  these, `.pr` files get `application/octet-stream`, file.Read can't
  resolve a CLR type, .pr deserialization breaks. C# tests
  `GetGoals_*` and PLang `--tester` both crashed without these.)
- `.htm` → `text/html`, `.llm` → `text/plain` — same. Added.

## Verification

- `find PLang/App/Utils/MimeTypes.cs` → empty.
- `find PLang/App/Formats/this.cs` → exists.
- `app.Formats` and `App.Types.@this.ClrFromMime` exist.
- `grep -rn "MimeTypes\." PLang/ PLang.Tests/ --include='*.cs'` → 0.
- C# 2752/2752; PLang 199/199; build clean.
