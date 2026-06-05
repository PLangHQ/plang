## docs v1 — 2026-06-05

**Target:** `/CLAUDE.md`

**Why:** Three cross-cutting patterns introduced on `collections-are-data` are
now load-bearing for any coder writing collection or comparison code. Without
CLAUDE.md coverage, future coders will re-derive (or break) them from scratch:
(1) `Compare` as the single typed-compare mediator — parallel paths for ordering
and equality already caused the F2 case-mismatch bug fixed on this branch;
(2) `dict.@this` / `list.@this` as the native PLang collection types — a coder
unaware of these will reach for `List<object?>` or `Dictionary<string,object?>`
in action handlers, bypassing the signature/type infrastructure; (3) `IListLeaf`
for the dissolve-into-list contract.

**Proposed change — extend the existing "Truthiness" bullet and add a new "Collections" bullet:**

```markdown
- **Truthiness — `IBooleanResolvable`.** A value's boolean meaning belongs to the value, not to `Data`. `Data.ToBoolean()` is the sync fallback (null/false/0/"" falsy); do **not** add type-specific cases. A type that knows its own truthiness implements `app.data.IBooleanResolvable` (`Task<bool> AsBooleanAsync()`) — `path` does, where truthiness means "does the resource exist" (stat for `FilePath`, HEAD for `HttpPath`). Because the probe can be I/O, the condition-evaluation pipeline is **async**: `IEvaluator.Evaluate` returns `Task<data.@this>`, `Operator.Evaluate` is `Func<data.@this?, data.@this?, Task<bool>>`, `assert.IsTrue`/`IsFalse` are async. A new operator or evaluator must `await`. Full rule: `Documentation/v0.2/good_to_know.md` "Truthiness — `IBooleanResolvable` and async condition evaluation".
- **Equality and ordering — `IEquatableValue` / `IOrderableValue` / `Compare`.** A value that owns its compare implements `IEquatableValue` (`bool AreEqual(object?)`) and/or `IOrderableValue` (`int Order(object?)`). `app.data.Compare` is the **single mediator** — both `if a > b` and `list.sort` route through it; the two paths can never drift. `dict` is equality-only (no natural ordering → `Compare.NotOrderableException` on `Order`); `list` implements both. Do NOT add `is MyNewType` arms to `Compare`; instead implement the interface and recurse back through `Compare` for children. `ScalarComparer` (internal) is the one legal type-switch over CLR scalars. Full rule: `Documentation/v0.2/good_to_know.md` "Compare — single typed-compare mediator".
- **Native collection types — `dict.@this` / `list.@this`.** PLang's native object and array values live in `app/type/dict/` and `app/type/list/`. Collections hold `Data` end-to-end — an element stored inside keeps its type-tag and signature; never decompose to raw CLR on entry. Action handlers that work with lists or dicts receive `Data<list.@this>` or `Data<dict.@this>` — do not reach for `List<object?>` or `Dictionary<string,object?>`. `IListLeaf` is the value-side contract for "dissolve into my container list" (only `list.@this` implements it; dict/table stay whole). `CopyStructure` on `list.@this.Add` prevents list-in-list write-through aliasing; nested dicts share by reference (intentional, auditor O1, future copy-on-write pass). Full rule: `Documentation/v0.2/good_to_know.md` "dict.@this and list.@this — native PLang collection types".
```

Replace the single existing "Truthiness" bullet with the three bullets above (Truthiness bullet text is unchanged; the two new bullets follow it).
