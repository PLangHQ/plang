# Stage 6: Structured `{name, kind}` at the external-content producers

> **Coder/test-designer: you own the final shape.** Every signature and code sketch here is a *suggestion* to convey intent. The settled decisions are: (1) one shared derivation that both build and runtime call so they can't drift, (2) `file.read` and `http` produce `{name, kind}` not a muddy MIME or bare extension, (3) non-string reads materialize lazily. The literal C# is yours.

**Goal:** Make the file/network read producers stamp the structured `type` `{name, kind}` — derived once, used at both build and runtime so they agree — and hold non-string reads as raw text that materializes on first access.
**Scope:** Included — a shared `(extension | mime) → type.@this{name, kind}` derivation on the format/path layer; `file.read` (`ReadText`, the image-lift, and `Build()`); `http.request`/`http.download` (`HttpBuildHelpers.InferTypeFromUrl` and the runtime response-body stamp); lazy materialization for non-string CLR targets. Excluded — `crypto`/`hash` (stage 7, different mechanism); numeric kinds (already auto-stamped by the `Data.Type` getter); `variable.set` (done, stage 4); `file.save` serializer dispatch (separate concern — note below).
**Deliverables:** the shared derivation; `file.read` and `http` both emitting `{name, kind}` with build==runtime; lazy parse for non-string reads.
**Dependencies:** Stages 1–3 (`type.@this` carries `Name`/`Kind`; `Create` splits the slash and canonicalises; `Format.FamilyOf`/`CanonicaliseKind`/`Mime`), Stage 2 (`Canonical[typeof(string)] = "text"`). Independent of stage 7.

## Why

The branch migrated the *consumer* (`variable.set` takes a `type`, stage 4) and the entity/wire model (stages 1–3, 5), but left the *producers* on the old path. Today `file.read` stamps two different wrong answers for the same file:

- **Runtime** (`FilePath.ReadText`, `path/file/this.Operations.cs:89`): `type = FromMime("text/markdown")`. `FromMime` calls the constructor directly, *not* `Create`, so the slash never splits — `Name = "text/markdown"`, `Kind = null`. The muddy full-MIME name this branch exists to kill is still live.
- **Build** (`file/read.cs::Build()`, lines 71–84): explicitly *skips* text (`!equals "text"`) and stamps the bare extension `"md"` as the type name. No kind.

So build says `type=md`, runtime says `type=text/markdown`, neither is `{text, md}`. `http` has the identical split: `HttpBuildHelpers.InferTypeFromUrl` stamps the bare extension at build; the runtime stamps the response body by Content-Type/MIME.

Patching each handler leaves the drift in place. The fix is one derivation both call.

## Design

### 1. One shared derivation

A single function that turns an extension or a MIME into the structured type, living where the format knowledge already is (`app.format.list.@this`, or a `path.Type` accessor since files/URLs are path-like):

```
Format.TypeFromExtension(".md")        → type{ name="text",   kind="md" }
Format.TypeFromMime("text/markdown")   → type{ name="text",   kind="md" }
Format.TypeFromExtension(".json")      → type{ name="object", kind="json" }
Format.TypeFromExtension(".png")       → type{ name="image",  kind="png" }
```

- **`name`** comes from the extension's *target* CLR type → canonical name: `Mime(ext) → ClrType → Canonical`/`App.Type.Name`. `string→text`, `Dictionary→object`/`dict`, `byte[]→bytes` (image-MIME bytes lift to `image` in the handler, as today). This is the type the value *materializes to*, not the transient string carrier — which is what makes the lazy path (below) consistent.
- **`kind`** is the extension, canonicalised via `Format.CanonicaliseKind` (`md|markdown→md`, `jpg|jpeg→jpg`). For a MIME input, the subtype canonicalises the same way.
- Both `file.read.Build()` and `ReadText` call this. Build==runtime by construction — the current disagreement becomes impossible.

### 2. `file.read` migration

- **`ReadText`**: replace `FromMime(mime)` with the shared derivation. Read the file, stamp `{name, kind}`. For a non-string target, hold the raw text and defer conversion (see §4). The `ClrType == typeof(byte[])` branch that decides bytes-vs-text still works — `ClrType` resolves off `name` through the registry as before.
- **Image-lift** (`read.cs:40`): `FromName("image")` → carry the kind too (`{image, png}`). The derivation already produces it; pass it through instead of dropping it.
- **`Build()`** (lines 71–84): delete the "keep the extension stamp for text" branch — that was the pre-branch Stage-5 logic this branch overturns. Call the shared derivation; return the same `{name, kind}` the runtime will stamp.

### 3. `http` migration

- **`HttpBuildHelpers.InferTypeFromUrl`**: stop returning `ext.TrimStart('.')` as a bare name. Call `Format.TypeFromExtension` on the URL's extension; stamp `{name, kind}`.
- **Runtime response body**: where the response Body's type is set from the `Content-Type` header, route through `Format.TypeFromMime(contentType)` so the body carries `{name, kind}` (e.g. `application/json` → `{object, json}`, `text/markdown` → `{text, md}`).

### 4. Lazy materialization (the todo, scoped)

Per Ingi (2026-05-30): a file read should return the **raw text** and materialize the structured shape only on **first access** — for *every* non-string CLR target, not just JSON.

- `ReadText` stamps the `{name, kind}` from the extension but, for a non-string target, sets the value as the raw string plus a deferred factory rather than converting inline. The machinery exists: `Data.SetValue(Func<object?>)` for the factory and `Data.ConvertValue()` ("if value is a string and Type knows the conversion, convert once on first navigation").
- String targets (`.md`/`.txt`/`.csv` → `text`) are already strings — no factory, nothing deferred.
- Net: reading a `.json` you only pass along never pays the parse; navigating into it parses once. The type is correct (`{object, json}`) from the moment of the read; only the value is lazy.

### `file.save` — adjacent, not in scope

`file.save` delegates to `path.Save(value)`, which picks a *serializer* by destination extension. It doesn't return a kind-bearing value, so it's not a type-stamp gap. Worth a glance that its serializer dispatch keys off the same extension→format the derivation uses (so a `.json` save and a `.json` read agree), but no change is required here.

## The Data shape this produces

```
read "file.md"  →  data.@this { Name="file.md", Value="# Hello…",
                                Type = { Name="text", Kind="md", Strict=false } }
wire:  { "name":"file.md", "type":{"name":"text","kind":"md"}, "value":"# Hello…" }
```
