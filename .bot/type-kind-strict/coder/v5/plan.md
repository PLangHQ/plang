# coder v5 — Stage 5: LLM type representation

## Scope

1. `TypeSchemas` renderer (`PLang/app/builder/type/this.cs`) gains two
   modes: advertised kinds (closed list) → `name — kinds: a | b | c`;
   extension-derived kinds (Build hook discoverable, no static Kinds list)
   → `name — kind = extension (jpg, png, gif, ...)`.
2. `app.type.@this` gets `[LlmBuilder]` on `Name`/`Kind`/`Strict` so the
   `type` constructor surface (`{name, kind?, strict?}`) is discoverable
   by the catalog renderer.
3. `TypeDescription` constant on `app.type.@this` carries the
   replaces-`as text`-prose teaching: emit a dict, never the slash form.

## Done

- Dual-mode `TypeSchemas` rendering — advertised lists vs extension teaching
  with example extensions (md/txt/csv/html for text, jpg/png/gif/webp for image).
- `app.type.@this`: `[LlmBuilder]` on Name/Kind/Strict; const `TypeDescription`
  string holds the constructor teaching.
- Test bodies: `TypeSchemasRendererTests` (6) + `BuilderNamesTests` (3).

## Out of scope here

- `os/system/builder/llm/Compile.llm` — the hand-written valid-type list in
  the cached system prompt. The architect calls for replacing this with
  the catalog-generated vocabulary; the template change requires a build-trace
  capture to validate (per the architect's `Compile.llm` note), and that
  flow needs builder credentials + a real LLM call. Left for a follow-up
  with the build trace in scope.
- `os/system/builder/llm/CompileUser.llm` — drop the `Primitive types:` line.
  Same reason as above — trace-validated.
- `as text` prose removal from `variable.set` Notes — that prose currently
  lives in the markdown sidecar under `os/system/modules/variable/`. Same
  trace-validation rationale.

These three template/prose pieces are pure-content edits that need a real
LLM round-trip to verify they teach correctly; the C# pieces of Stage 5
(the renderer + the entity's `[LlmBuilder]` shape) are landed.

## Results

- C# total: 3803.
- C# passing: **3742** (vs 3734 after Stage 4, +8). 9 test bodies wrote;
  some were already trivially-passing due to Stage 1's catalog generation
  carrying numbers/kinds through.
- PLang: 253/253 + 10 stale.
