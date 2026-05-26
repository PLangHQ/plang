## coder — v1 — 2026-05-26
**Target:** `Documentation/v0.2/app-tree.md`
**Why:** When tracing a `foo.Bar()` call where `foo`'s static type is an
abstract base (e.g. `path`, channel base, scheme), the trace needs to pick the
concrete impl. Today this requires data-flow back to the producer (registry
call, factory) — extra hops that the app-tree could absorb. In practice each
abstract has one dominant default impl (`path` → `file` for unscheme'd
strings; `channel` → varies but the bootstrap default is known). Noting the
canonical default in app-tree.md collapses an N-step lookup to one.

**Proposed change:** for every abstract type listed in app-tree.md, append a
one-line "Default impl:" line under the entry, like:

```
### app.types.path
Location: `PLang/app/types/path/`
Role: abstract base for scheme-typed paths.
Default impl: `app.types.path.file` (selected by scheme registry when no
explicit scheme present; HTTP/HTTPS/etc. dispatched separately).
```

Apply to every abstract / interface entry that has a clear "happens by
default" implementation. Skip entries with no dominant default (genuine
N-way dispatch with no canonical winner).

**Footer:** filed under explicit user request after a tracing-discipline
discussion on this branch; not in response to a security/test/audit finding.
