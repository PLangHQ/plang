# codeanalyzer — plang-types

**Version:** v1
**Verdict:** PASS (2 minor findings)
**Next bot:** tester

## Scope

Reviewed the 7 stages on `plang-types` (commits `205063c5..b28282ee4`) plus the
final test/cuts cleanup commit. Focus: OBP shape (registry fold, kinds noun,
renderers noun, number/image/code/datetime/duration types, path serializer
migration), the typed-value wire path (`Normalize` → `TypedValueNode` →
`renderers.Of` → `IWriter`), runtime DLL loading (Stage 7 `Loader`), and
math handler retypes.

Built clean (PlangConsole + PLang.Tests) from a wiped artifact tree.

## Verification

- Clean build: `0 Error(s)` on PlangConsole and PLang.Tests.
- C# tests: `3609 / 3609` passing (the 11 integration-cut stubs noted in
  coder v1 are completed in commit `b28282ee4`; cuts 2/3 now green).
- System.IO ban (PLNG002): no new reaches outside `app.types.path.**`.
  `Loader.cs` uses `Reflection`, never `System.IO`. Existing exemptions
  (`PathHelper`, `channel/stream`, `System.IO.Compression` for transport
  bytes) unchanged.
- Console.* ban: clean. Two new occurrences are *string literals* in
  `code/this.Parse.cs` (heuristic detection of `"Console.WriteLine"` in
  source-string input) and `code/this.cs` (`Example` text) — no actual writes.
- `[PlangType("name")]` derivability rule: no new redundant named uses inside
  `PLang/app/`. The two named uses on `TestFixtures/TypeProvider/Money.cs`
  (`Money`/`CustomInt`) are intentional test-fixture overrides — `CustomInt`
  needs the explicit name (`customint` ≠ `int`); `Money` is intentionally
  explicit for runtime-loader contract clarity.
- Naked-T-leaf rule: every leaf I traced returns `Data` / `Data<T>`. The
  `number.Arithmetic` family wraps in `Data<@this>` via `Wrap(Func<@this>)`;
  the catch surfaces `MathOverflow` / `DivideByZero` / `ArithmeticError`
  through typed `ServiceError` rather than throw.

## Findings

### 1. `renderers.this.IndexAssembly` — dead first-assignment + dead Convert.ChangeType branch (Minor)

File: `PLang/app/types/renderers/this.cs:118–128`

```csharp
Write del = (value, writer) =>
{
    method.Invoke(null, new[] { System.Convert.ChangeType(value, valueParamType) ?? value, writer });
};
// Fast-path: value type is object → no Convert needed.
if (valueParamType == typeof(object))
    del = (value, writer) => method.Invoke(null, new[] { value, writer });
else
    del = (value, writer) => method.Invoke(null, new object?[] { value, writer });
```

The initial `del` assignment is overwritten unconditionally by the
`if/else` immediately following it — neither branch goes through the
`Convert.ChangeType` path. Effects:

- The `Convert.ChangeType` line is unreachable. If a future maintainer
  reads it as the "non-fast-path" implementation they will conclude
  conversion-on-invoke is in place when it isn't.
- Both `if` and `else` produce the same runtime behavior aside from one
  using `new[] { … }` (inferred `object[]`) vs `new object?[] { … }`.
  The intent gap should be either deleted or restored.

Recommendation: drop the dead initial assignment and collapse the
`if/else` to a single delegate — the reflection invoke already handles
the cast through STJ's parameter binding. If the original
`Convert.ChangeType` path was meant to bridge primitive widening for
runtime-registered renderers, restore it as the else-branch and pin it
with a test.

This does **not** affect today's behavior — every shipped serializer
declares its value parameter as the concrete type (or `object` for the
runtime renderer surface), and reflection invocation already handles
the cast — so the build-pass-with-tests result is genuine. Filed as
Minor because it's a maintenance footgun rather than a runtime bug.

### 2. `number.DoDivide` — dead conditional that signals intent mismatch (Minor)

File: `PLang/app/types/number/this.Arithmetic.cs:149–152`

