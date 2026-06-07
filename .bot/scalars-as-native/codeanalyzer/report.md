# Code Analyzer — `scalars-as-native` v1 — NEEDS WORK (FAIL)

**Verdict: FAIL (not mergeable).** Next bot: **tester**.

The *production code* is sound — clean OBP, correct architecture, and genuinely
proven at the C# unit level (70 dedicated wrapper tests, all green). I found no
production-code correctness defect. **The blocker is verification honesty:** the
branch's own PLang acceptance suite is hollow, and the headline claim "both suites
100% green / 272/272" silently excludes 35 stale entries that are the feature's
end-to-end proof. That misrepresentation, not a code bug, is why this can't merge yet.

HEAD reviewed: `a904ec7a7`. Clean rebuild from scratch.

## Suites (what actually ran)
- **Build:** 0 errors (`where T : item` constraint ON).
- **C# suite:** 4165 / 4165 — includes `PLang.Tests/App/ScalarsAsNative/` (70 `[Test]`
  methods across Bool/Text/Date/Time/DateTime/Duration/Null/Item-apex/Item-constraint/
  Coercion/ScalarComparer-collapse/Construction). The wrappers ARE unit-proven.
- **PLang suite:** `309 total, 272 pass, 0 fail, 35 stale, 2 skipped`. 0 fail is real
  — but read the 35 stale below before trusting "272/272 green".

## F1 — BLOCKER (major): the feature's PLang acceptance suite is hollow; the green is misleading
The 35 "stale" PLang tests are **the entire `Tests/ScalarsAsNative/` suite** — every
Stage1–7 acceptance test for this branch. Stale = no built `.pr`, so they never execute
and are excluded from the 272 count. Breakdown of the 36 authored test goals:

- **30 of 36 are stubs** — body is a comment plus `- throw "not implemented"`
  (e.g. `Stage5/IfBoolTruthy`, `Stage5/BoolBornNative`, `Stage2/TextLengthOp`,
  `Stage6/NullValueIsSingleton`, all of Stage4/Stage7, …). They assert nothing.
- **6 have real steps**, but **only 1** (`Stage1/DictIsItemKeepsNoOrder`) is built/committed
  and runs (green). The other 5 (`DateIsDateNotDatetime`, `TimeIsTimeNotUnhandled`,
  `NumberArithmeticUnchanged`, `JsonReadIsItemUntilTouch`, `ListIsItemSortsAsBefore`)
  have no committed `.pr` → stale → never run.

Evidence: `git ls-files Tests/ScalarsAsNative/**` shows **36 `.test.goal` but only 1 `.pr`**
committed (comparable dirs carry their `.pr`: `TypeKindStrict` has 9, `LazyDeserialize`
several). `plang build ScalarsAsNative` needs LLM compilation and was never completed+committed.

