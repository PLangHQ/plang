# Coder v4 — Summary: Action catalog metadata (Phases 1–4)

## What this is

The action catalog fed to the LLM builder (via `system/actions/v2/summary.md`) was missing
module-level and action-level descriptions, had leaked CLR collection type names, and had
noisy examples that restated the signature verbatim. This session audited all four phases and
delivered the remaining work (Phase 3 — example pruning and formal rewrite).

## What was done

### Audit findings — Phases 1, 2a-2e, and 4 already committed

- Phase 1 → commit `197729df` ("Catalog: normalize collection type names")
- Phases 2a–2e → commit `83c9763d` ("Catalog: add module and action descriptions")
- Phase 4: Fluid's `IgnoreCasing = true` (FluidProvider.cs line 70) makes PascalCase properties work.

### Phase 3 — implemented this session

**Commit `7c0beeec`**: "Catalog: prune examples and rewrite to formal shape"

36 examples evaluated. 18 dropped, 18 kept and rewritten to formal pipe syntax.

**Drop**: assert.* (10), condition.else, error.throw, event.remove, file.copy/delete/move/save, output.write
**Keep/rewrite**: condition.compare/elseif/if, crypto.hash/verify, event.on/skipAction, file.exists/list/read, llm.query, loop.foreach, test.discover/report/run/tag, ui.render, variable.set

## Code example (before/after)

```csharp
// Before — restates signature
[Example("hash %content%, write to %hash%", "Data([any] %content%), Algorithm([string] keccak256)")]

// After — formal pipe syntax with variable capture
[Example("hash %content%, write to %hash%",
    "crypto.hash Data([object] %content%), Algorithm([string] keccak256) | variable.set Name([string] %hash%), Value([object] %__data__%)")]
```

## Commit SHAs

- Phase 1: `197729df`
- Phase 2: `83c9763d`
- Phase 3: `7c0beeec`