```csharp
if (kind == NumberKind.Int || kind == NumberKind.Long)
    kind = policy.Precision == PrecisionMode.Decimal
        ? NumberKind.Decimal
        : NumberKind.Decimal; // lenient: prefer Decimal for 7/2 → 3.5
```

Both arms return `NumberKind.Decimal`. Reading the conditional, a future
maintainer expects divide under `PrecisionMode.Double` to leave the integer
track as Double, but the code unconditionally goes to Decimal. The
behavior is intentional per the docstring at the top of the file
("Divide leaves the integer track — `7 / 2 → 3.5` as Decimal (lenient)
or Double (when precision is Double-leaning)") — but the implementation
contradicts the second clause. The tests in `NumberDivideTests` cement
the always-Decimal behavior (`Divide_IntByInt_LeavesIntegerTrack_KindDecimal`
asserts Decimal under `PPolicy.Lenient`, which carries
`Precision = Double`).

Two possible fixes — coder picks:

1. Collapse the conditional and update the docstring to say "Divide on
   integer kinds always promotes to Decimal regardless of policy" — the
   policy axis only matters for Decimal × Double mixes (which the
   `kind switch` below already handles correctly).
2. Restore the Double arm and add a test that
   `Divide(int, int, Lenient).Kind == Double`.

This is **not a runtime bug today** — tests are green and reflect the
intent the rest of the code implements. Filed as Minor because the
conditional smells like "varies on policy" while doing the opposite,
which fails the "Don't explain WHAT the code does, since well-named
identifiers already do that" reading test the next time someone tunes
policy.

## Notes (not findings)

- **`number.IConvertible.GetAwaiter().GetResult()`** at
  `PLang/app/types/number/this.IConvertible.cs:22,46`. Because `number`'s
  `AsBooleanAsync()` returns `Task.FromResult(...)` synchronously and the
  class is `sealed`, this is not a real sync-over-async hazard — the
  Task is always pre-completed. Worth one comment line on each call site
  pointing at the sealed-sync guarantee so future "look, GetResult, fix
  it" sweeps don't break the IConvertible bridge.
- **`path.serializer.Default` no-context fallback to `value.Absolute`** at
  `PLang/app/types/path/serializer/Default.cs:31`. Inside the `app.types.path.**`
  exemption envelope, so the rule allows it. When `value.Context == null`
  AND `value.Raw` is empty (an unusual but possible combination — bare
  `FilePath` constructed in test fixtures) this writes the absolute
  filesystem path to the wire. Today's only such path is
  `value.Absolute` ≈ `value.Raw` because the test fixtures construct
  with a raw input that resolves through `AbsolutePath`. Worth a comment
  noting the security-adjacent assumption ("`value.Raw` empty here means
  the path was constructed in-process — there's no untrusted input that
  could exfiltrate filesystem layout"), but no code change.
- **`kinds.@this.Of` swallows hook exceptions silently.** The "Never throws"
  contract is documented at the type level. The cost is that a broken
  Build hook (returns the wrong shape or throws an unexpected exception)
  silently produces no kind — Data is still emitted, just without the
  refinement. Worth knowing for the tester pass: a Build hook that
  silently fails won't surface a stamp on the wire either.
- **`Loader.cs` file naming.** Per the OBP file-naming rule
  (`<Type><Role>.cs` smell), the better OBP shape would be `loader/this.cs`
  or fold the verb into `types.@this.Load(assembly)`. Today's `Loader.cs`
  is a single static class with one public method (`Register`) plus a
  `Result` record — small enough that the smell is mild. Mentioned in
  case the auditor wants to canonicalize.

## What's next

Tester. Worth-pinning ideas surfaced during review:

- A roundtrip test for the runtime `TypeProvider.dll` Money + CustomInt
  path covering both name-resolution precedence AND renderer dispatch
  (the runtime-wins rule applies to both tables).
- A test that `Divide(int, int, NumberPolicy { Precision = Double })`
  returns `Kind` matching the policy axis OR a test that asserts policy
  is intentionally ignored on the integer-track divide — pinning the
  intent of finding #2 either way.
- A renderer that takes `object` parameter via runtime registration —
  exercises whether the `IndexAssembly` collapsed `if/else` works as
  intended for object-typed Write signatures (finding #1).