**Why this is a blocker, not polish:** the coder v3 report ("PLang runtime suite: 272/272
green") and `test-designer/verdict.json` (`{"pass": true}`) read as "feature fully proven
end-to-end." It is not. The end-to-end wiring the thesis rests on — `set %b% = true` flows
`bool.@this`, `if %b%` truthy through the real condition pipeline, `%s.length%` via the
wrapper op, null singleton round-trip, date≠datetime at runtime — is asserted only by
stubs that throw. There IS incidental end-to-end coverage from other green PLang tests
(`TypeKindStrict/SetIntLiteralIsNumberInt`, `SetAsTextUppercase`, the `LazyDeserialize`
navigation/csv tests), but the branch's *own* acceptance gate is empty.

**Required before merge (tester):** implement the 30 stubs, build all 6 real tests,
commit their `.pr`, and the runner must show them green (≈307/307, not 272/309 with 35
stale). The "both suites green" claim must be restated honestly. This is the tester's job
— the production code is sound, so I'm not bouncing to coder — but the branch must not
merge with a hollow acceptance suite presented as green.

## What I verified in the production code (all sound)
- **`item.@this` apex** (`type/item/this.cs`): storage-free, carries only truthiness +
  lazy narrow; ordering/equality stay opt-in interfaces. `dict : item` correctly inherits
  no order. Clean.
- **Double-wrap killed structurally** (the strongest payoff): base `data.@this` is NOT
  `: item`; `data.@this<T> where T : item`. Therefore `T` can never be a `Data` →
  `Data<Data>` is uncompilable, and the historical `Data<object>`-implicit-operator footgun
  is gone (object isn't item). Verified at `data/this.cs:25,1486,1550`. Build compiles with
  the constraint = compiler census confirms no `Data<rawCLR>`/`Data<object>` slot survives.
- **Leaf serialization dispatch** (`channel/serializer/json/writer.cs:142`): one
  `case item.@this leaf when leaf.IsLeaf: leaf.Write(this)` covers every scalar; the writer
  never type-switches per scalar (OBP Rule 9). `IWriter` has every bare-render method.
- **Coercion mediator rewritten for wrappers** (`condition/Operator.NormalizeTypes`):
  inspects `text`/`number` wrappers (tolerating raw at perimeter); text↔number and enum↔text
  only. Numeric widening correctly stays in number's own tower, not here.
- **`ScalarComparer` collapsed** (69 lines): per-type arms gone; only numeric + string +
  a thin same-typed `IComparable` fallback (bool correctly excluded as equality-only).
- **Construction born-native** (`UnwrapJsonElement`): String→text, Number→number,
  True/False→`bool.@this`, Null/Undefined→`null.@this.Instance` singleton. Load-bearing seam correct.
- **`text` atomicity:** `text.@this` is not `IEnumerable`; `IsPlangIterable` still carves
  out raw `string`. `foreach %text%` will not char-iterate. Correct.
- **On-error chain reconstruction** (`step/actions/this.Convert.cs` + `action/this.FromWire.cs`):
  action owns its `FromWire` (read-side mirror of `AsData`), rebuilds field-by-field via
  `FromWireShape` (marker-free), named factory not a catalog hook so it doesn't hijack the
  generic `list<action>` path. OBP-clean.
- **`variable.set` `as <type>` + `TypeFromWire`**: dict→TypeFromWire / bare-name→FromName
  split is correct; the `@bool`/`strict` unwrap (`data/this.cs:797`) is a real fix
  (a wrapped `true` would otherwise read false).
- Bans: no new `Console.*` or `System.IO.*` reaches in the branch's production C#.

## Minor findings (non-blocking; tester/coder may fold in)
- **F2 (minor, latent): `text` op index-unit inconsistency.** `text.Length` counts
  codepoints (`EnumerateRunes`) but `Substring`/`IndexOf`/`Replace` operate on UTF-16
  char units. For non-BMP input the Length value doesn't match the index space its own
  ops use. Currently *unconsumed* in production and `TextWrapperTests` only exercises
  ASCII ("hello"), so it's latent — but it's a real internal inconsistency to settle
  before these ops get wired to the PLang surface (pick codepoint or UTF-16 throughout).
- **F3 (minor, OBP): `object`/`item` serializer ownership inversion.** Plan says "no
  enduring PLang `object` type," yet `type/object/serializer/json.cs` remains the canonical
  json reader and `type/item/serializer/json.cs` *delegates to it*. The folded-away type
  hosts the live code while the surviving type forwards. Cosmetic, but the implementation
  should live under `item` with `object` gone (or a one-line note on why the `object` reader
  is retained).
- **F4 (minor, edge): `Compare` null-coalesce asymmetry.** `Compare.Order` coalesces the
  `null.@this` singleton to C# null for the sort-last policy (`Compare.cs:42-43`);
  `AreEqualValues` does not and relies on `null.@this.AreEqual`. A raw C# null vs the
  singleton can compare equal in one direction but not the other. Edge-only (born-native
  always carries the singleton), but worth one regression test.

## Verification method
Clean rebuild (`rm -rf bin/obj` across all projects, `dotnet build PlangConsole` → 0 err).
C# suite via `dotnet run --project PLang.Tests` → 4165/4165. PLang suite via
`cd Tests && plang --test` → 272 pass / 0 fail / 35 stale. Inspected the 35 stale by
git-tracking census (`.test.goal` vs `.pr` counts) and reading the stub bodies directly.
Read the apex, all seven wrappers, Compare/ScalarComparer, NormalizeTypes, the json writer
leaf dispatch, construction seam, FromWire reconstruction, and the constraint declaration.

— codeanalyzer
