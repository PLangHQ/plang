# codeanalyzer v3 — typed-action-returns (v2 follow-up)

**Scope:** Production-code diff `117b754f3..HEAD` (4 commits). Re-checks every v2
finding plus the spill-over rename (`Mock.ActionPattern → Pattern`) and the
`goal/getTypes.cs` precedence swap.

**Verdict at a glance:** **PASS.** All actionable v2 findings closed. One minor
stale-comment carry-over (same family as v2 F10/F11) and one behavioral note on
the new `file.read.Build()` gate worth recording but not blocking.

---

## v2 finding sweep

| ID | Status | Notes |
|----|--------|-------|
| F1 — `Run.File` → `Run.Test` | ✅ Closed | `tester/Run.cs` property renamed; all 8 callers in `test/report.cs` updated; constructor parameter renamed; docstring on `UserTags` updated. |
| F2 — mock action triple-mismatch | ✅ Closed | `action.cs` → `intercept.cs`, class `MockAction` → `intercept`. Spill-over: `Mock.@this.ActionPattern` → `Pattern` (verify.cs follows). The wider rename is consistent — Mock's pattern was always going to be called `Pattern` once the action was `intercept`. |
| F3 — HTTP leaf helpers throw | ✅ Closed | `ReadLimitedBytesAsync` now returns `Data<byte[]>` with `ResponseTooLarge`/`SlowResponse` keys at the leaf. `CreateFileContentAsync` and `CreateFormContentAsync` return `Data<HttpContent>`. `ResolveUploadContentAsync` and the upload entry point thread the result through. |
| F4 — `ReadLimitedString`/`Bytes` duplicate | ✅ Closed | One implementation (`ReadLimitedBytesAsync`); `ReadLimitedStringAsync` is a 3-line UTF-8 wrapper. Slow-loris/size-cap discipline lives in one spot. |
| F5 — bare `catch (System.Exception)` | ✅ Closed | `file/read.cs:79` narrowed to the project-standard `when (ex is not (NullReferenceException or OutOfMemoryException or StackOverflowException))`. |
| F6 — application/plang serializer bypass | ✅ Closed | Option (a) taken: docstring on `ParsePlangResponseAsync` explains why this transport doesn't go through `Serializers.GetByContentType` (signature inflow via `[In]`, transport-specific options). NDJSON path covered by same note. |
| F7 — `Ask.ToString()` leakage | ✅ Closed | Docstring note added explicitly: "ToString() leaks the user's answer; do not use an `Ask` value in diagnostic / log paths." Behavior unchanged; readers warned. |
| F8 — Type-slot-any-value as hint | ✅ Closed (won't-fix, documented) | Architect's call: surface-pattern detector can't distinguish "LLM forgot Type" from legitimate translations of natural-language type hints. Catch point is behavioral, not compile-time. |
| F9 — discarded `err` | ✅ Closed | `(handler, _)` discard pattern. |
| F10 — `test.File.Path` stale comment | ✅ Closed | Comment now reads `test.Goal.Path / PrPath`. |
| F11 — `File.Goal is never null` stale comment | ✅ Closed | Comment now reads `Test.Goal is never null`. |

---

## New observations from the diff

### N1 — `modules/goal/getTypes.cs:45-46, 98, 128` — `%__data__%` comments now stale (READABILITY, TRIVIAL)

The code switched from matching the literal `%__data__%` to the project-standard
`%!data%` (line 88). The docstring at line 18 was updated. Three sibling comments
weren't:

```csharp
// Line 45-46:
// Tracks the return type of the most recent producing action in the current step's
// action chain — needed so `variable.set Value=%__data__%` can take the prior
// action's type. Reset at the start of every step (no cross-step %__data__%).

// Line 98:
chainReturnType = null;  // variable.set consumed %__data__%

// Line 128:
// Generic action — record its return type so a following variable.set picking up
// %__data__% sees it. Reflection on the handler's Run() method handles the
```

Same readability hygiene as v2 F10/F11. Trivial edit — three `%__data__%` →
`%!data%` substitutions.

### N2 — `modules/file/read.cs:65` — registered-types gate also silences missing-file warning (BEHAVIORAL, INFO)

```csharp
if (Context.App.Types.Get(typeName) == null) return data.@this.Ok();

// missing-file warning try/catch follows
```

The gate exists to avoid stamping `Type=pdf` onto a `variable.set` when `pdf`
isn't a registered conversion target (it would surface `"Unknown type 'pdf'"` at
runtime). But because the early return precedes the missing-file warning, a
literal `read foo.dat` no longer warns when `foo.dat` doesn't exist. Before this
change, the warning fired regardless of whether the extension was registered.

