# Test strategy — comparison redesign

## Scope (the floor/ceiling)

The integration cuts below are the **contract for end-to-end behaviour** — they prove the developer-visible promises hold through the whole pipeline (builder → runtime → `Data.Compare` → type → boundary → result). Everything beneath them — per-type round-trips, the value-door internals, the negative paths, the tripwire throws — lives in `test-coverage.md` and is written one test per matrix row.

## Test layer mapping

The rule: **C# TUnit pins the engine-internal behaviour the developer can't see; PLang `.goal` pins the developer-facing surface.**

- **C# (`PLang.Tests/App/...`)** owns: the value door (`Value()` lazy load, `ValueTask` sync-completion when present, `Peek()` no-parse, the framework-method tripwire throws, `ToString` degradation, `internal PresentValue()` throwing on pending); the per-type compare (rank winner selection, coercion, `Order` results, the antisymmetry property); the `Comparison` enum boundary mapping; the `Diff` rename still diffing.
- **PLang `.goal` (`Tests/`)** owns: the developer surfaces — `if a > b` / `==` / `!=` / `<` / `>`, `sort`, `contains`, `assert equals`/`greaterThan`, cross-type comparisons (`"5" == 5`), null comparisons, `sort by <key>`.
- **Integration cuts** are the end-to-end behaviours below; some are goal-level, the lazy-read one needs C# instrumentation (the read counter).

Per-behaviour assignment is in `test-coverage.md`'s matrix — read it top-to-bottom, one test per row. The split above is the rule when a behaviour could sit in either layer: if a developer would write it in a `.goal`, it's a goal test; if it's only observable from C# (alloc behaviour, a throw, a read count), it's C#.

## Integration cuts

Four cuts. Each proves one end-to-end promise.

1. **Cross-type antisymmetry round-trip.** Setup: `%a% = "10"` (text), `%b% = 9` (number). Run `if %a% > %b%` and `if %b% < %a%`; also `if "5" == 5`. Capture which branches fire. Must prove: both ordering directions **agree** (`"10" > 9` true *and* `9 < "10"` true — numeric, not lexical), and `"5" == 5` is true. This is the whole rank + coercion + dispatch + boundary path, and it's the test that catches the antisymmetry bug the rank exists to prevent.
2. **Lazy read.** Setup: a file with known content, read through an instrumented source that counts reads (Data already exposes `internal MaterializeCount`). Run: read the file into `%x%` and do **not** use it → assert zero reads. Then use `%x%` (navigate or write it out) → assert exactly one read, and a second use → still one (cached). Must prove: nothing is read until `Value()` is first awaited, and the path is held until then. This is C#-instrumented (the read count isn't visible from a `.goal`).
3. **Sort by an I/O key.** Setup: a list of files of different sizes. Run `sort %files% by size`. Capture the resulting order and that the goal completes (no hang). Must prove: keys (sizes) materialise async (files read in phase 1), ordering runs sync on the in-hand sizes, the result is correct, and nothing deadlocks — the two-phase shape with no sync-over-async.
4. **Enum boundary.** Setup: `%d%` a dict, `%n% = 5`, `%d2%` another dict. Run: `if %d% > %n%` → error; `if %d% == %n%` → error; `if %x% == null` → works (false/true, no error); `if %d% == %d2%` → works. Must prove: `Incomparable` errors on every operator, `NotEqual` errors only on ordering, null is always equality-comparable, and same-type equality works.

## What these cuts do not cover (the matrix picks these up)

- **Per-type round-trips** — `date` vs `date`, `time`, `datetime`, `duration` ordering; `list` lexicographic order; `bool`/`binary`/`choice` equality; `datetime` vs ISO-text coercion. One row per type in the matrix.
- **The value-door internals** — `ValueTask` completes synchronously when present (assert `IsCompleted` / no alloc), `Peek()` returns the unparsed form (json string stays a string), `ToString` returns `<text pending>` on a pending value without throwing, `PresentValue()` throws on pending.
- **The tripwire throws** — `GetHashCode`/`Equals`/operators on a view throw with the guidance message. These are negative C# tests (the consolidated failure matrix).
- **The `Diff` rename** — the existing `DataCompareTests` still pass under `Diff`.
- **Both-direction equality for every coercible pair** — cut 1 proves the principle on `text`↔`number`; the matrix repeats it for `datetime`↔text and any other coercible pair.

The "no `Type.Name` switch / no second registry / reuses the existing routing" properties are architectural invariants, not behaviours — they're a code-review assertion (codeanalyzer), not a test row.
