# Coder v4 — Plan: Action catalog metadata (Phases 1-4)

## Status audit — what's already done

**Phase 1** (TypeMapping.cs collection types): Done. Lines 210-229 already handle HashSet<>, IEnumerable<>, ImmutableList<>, ISet<>, IReadOnlyCollection<>, ICollection<>, IReadOnlyList<> → `list<T>`, and ConcurrentDictionary<,>, ReadOnlyDictionary<,>, SortedDictionary<,>, ImmutableDictionary<,> → `dict<K,V>`. No code change needed.

**Phase 2a** (ModuleDescriptionAttribute): Done. In Attributes.cs lines 85-95.

**Phase 2b** (Action Description/ModuleDescription properties): Done. In Action/this.cs lines 101-110.

**Phase 2c** (Modules.Describe() population): Done. In Modules/this.cs lines 224-255 (reads [Description] and [ModuleDescription], caches per-namespace).

**Phase 2d** (Action descriptions): Done. All 105 action files have [System.ComponentModel.Description(...)].

**Phase 2e** (Module descriptions): Done. All 27 modules have [ModuleDescription(...)] on the alphabetically-first action.

**Phase 4** (Template verification): Fluid uses `IgnoreCasing = true` (FluidProvider.cs line 70), so PascalCase `Description`/`ModuleDescription` resolves fine. No code change needed.

## Remaining work — Phase 3 (Example pruning + formal rewrite)

### Decision table (36 current examples → ~13 keepers)

**DROP** (restates signature, zero added value):
- assert.contains, assert.equals, assert.greaterThan, assert.isFalse, assert.isNotNull, assert.isNull, assert.isTrue, assert.lessThan, assert.notContains, assert.notEquals (10)
- condition.else (1)
- condition.if (1)  
- error.throw (1)
- event.remove (1)
- file.copy, file.delete, file.move, file.save (4)
- output.write (1)

Total drops: 19

**KEEP + REWRITE** to formal pipe syntax:
- condition.compare — has implicit `write to` pipe
- condition.elseif — non-obvious Operator enum usage  
- crypto.hash — has `write to` pipe
- crypto.verify — has `write to` pipe
- event.on — non-obvious EventType enum + GoalPattern
- event.skipAction — non-obvious use-inside-event context
- file.exists — has `write to` pipe
- file.list — has `write to` pipe
- file.read — has `write to` pipe
- llm.query — complex Messages list + Schema
- loop.foreach — two-action pattern
- test.discover — has `write to` pipe
- test.report — format param + has `write to` pipe
- test.run — has `write to` pipe
- test.tag — Tags as list
- ui.render — dict param + has `write to` pipe
- variable.set — already formal-ish, shows JSON struct + type coercion

Total keepers: 17 (target: ~13-20, this is fine)

### Formal syntax rules applied
- Single action with result: `module.action Param([type] value)`
- "write to %var%" always adds: `| variable.set Name([string] %var%), Value([object] %__data__%)`
- Two-action patterns (foreach+call): `loop.foreach Collection([list<any>] %x%), ItemName([string] item) | goal.call GoalName([goal.call] ProcessItem)`
- Enum values shown with their string form
- Optional params omitted from examples (not showing defaults)

### Commit plan
After Phase 3 edits: one commit "Catalog: prune examples and rewrite to formal shape"

Then: verify build stays green, write summary, write patch.