**Severity:** info. The warning's purpose is catching typos in build-time
literals like `Path=missing.csv`, which still works (csv is registered as a
string alias). The cost is that misspelled extensions (`.csvv`) get *no*
feedback — neither a type-mismatch nor a missing-file warning. A future
refinement could split the gate: emit the warning unconditionally for any
literal-extension miss, then check registration only for the stamp. Not worth
fixing today; flagging so the next reader knows.

### N3 — `modules/goal/getTypes.cs:82-91` — typeParam precedence swap is now the canonical resolution path (DESIGN, INFO — not a finding)

```csharp
if (typeParam?.Value is string explicitType && !string.IsNullOrEmpty(explicitType))
{
    // Type slot wins — covers both Build()'s stamp (file.read.Build()
    // → "csv") and the user (type) hint (set %x% = {...}, type=json).
    type = explicitType;
}
else if (valueParam?.Value is string sval && string.Equals(sval, "%!data%", StringComparison.OrdinalIgnoreCase))
{
    type = chainReturnType ?? "object";
}
```

Pre-change: `%!data%` → chain return type → typeParam → default. Post-change:
typeParam → `%!data%` → chain return type → default.

The swap is intentional and architecturally sound: Build() runs at Validate and
stamps typeParam *with the canonical type already*, so by the time getTypes
runs, the typeParam slot is authoritative. The chain-return path becomes a
fallback for actions whose Build() doesn't stamp. This is the same architectural
move that F8 documents — the type slot is the truth, hint or stamp.

**No follow-up needed.** Mentioning because the comment "Type slot wins" reads
as a small phrase but represents the full hint-precedence design decision.

---

## Pass summary

- **Pass 1 (OBP rules)** — clean. The rename `ActionPattern → Pattern` on
  `Mock.@this` is owner-internal (Mock owns its own pattern); `intercept` reads
  it via `Pattern.Value!` which is the standard `Data<string>.Value`. No
  cross-file mutation/lock-target smells introduced.
- **Pass 1b (shape smells)** — clean.
- **Pass 2 (simplification)** — F4's collapse is the right shape; one helper, one
  wrapper. No new duplication.
- **Pass 3 (readability)** — N1 only (3 stale `%__data__%` comments).
- **Pass 4 (behavioral)** — N2 (missing-file warning surface slightly reduced;
  info-level).
- **Pass 5 (deletion test)** — `ResolveUploadContentAsync` returning
  `Data<HttpContent>`: deleting the `.Ok(...)` wrappers around the
  non-File switch arms would compile-fail (raw HttpContent into
  Data<HttpContent>'s switch result). Each `.Ok(...)` earns its place. The
  `if (!contentResult.Success) return contentResult;` early-out at line 199
  returns `Data<HttpContent>` from a lambda typed `Task<Data>` — valid because
  `Data<T> : Data`, and the error carries through.
- **Leaf-Data** — HTTP body cap, slow-loris, file-content read, form-content
  read all return Data at the leaf now. No throw-as-control-flow inside the
  Data-returning module surface.
- **Data<T> footgun** — `ResolveUploadContentAsync` returns `Data<HttpContent>`
  via `.Ok(...)` in every arm (owned construction) and `await
  CreateFileContentAsync(...)` (forwarding a Data envelope of the same T).
  The early-out `return contentResult` from inside the `UploadAsync` lambda is
  forwarding a `Data<HttpContent>` *up the stack* — the lambda's declared
  return is `Task<Data>` (base), so the implicit-operator footgun doesn't fire
  (there is no `Data<object>` wrapping). Clean.

## Files inspected

`PLang/app/mock/Mock/this.cs`, `PLang/app/modules/builder/code/Default.cs`,
`PLang/app/modules/file/read.cs`, `PLang/app/modules/goal/getTypes.cs`,
`PLang/app/modules/http/code/Default.cs`, `PLang/app/modules/mock/intercept.cs`,
`PLang/app/modules/mock/verify.cs`, `PLang/app/modules/output/ask.cs`,
`PLang/app/modules/test/discover.cs`, `PLang/app/modules/test/report.cs`,
`PLang/app/modules/test/run.cs`, `PLang/app/tester/Run.cs`,
`PLang/app/types/this.cs`.

## Build / test posture

- `dotnet build PlangConsole`: clean (0 errors, 454 warnings — same as v2
  baseline; no new warnings introduced).
- Test suites: relying on coder handoff (PLang.Tests 3123/3123, plang --test
  208 pass / 0 fail / 12 stale).

## Verdict

**PASS.** All v2 findings closed. Two observations (N1 stale comments, N2 mild
gate side-effect) recorded for the next pass — neither blocks ship. The
typed-action-returns branch is in good shape to merge.
