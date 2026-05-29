# Stage 7: Runtime type-loading

**Goal:** Let a PLang developer add or override types at runtime by loading a DLL — `- load mynumbers.dll` registers its `[PlangType]` classes and per-format renderers into the same registry and dispatch table the generator feeds.
**Scope.** *Included:* the `code.load`-style extension that scans a loaded assembly for `[PlangType]` classes and `ITypeRenderer` instances and registers them; `ITypeRenderer`; the overwrite-precedence wiring; the honest-limit documentation. *Excluded:* a package/dependency manager for DLLs; sandboxing beyond the existing `LoadAssemblyAsync` Execute gate (it already gates).
**Deliverables (per [plan/dispatch.md](plan/dispatch.md) "Runtime-loaded and overwritten types" + [plan.md](plan.md) "Extending the vocabulary at runtime"):**
- `app/types/ITypeRenderer.cs` — `interface ITypeRenderer { string Format { get; } void Write(object value, IWriter writer); }`. A loaded DLL ships one per format it supports (the type-system analogue of `ICode`).
- A loader (extend `code.load` or a sibling action) that, given a loaded assembly: scans `GetExportedTypes()` for `[PlangType]` classes → `Registry.RegisterRuntime(name, clrType)` (exists, `Registry.cs:103`); scans for `ITypeRenderer` implementations → `TypeSerializers.RegisterRuntime(typeName, formatToken, write)` (the Stage 2 seam). Mirrors `code.load`'s load-scan-register over `ICode` (`PLang/app/modules/code/load.cs`).
- Overwrite precedence: confirm `Registry.ResolveType` favors runtime over built-in (it does, `Registry.cs:85`) and that `TypeSerializers` lookup gives runtime registrations the same precedence.
- A typed load failure when a loaded `[PlangType]` registers no `"*"`/covering renderer (mirrors `code.load`'s "no parameterless constructor" rejection) — the runtime analogue of the `PLNG_SerializerCoverage` build gate.
- Docs note (user-facing): the honest limit on overwriting built-ins.
**Dependencies:** Stages 1–2 (registry, `RegisterRuntime`, dispatch table + its `RegisterRuntime` seam). Additive — nothing in 1–6 needs this; the static vocabulary works without it.

## Design

> **You own the code.** Intent, not dictation.

This is the smallest architectural step because the seams already exist: `Registry.RegisterRuntime` (built today), `ResolveType`'s runtime-first precedence (built today), `TypeSerializers.RegisterRuntime` (Stage 2 seam), and `code.load`'s load-scan-register pattern (built today, over `ICode`). Stage 7 is "do the same for types."

**The honest limit — document it loudly so nobody is surprised.** Runtime registration changes *resolution and rendering* — what a name resolves to, how a value serializes. It cannot rewrite what the source generator already baked at build: PLNG slot validation, the `Data<int>` slots on already-compiled handlers, the type stamps in shipped `.pr` files. So `- load myint.dll` makes `int` resolve and render differently *going forward*, but a handler compiled against the built-in `int` still sees the built-in at its typed slot. **Adding** new types is unconstrained; **overwriting** built-ins is "new resolution + new rendering, same compiled slots."

**Why this is a real stage and not a throwaway:** it's the thing that keeps PLang's type system *open* — the same way `code.load` keeps the provider system open. The built-in set is a starting vocabulary, not a closed one. But it's last because it's additive and the core pattern must be proven first (Stages 1–5); shipping it before the static types are solid would be building the extension point before the thing it extends.
