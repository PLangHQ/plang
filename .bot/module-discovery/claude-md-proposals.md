## architect — v1 — 2026-07-15
**Target:** /workspace/plang/CLAUDE.md (Runtime2 Conventions, the `app/` casing bullet)
**Why:** Ingi ruled (module-discovery planning, 2026-07-15) that properties on `app.@this` are lowercase + singular like everything else in the PLang vocabulary — "not sure where that rule came from but it's wrong." Stage 4 introduces the first lowercase node (`app.module`); the documented PascalCase-properties carve-out now contradicts the ruling and will mislead the next reader. Existing properties (`.Cache`, `.Goal`, `.FileSystem`, …) rename in their own mechanical pass, not on this branch.
**Proposed change:** In the first Runtime2 Conventions bullet, replace

```
**Property names on `app.@this` stay PascalCase** (`.Cache`, `.Builder`, `.Code`, `.Module`, `.FileSystem`, `.Goal`, etc.) — only the *types* live in lowercase singular namespaces. So `ctx.App.FileSystem.Read(...)` is property access (stays capital); `app.filesystem.@this` is the type.
```

with

```
**Property names on `app.@this` are lowercase + singular** (ruled 2026-07-15): `app.module` is the first; existing PascalCase properties (`.Cache`, `.Goal`, `.FileSystem`, …) are legacy pending a dedicated rename pass — do not add new PascalCase properties.
```
