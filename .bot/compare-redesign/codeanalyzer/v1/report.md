# codeanalyzer v1 — `compare-redesign`

**Verdict: NEEDS-FIX (FAIL)** → next bot: **coder**

Build: `dotnet build PlangConsole` → **0 errors** (611 pre-existing warnings, unchanged).
Branch production diff vs `template-stamping-at-read` base: **clean** — no findings.
Headline finding is a **pre-existing systemic swallow** in the catalog conversion
path that the user flagged directly (`var (convEl, _) = TryConvert(...)`), asked
to be caught, and full-scanned for. It is reported here as the blocking item.

---

## Scope

Two scopes, deliberately separated:

1. **Branch diff** (the 4 production C# files changed on `compare-redesign`):
   `Wire.cs`, `channel/serializer/plang/this.cs`, `data/Comparison.cs`,
   `module/condition/Operator.cs`. Reviewed in full — **clean** (see §C).
2. **Full-codebase swallowed-error scan** (the user's explicit ask): every site
   that discards an `Error`/`IError` returned alongside a value. **F1–F3 below.**

---

## F1 — MAJOR (systemic): catalog `TryConvert` errors discarded across the conversion read/write path

`TryConvert` returns `(object? Value, error.Error? Error)`. Its *entire reason to
exist* over the `ConvertTo` convenience overload is that second slot — a rich,
slot-named, chain-capable Error. Yet a whole family of runtime call sites throw it
away with `var (x, _) =`. The effect is never "nothing happens"; it is one of:
silent type-violation, silent null, silent element-drop, or a downstream
*uninformative reflection exception* that buries the honest error TryConvert
already built.

The same file proves the correct shape exists: the non-generic `IList` arm
(`Conversion.cs:381–409`) **aggregates** per-element errors into a
`ListConversionFailed` carrying an `ErrorChain`. And `variable/set.cs:63` +
`builder/validateResponse.cs:214` capture `(_, error)` and surface it. So this is
not "we don't have a pattern" — it is the same method/codebase contradicting
itself across sibling arms. Per review discipline, *same-shape-second-site with a
correct sibling = FAIL*, even with green tests.

### The swallow sites

| # | Site | Failure behavior | Severity |
|---|------|------------------|----------|
| a | `app/type/catalog/Conversion.cs:229` — `list<T>` arm | On element-convert failure inserts the **unconverted** `row.Peek()` into the typed list. A `list<number>` can silently hold strings — the typed-list element invariant is violated with no error. **Worst of the set: it doesn't drop, it pollutes.** | MAJOR |
| b | `Conversion.cs:361` — `JsonElement[]` arm | `if (convertedElem != null) targetList.Add(...)` → failed elements silently **dropped**; result list shorter than source, no error. | MAJOR |
| c | `Conversion.cs:375` — `JsonArray` arm | Same silent drop as (b). | MAJOR |
| d | `app/type/dict/this.cs:190` — `Clr<T>()` typed accessor | `var (converted, _) = TryConvert(...); return converted as T;` → on failure returns **null**; the "why" is lost. | MEDIUM |
| e | `app/variable/list/this.cs:420, 439, 457` — navigator typed writes | On failure keeps the **original wrong-typed value**, then `list[idx]=` / `indexer.SetValue` / `clrProp.SetValue` throws an **uninformative reflection `ArgumentException`** instead of TryConvert's slot-named error. Error swallowed *and replaced by a worse one*. | MEDIUM |
| f | `app/variable/list/this.cs:534` | `return typedValue ?? value;` → returns original on failure, swallowing. | MEDIUM |

### Why this is the bug the user pointed at

> `var (convEl, _) = TryConvert(row.Peek(), elemType, context);`
> `typed.Add(new data.@this("", convEl ?? row.Peek()));`

`convEl ?? row.Peek()` is the tell: when conversion fails, `convEl` is null and the
**raw, unconverted** value is added to a `list<T>`. The list now lies about its
element type and nothing reports it. This is a correctness/safety defect in a core
type-system primitive, independent of which branch introduced it.

### Fix direction (for coder, not prescriptive)

Make these sites propagate rather than discard:
- **(a)** mirror the `IList` arm (381–409): accumulate per-element errors, return a
  `ListConversionFailed` with the element errors on `ErrorChain`. Do **not**
  `?? row.Peek()` an unconverted value into a typed slot.
- **(b)(c)** same — aggregate, don't silently drop.
- **(d)(e)(f)** thread the Error out of `Set`/`Clr<T>` so a typed-write failure
  surfaces the slot-named message instead of a null or a reflection throw. The
  `variable/set.cs:63` capture is the template.

---

## F2 — MINOR: build-pass discards `GetCodeGenerated` error

`app/module/builder/code/Default.cs:608`
```csharp
var (handler, _) = modules.GetCodeGenerated(a);   // returns (ICodeGenerated?, IError?)
if (handler is not global::app.module.IClass classified) continue;
```
A module-resolution failure during `RunBuildPass` is discarded and the action is
silently `continue`d, so the build pass can report success while having skipped an
action it couldn't resolve. The error should land in the `errors` list this method
already accumulates.

---

## F3 — MINOR: `%MyIdentity%` resolution discards `Code.Get` error

`app/actor/this.cs:127`
```csharp
var (idProvider, _) = app.Code.Get<IIdentity>();   // returns (T?, IError?)
if (idProvider == null) return null;
```
A failure to resolve the identity provider collapses to a null `%MyIdentity%` with
no diagnostic. Lazy `DynamicData`, low blast radius, but the reason is lost. Low
priority; flagged for completeness so the swallow class is reported whole.

---

## C — Cleared (verified non-issues)

So a re-review doesn't re-flag these:

- **Branch diff is clean.** `Wire.cs` = the already-security-reviewed fail-closed
  hardening (context-less transport read of a signature layer now returns
  `SignatureVerifyContextMissing` instead of peeling unverified; Store reads
  trusted on read). `serializer/plang/this.cs` = doc-comment update to match.
  `Operator.cs` = deletes dead `BothPresent` (the F1 cleanup from the prior
  branch). `Comparison.cs` = moves the `IncomparableException` declaration above
  the enum (cosmetic). No new swallow introduced on the branch.
- **`OwnerOf` discards** (`type/this.cs:261`, `data/this.cs:248`,
  `variable/set.cs:379`) — second slot is `kind`, **not** an error. Fine.
- **`ReadErrorResponseAsync` discards** (`http/code/Default.cs:172, 656`) — second
  slot is `Body`; the **error is the captured slot**. Fine.
- **`settings/Sqlite.cs:197` `Load<T>`** — paired value/diagnostic; load path
  handles the null. Not in the conversion-invariant class.
- **`JsonString.cs` empty `catch (JsonException) {}`** (7×) — a staged
  JSON-repair probe; each stage falls through to the next and the method returns an
  **aggregated** error at the end. Correct probe pattern, not a swallow.
- **`Conversion.cs:342` inner catch** — intentional array-fallback fallthrough,
  already commented.
- **`Conversion.cs:117` `ConvertTo`** — the swallow is its **documented contract**
  ("Returns null on failure — use TryConvert for error details"). Acceptable as a
  convenience overload; noted only because `Populate` (127) and `dict.Clr` ride it,
  so callers needing diagnostics inherit the silence — covered by the F1(d) fix.

---

## Lesson logged

The miss: a `var (x, _) =` that discards an `Error`-typed tuple slot is a
swallowed-error site and must be triaged every review — **especially** when a
sibling arm in the *same method* handles the same error properly (the tell that the
discard is an oversight, not a contract). Recorded to analyzer memory.

---

## What this is NOT

Not a certification of the whole conversion subsystem's behavior — it is a targeted
swallowed-error scan plus a full read of the branch diff. The F1 fix is code
authoring → **coder**.
