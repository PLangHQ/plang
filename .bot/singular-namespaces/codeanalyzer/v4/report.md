# Code Analyzer — v4 report (singular-namespaces)

**Scope:** coder v2 (`c9d39b093`) "fundamental changes" responding to tester v1.
Diff range `30aa4db46..HEAD` (since my v3 PASS).

**Method:** read the full production diff; traced the non-null invariant through every
stripped guard; re-checked the cache-build interaction my v3 PASS depended on; rebuilt clean
and ran the affected C# subsets myself (tester v1 found false-greens — I confirmed coder v2
fixed them rather than re-greening).

---

## Headline

The fundamentals are sound and the tests are now honest. The four big moves —
non-null `Data.Context`, the `type.Null` sentinel, the `Promote()` throw, and producer
stamping — are internally consistent and the suite's green is real (verified, not claimed).
Findings are minor/latent robustness items; **no blockers**.

### Green is real (I ran it)
- Clean `dotnet build PlangConsole` → **0 errors** (254 pre-existing warnings, none new from this diff).
- `BuilderSchema*` golden → **2/2** (the F2 fix is a genuine SHA256 compare with length guards, not a tautology).
- `NullabilityTests` → **7/7** (F1 rewrites assert the architect's spec direction).
- `DataTests` → **310/310** (Null-sentinel paths).

---

## The one interaction worth confirming — and it's safe

My v3 PASS said the cache build was safe *because `Promote()` short-circuits when
`Context == null`*. Coder v2 **changed `Promote()` to throw** in exactly that case
(`type/this.cs:168`). If nothing else had changed, cache build (`type/list/this.cs:174`,
`BuildTypeEntries(null)` with no Context) would now throw the moment `Rank()` reads a
barren entry's `Fields`.

It doesn't, because the safety now rides on a different mechanism:

- Every entry `BuildTypeEntries` adds uses the **2-arg ctor** `new app.type.@this(typeName, type)`
  (`list/this.cs:565,616,630`), which sets `_foldLoaded = true` (`type/this.cs:147`).
- `Promote()` returns at line 152 (`if (_foldLoaded) return this;`) **before** the Context
  check. So `Rank()` → `Fields` → `Promote()` → early return. No throw.
- The barren `Rank()==0` branch (`list/this.cs:203`) is in fact unreachable for catalog
  entries — every added entry carries Values, Shape (≥`"string"`), or Fields — so it's
  defensively dead but harmless.

**Verified:** clean rebuild + golden/nullability/data subsets all green. The v3 safety
claim still holds, just via `_foldLoaded` instead of the (now-removed) null short-circuit.
Good that the coder set `_foldLoaded=true` in the 2-arg ctor *as part of this change* —
without that line this would have been a cache-build crash.

---

## Findings (all minor / latent — none block PASS)

### F1 — `type.IsNull` is string-magic, not identity  *(recommend fix)*
`PLang/app/type/this.cs:69`
```csharp
public static @this Null { get; } = new("null", typeof(object));
public bool IsNull => Value == "null";
```
`Null` is a singleton and the `Data.Type` getter always hands back *that* instance for the
no-type case (`data/this.cs:230 return type.Null`). So the sentinel has a stable identity —
but `IsNull` detects it by the magic string `"null"` instead of `ReferenceEquals(this, Null)`.

Two consumers branch on this (`Wire.cs:391` skip-emit, `data/this.cs:243` setter "clear on
Null"), so any entity whose `Value == "null"` is silently treated as the sentinel. The
`@this(string)` ctor is public, so `new type("null")` anywhere — or a user writing
`type=null` — collapses to the sentinel: dropped from the wire, cleared on copy.

This is the character's Pass-4.5 tell #4 (special-casing by literal name) and the Pass-4
fragility "key-name-based detection breaks with user data." The robust form is free and
exact, because the getter only ever yields the singleton:
```csharp
public bool IsNull => ReferenceEquals(this, Null);
```
No user-named `"null"` type exists today, so this is **latent**, not an active bug — but it's
a real footgun and the fix is one line. Recommend it; not a blocker.

### F2 — test name overpromises (same shape tester flagged in F2/F4)  *(minor)*
`PLang.Tests/.../NullabilityTests/NonNullInvariantTests.cs` —
`DataType_OnUnstampedData_ThrowsHard_NoSilentFallback`. The body asserts
`d.Type!.ClrType` **`.IsNull()`** — nothing throws. `ClrType` legitimately returns null for
an unknown domain name with no Context; that's correct behavior and a real assertion, but the
name says "ThrowsHard" when the test proves "returns null." This is the exact "a test that
*names* an invariant it never checks" pattern the tester caught in F2/F4. Rename to
`…_ReturnsNull_NoSilentFallback`. The assertion itself is honest, so this is cosmetic.

### F3 — `As(string typeName)` fallback-drop contradicts the ValidateBuild reasoning  *(latent)*
`PLang/app/data/this.cs:444`
```csharp
context = context ?? _context!;
var clr = context.App.Type.Clr(typeName);   // dropped: ?? AppTypes.GetPrimitiveOrMime(typeName)
```
In the *same commit* the coder deliberately routed `variable/set.ValidateBuild` and
`Sqlite.RehydrateValue` **through** `data.Type.ClrType` specifically to keep the no-context
primitive fallback ("falls back … when Context is null e.g. build-time validation paths and
unit tests"). Here the opposite was done — fallback removed, context hard-required via `!`.
The two treatments of "resolve a type name without guaranteed Context" disagree.

Severity is low because this `As(string,…)` overload has **no production caller** today
(grep: only the internal error string references it). But if one ever appears in a
null-context path, it NREs where the old code resolved a primitive. For consistency, either
restore `?? AppTypes.GetPrimitiveOrMime(typeName)` or document that this overload requires a
stamped Data. Pass-4.5 tell #14 (asymmetry in paired operations).

### F4 — `Scheme` getter lost its null-safety  *(minor)*
`PLang/app/type/this.cs:137`
```csharp
=> Value == "path" ? Context.App.Type.Scheme : null;   // was Context?.App?.Type?.Scheme
```
An unstamped `path`-named entity reading `.Scheme` now NREs instead of returning null. Same
fail-loud class as the Promote throw, but without Promote's helpful "producer forgot to stamp"
message — a bare NRE. All three readers (`app/this.cs:323`, `path/this.cs:87`,
`Conversion.cs:257`) run on stamped entities today, so latent. If you want the fail-loud
behavior here too, mirror Promote's explicit `InvalidOperationException` with a producer-bug
message rather than letting it surface as a raw null-deref.

---

## What's genuinely good (Pass 4.5 — root-cause, not symptom)

- **Producer stamping is the right level.** `Permission.Find` stamping Context on
  SQLite-rehydrated grants (`permission/this.cs:67`) fixes the producer, not the consumers —
  the textbook root-cause shape. Same for routing `RehydrateValue`/`ValidateBuild` through
  the entity's own `ClrType` resolver instead of each consumer re-chaining `?? GetPrimitiveOrMime`.
  The `GetPrimitiveOrMime_ExternalFallbackCallSites_AllRemoved` test even pins that the
  consumer chains stay gone.
- **The Promote throw is fail-loud-at-source, not a workaround.** It converts a silent
  empty-fold (which surfaced as wrong LLM prompts far away) into an exception naming the
  producer bug. Correct direction.
- **F1 nullability tests are honestly flipped.** They now assert non-null back-refs and
  "no silent fallback" (the architect's spec), with the comment re-grounded as
  "Ingi, post-tester-v1: producers always stamp." The earlier inverted "Per Ingi" framing
  the tester flagged is gone; the tests match the recorded design again.
  *(Process note: I can confirm the tests now agree with the architect's spec; I can't
  independently confirm a separate Ingi ruling occurred — but since the direction now matches
  the original design rather than contradicting it, the tester's core complaint is resolved.)*
- **F2 golden is a real gate** — SHA256 of `ToJson(indent:false)` + `TypeSchemas`, with
  `>1000` / `>100` length guards so a hash-of-empty can't pass.

---

## Verdict: **PASS**

The fundamental non-null / sentinel / fail-loud reshape is sound, the cache-build interaction
my v3 PASS depended on is still safe (now via `_foldLoaded`), the build is clean, and the
test green is real (verified). Four minor/latent notes for the coder; **F1 (`ReferenceEquals`
for the Null sentinel) is the one worth doing** — one line, kills a literal-name footgun.
None block PASS.
