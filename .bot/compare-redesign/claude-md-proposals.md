## architect — compare-redesign — 2026-06-08
**Target:** /CLAUDE.md (Runtime2 Conventions)
**Why:** Ingi asked for a consistent notation when referring to class members in docs/plans/comments, applicable to all classes: an instance call written as `Data.Compare(...)` reads as a static and misleads. The fix is free and removes the ambiguity at a glance. Surfaced while writing the comparison-redesign stages (`Data.FromRaw` is static; `Compare`/`Value`/`Peek` are instance).
**Proposed change:**

```
- **Member-reference notation (docs/comments).** When naming a class member in prose, the leading token signals static vs instance: a **static** member is written on the capital Type (`Data.FromRaw`, `Path.Resolve`), an **instance** member on a lowercase instance variable (`data.Value()`, `data.Compare(other)`, `path.ReadText()`). Applies to all classes. This is distinct from a namespace-qualified type name (the namespace stays lowercase: `app.data.IEquatableValue`) and from a type used in a signature (`Compare(Data other)` — `Data` is the parameter type). The rule is only about member-access expressions.
```
