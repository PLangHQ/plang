# Coder — compare-redesign

## Version: v5 (FIRST IMPLEMENTATION version — v1–v4 were reviews)

### What this is

The typed value model. Architect settled a 7-stage plan; test-designer stubbed ~140 tests
(125 C# + 15 `.goal`). This is the first version that writes production code. Ingi's sequencing
decision (asked this session): **full sprint, build red mid-flight** — drive Stages 2→6 (one
all-or-nothing green unit) straight through in dependency order, checkpointing each session until
it lands green. Not "seek additive increments," not "Stage 2 only."

### What was done (v5)

Landed the two pieces of the sprint that are **green standalone**, verified and committed:

1. **Stage 1 — `Comparison` enum** (`PLang/app/data/Comparison.cs`). Sign-free
   `{ Less, Equal, Greater, NotEqual, Incomparable }`; nothing reads it yet (architect's design).
   Test `Stage1_ComparisonEnumTests` rewritten from stub → green (reflection asserts exactly the
   five members; `NotEqual != Incomparable`).
2. **Stage 2 `Peek()`** — `ScalarValue` (property) → `Peek()` (method) at `PLang/app/data/this.cs:247`.
   ~20 call sites migrated across PLang/ and PLang.Tests/. Build clean (0 errors); the
   `ScalarAccessTests`/`Cut2_TouchMaterializes` tests that exercise it stay green. This is Stage 2's
   "Peek()" deliverable, landed independently because it doesn't touch the door.

### What is NOT done — the door (Stage 2 core), and Stages 3–6

The heart of Stage 2 is cutting `Data.Value` from a property to an async `ValueTask Value()` method.
That is the single all-or-nothing change: it breaks every Data-receiver `.Value` read site (the
migrating subset of 977 total `.Value` reads in PLang/), the `Data<T>`/`DynamicData` overrides, and
the `Wire` serializer's sync value-read (`Wire.cs:485,528,545`). There is NO faithful shortcut —
the design's whole point is to force the async read at every site. It cannot be half-landed in a
navigable state, so it is the next focused session's work.

**The runway is written: `v5/door-implementation.md`** — the concrete C# shape the architect's prose
leaves to the coder: the `Value()`/`Load()`/`SetValue` shape + `_present` field; the override-seam
decisions for `Data<T>.Value` and `DynamicData` (recommend dropping `Data<T>.Value`, overriding
`Load()`); the serializer touchpoint (add internal sync `Materialized()` that throws if not present,
keep `Peek()` for the `RawUntouched` verbatim path); navigation→`ValueTask`; `GetParameter<T>` lazy +
the source-gen edit; the ~42 `param.Value!` `await→guard→use` migration; `data.Type → return _type`;
`.`/`!` resolver; no-`ToRaw`. **The compiler error list after the door edit IS the migration
worklist** (views keep their sync `.Value`, so only Data receivers error).

### Code example — the door shape (from door-implementation.md)

```csharp
public ValueTask<object?> Value()
{
    if (_valueFactory != null) { _value = _valueFactory(); _valueFactory = null; _present = true; }
    if (_present) return new ValueTask<object?>(_value);   // in memory: sync, zero alloc
    return Load();                                          // pending: async read+parse
}
protected virtual async ValueTask<object?> Load() => _value = await ReadAndParse();
```

### Next

`run.ps1 coder typed-value-model "Land the Stage 2 async Value() door per
.bot/compare-redesign/coder/v5/door-implementation.md, then continue 3-6" -b compare-redesign`

Do the door cutover as one focused pass: edit the door, build PLang, work the compiler error list
file-by-file (Navigation → Variable → handlers via `await→guard→use`), then Stages 3–6. Build stays
red across the unit (Ingi's accepted sprint mode); land green at the 2→6 boundary.
