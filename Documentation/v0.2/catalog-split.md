# Dissolve `app.builder.type` — split the LLM catalog into its real homes

**Status:** a stage on `cli-app-property-override` (Stage 10). Agreed with Ingi 2026-07-07.

## What `app.builder.type` is (and why the name is wrong)

`app.builder.type` (held as `Modules.Schema`) is **the LLM's view of the action + type catalog** —
what the compile prompt shows the model so it knows which actions and types it may emit.

Traced identity:
- **Owner:** `Modules.Schema` (`app/module/this.cs:28`) — Modules is the action registry.
- **Reads:** `app.Type.GetBuilderTypeNames()` / `BuildTypeEntries()` (the type catalog) + `app.Format`
  + the action catalog. `Build()` is mostly a delegation to `app.type.catalog`.
- **Produces:** `PrimitiveNames`, `Types` (record/enum entries), `Kinds`, rendered `Example`s.
- **Used:** build-only — `modules.Schema.Build()` → Liquid template → compile LLM prompt.

The name is the worst of its three identities: it's not the *builder* (it's the catalog the builder
*reads*), it's owned by *Modules* (not `build`), and its data is the *type catalog*. There is no
`app.module.catalog` today and we don't want to invent one just to hold this — so it **splits**.

## The split

```
app.type.catalog.*     ← grows its own LLM view: PrimitiveNames / Types / Kinds
                          (Build() already calls app.Type.BuildTypeEntries/GetBuilderTypeNames —
                           the catalog is describing ITSELF; that logic comes home here)
app.type.<spec>.*      ← Example / Action  (the structured spec records)
                          NEUTRAL location: authored across modules (math, error, …) via
                          `public static Example[] ExamplesForLlm()`. They must NOT live under
                          `build` — else every action module depends on the build module (backwards).
                          app.type.* is where everyone can depend already.
app.module.build.*     ← Render + prompt assembly (turning the catalog + examples into the
                          formal-language string the compile LLM reads). Build-only; fine for
                          `build` to depend on app.type.* (build is the last link in the chain).
app.builder.type       ← dissolves. app/builder/type/ folder removed.
```

**Coupling test (the reason for the split):** today `math` authoring an example pulls in
`app.builder.type`. After: `math` depends on `app.type.*` (fine — everyone does), never on `build`.
`build` depends on `app.type.*` and renders. No module → build dependency anywhere.

### Naming knob (Ingi's call)
The spec records under `app.type.*`: `app.type.example` / `app.type.action`, or a shared
`app.type.spec.*`. Note `app.type.action` risks confusion with the real action descriptor
`app.goal.steps.step.actions.action.@this` — lean toward `app.type.spec.Example` / `.Action` or a
distinct name.

## The bigger win this unlocks — catalog as an LLM tool

The catalog already has a PLang action face: **`build.actions`** returns `list<action>` (filtered),
**`build.types`** returns the type entries (`app/module/build/actions.cs`, `types.cs`). Today the
builder *pre-renders the whole catalog* into every compile prompt (static, large, noisy).

Two follow-on moves (each optional, each valuable):
1. **Generalize the action face** so a *dev* can introspect the catalog, not just the builder —
   e.g. `- build.actions where module="http", write to %actions%`. The machinery exists; it's a
   home/visibility question (still `build.*`, or a neutral `module.*`/`type.*` action).
2. **Expose the catalog as LLM tools** (function-calling) instead of pre-rendering it — the `llm`
   action already carries `tools` (OpenAi function-calling is wired). The model calls
   `getActions(filter:"file.*")` on demand → smaller prompt, model explores instead of being
   spoon-fed the whole catalog. This is the high-value bit.

End state: one catalog, three faces — the C# view (`app.type.catalog` + specs), the PLang action
(`build.actions`/`build.types`), the LLM tool.

## Migration sketch (for the branch that does this)

1. Move `Example` / `Action` / (their `Render` inputs) → `app.type.<spec>.*`. Update the ~8 author
   sites (`math/*`, `error/*`, `module/this.cs`) — compiler-verified.
2. Move the `PrimitiveNames`/`Types`/`Kinds` assembly onto `app.type.catalog` (it already owns the
   source methods). Expose the LLM view from there.
3. Move `Render` + the prompt-string assembly → `app.module.build.*` (its only consumer).
4. Delete `app/builder/type/`; repoint `Modules.Schema` (rename/relocate) and `build/code/Default.cs`.
5. Verify: build clean, Modules/Runtime baseline, and the builder still renders its prompt (needs the
   builder to run — gated on the born-source regression, same as the builder-rebuild task).
