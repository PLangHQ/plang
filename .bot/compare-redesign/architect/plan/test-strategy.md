# Test strategy — typed value model

## Scope (the floor/ceiling)

The integration cuts below are the **contract for end-to-end behaviour** — they prove the developer-visible promises hold through the whole pipeline (builder → runtime → the door → the type → the boundary → result). Everything beneath them — per-type round-trips, the door/plane internals, negative paths, the gate — lives in `test-coverage.md`, one test per matrix row.

## Test layer mapping

The rule: **C# TUnit pins the engine-internal behaviour a developer can't see; PLang `.goal` pins the developer-facing surface.**

- **C# (`PLang.Tests/App/...`)** owns: the value door (`Value()` lazy load, `ValueTask` sync-completion when present, `Peek()` no-parse, private backing, no public `.Value`); the `.`/`!` resolver (data plane vs property plane, the type answering); per-type `Compare` (rank, coercion, the enum, antisymmetry); references' narrow-on-examination (identity accumulates `item|file|dict`, same instance) and chain-wide `!` resolution; type-owned serialization; the lazy param getter (`GetParameter<T>` hands a lazy `Data<T>`, the read + resolution-error guard land at `await Param.Value()`) and the async navigation chain (`GetChild`/`Get`/`Resolve` are `ValueTask`, awaited once); the gate (public `item`-subtype member returning CLR fails); the `Peek`/`Diff` renames.
- **PLang `.goal` (`Tests/`)** owns: the surfaces — `if a > b`/`==`/`<`, `sort`, `contains`/`unique`, `assert`, `read`/`write out`, navigation `%x.field%` (data) and `%x!prop%` (property), cross-type (`"5" == 5`), null comparisons.
- **Integration cuts** are the end-to-end behaviours below; the lazy-read one needs C# instrumentation (a read counter).

Per-behaviour assignment is the matrix in `test-coverage.md`. The split rule when a behaviour could sit either side: if a developer writes it in a `.goal`, it's a goal test; if it's only observable from C# (alloc/sync-completion, a throw, a read count), it's C#.

## Integration cuts

Five cuts. Each proves one end-to-end promise.

1. **Cross-type antisymmetry.** `%a% = "10"` (text), `%b% = 9` (number). `if %a% > %b%` and `if %b% < %a%` must **both** fire (numeric, not lexical); `if "5" == 5` true. Proves rank + coercion + dispatch + boundary, and catches the antisymmetry bug rank exists to prevent.
2. **Lazy read + narrow-on-examination + chain-wide `!`.** Read a file through an instrumented source that counts reads. `read file` then `%file!file!path%` → **zero reads** (location, no materialise) and `is file` → true (no read). Then `%file.field%` / `if %file% is dict` → **exactly one** read (the narrow); a second use → still one (cached); `%file!file!path%` **still resolves after** the narrow (chain-wide `!`, not headline-only). Proves the lazy door, narrowing-accumulates-identity, and "held value reads are sync after the door." The same lazy read is reached from a **handler param**: a param bound to `%file.field%` doesn't read at getter access, only at `await Param.Value()` — and the resolution-error guard fires *after* that await (a bad-scheme param → typed error, not an NRE). Counting reads through the param path proves `GetParameter<T>` is lazy.
3. **`write out %dir%` is a listing, not a content dump.** A directory with files/sub-dirs. `write out %dir%` → a listing of **paths/names**, *not* the files' contents. Proves `dir.list : list<path>` + type-owned serialization + the bug we traced (each-entry-self-serialising would have dumped contents).
4. **Sort by an I/O key.** `sort %files% by size` → keys materialise async (files stat/read in phase 1), order runs sync, result correct, no hang. Proves the two-phase sort with no sync-over-async.
5. **Enum boundary + membership.** `%d%` a dict, `%n% = 5`. `if %d% > %n%` → error; `if %d% == %n%` → error; `if %x% == null` → works; `if %d% == %d2%` → works; `[%d%] contains %n%` → **false, no error**. Proves `Incomparable`/`NotEqual` boundary, the null carve-out, and membership-never-errors.
6. **`read`-then-use as a scalar still yields content (the inversion guard).** `read` now returns a `file` (was lazy content). So `set %c% = read file.txt` then `write out %c%` / `%c%` interpolated into a string must still emit the file's **content**, not the location `"file.txt"`. Proves the `.`/scalar → content forward survives the `read X → file` inversion — the regression a `read`-then-use goal would otherwise hit.

## What these cuts do not cover (the matrix picks these up)

- **Per-type compare round-trips** — `date`/`time`/`datetime`/`duration` ordering, `list` lexicographic, `bool`/`binary`/`choice`/`dict` equality, `datetime`↔ISO-text coercion.
- **Door internals** — `ValueTask` completes synchronously when present; `Peek()` returns the unparsed rung; the framework-method throws (no public `.Value`/`ToRaw`).
- **The lazy param path** — `GetParameter<T>` returns a lazy `Data<T>` (getter doesn't read); the read + the resolution-error guard land at `await Param.Value()` (guard reorders **after** the await); `ToString`/`Equals`/`GetHashCode` never navigate; navigation `ValueTask`s are awaited once.
- **The gate** — a public `item`-subtype member returning CLR fails the build; `IsTruthy : @bool` passes; an `internal` plumbing member is untouched; the gated interop accessor is exempt.
- **`url` fetch** — `read %url%` materialises over http; `%url!host%`/`%url!path%` without fetch.
- **The renames** — `Peek()` behaves as the old `ScalarValue`; golden-diff `Diff` still diffs.
- **The no-`ToRaw` migration** — Pile-2 sites compare/serialize via typed methods, not raw.

The "no `Type.Name` switch / reuses the existing routing / each type decides" properties are architectural invariants — a code-review assertion (codeanalyzer), not a test row.
