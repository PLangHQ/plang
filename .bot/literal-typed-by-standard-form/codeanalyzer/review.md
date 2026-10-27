# codeanalyzer — literal-typed-by-standard-form (origin/runtime2...HEAD c3f0235ac), quick OBP review

Requested by the architect (snapshot 172–175). Pattern: 3 (a choice belongs on the element) for the typing, 5 for "where is the standard form": the value types answer `IsStandard` themselves. Read whole: `formal/reader.cs` (`LiteralType` and its callers), `date`, `datetime`, `time`, `duration`, `duration/iso` `this.cs`, `LiteralStandardFormTests.cs`; diffs of `set.notes.md`, `set.examples.md`, the goal, the `.pr` and the golden. The `.pr` and the golden were rebuilt by the builder (commits `builder:`), not by hand. No bench: the formal reader runs at build, not on a birth or request path.

## Clean
- `date`, `datetime`, `time` `IsStandard`: `TryParseExact` with one fixed set of formats each, invariant culture, `internal static` (Ingi's 172). Strict, as the doc says, and each member is on the type that owns the form.
- `duration/iso` `Read` made `internal static` (the local function lifted unchanged); `Create`'s `Measure(raw, Read)` still uses it.
- The tests pin the eight accepts and eight refusals, the annotated literal and the declared slot. The teaching edit narrowed `set` to the four forms and dropped the two examples that taught the old rule (`1st jan 2026`, `42`).

## Findings

**D1 [behavior, a silent inconsistency] `duration.IsStandard` is not strict: `iso.Read` accepts texts that are no ISO duration, and each is now typed `duration` by the build.**
`duration/iso/this.cs` `Read` (lifted at `:26–60`): `"PT"` passes (length 2, `s = "T"`, the loop sets `inTime` and ends) and answers a zero span, so a quoted `"PT"` in an open `item` slot is a `duration`, not text. Likewise a repeated unit (`"P1D1D"`, `"PT1H1H"` add up) and a `T` with nothing after it (`"P1DT"`). ISO 8601 needs at least one component after the `P`/`T`. `date`/`datetime`/`time` refuse such shapes (`TryParseExact`); `duration` is the one of the four that doesn't, though the doc (`reader.cs:443`, `duration/this.cs:56`) says "strict standard form". It was harmless while `Read` served only `as iso`, where a text the user forced is the user's; as the build's literal typing it changes what a quoted label becomes. Not covered by the tests (`P1Y` is the only refusal). Fix at the producer (`Read`, so `as iso` agrees): `null` when the text ends in `T` or `P`, or after a unit that has already been read (a unit once), with the two refusals pinned in `LiteralStandardFormTests`.

## Noted, not findings
- `LiteralType`'s four `IsStandard` calls name the four types in the formal reader (`reader.cs:447–450`): a new standard type is a fifth line here. That is the shape of Ingi's 172 (a static on each type); the type list asking "whose standard form is this text" would be the registry shape, and is his to take up with the fork-by-type-name item (snapshot 23).
- `duration.IsStandard` (base, `duration/this.cs:58`) names its subclass `iso`; the base already finds a standard by `kind[...]` in `Create` (`:87`). Low, and in the same decision.
- `LiteralType` deserializes the literal's own json text back to a string (`JsonSerializer.Deserialize<string>(v.Json)`) where the literal already read it. Build-time only; low.

## Verdict
**FAIL, one finding (D1); coder next.** Everything else is clean. Mechanical: one fix in `iso.Read` and two pins.
