# codeanalyzer v4 — path-polymorphism

**Scope:** all post-v3 work (`12f192c2e..2aab57daf`, ~30 commits):
1. **Coder v6** — slash-qualified `goal.call` resolution, inverted `File.Exists`
   bootstrap check, `builder.actions` filter param, two builder validators.
2. **Strongly-typed-returns sweep** — ~70 action handlers flipped
   `Task<Data>` → `Task<Data<T>>`; all provider interfaces typed
   (`IAssert`, `ICrypto`, `ISigning`, `IIdentity`, `IStore`, `IEvaluator`,
   `ITemplate`, `ILlm`); `IPath` verbs (`ReadBytes`, `ExistsAsync`, `List`,
   `Stat`, `Write*`, `Save`, `Append`, `Mkdir`, `Delete`, `MoveTo`, `CopyTo`)
   typed. New `Data<T>.From(@this source)` retype-without-rewrap factory.

## Verification

Build: clean (0 errors, 454 unrelated nullable warnings — pre-existing
generator output).

C# tests: **2889/2889** pass (`dotnet run --project PLang.Tests`).

PLang tests: **202/203** pass, **0 stale**. The 1 fail is
`Modules/Llm/LlmCache.test.goal` — `503 Service Unavailable` from the upstream
LLM. External-network flake, not a code regression. `Llm/LlmCache.test.goal`
(no `Modules/` prefix) passes in the same run, confirming the cache logic
itself works; the duplicate path is the same fixture mounted twice and only
the one that actually hit the network failed.

---

## PLang/app/goals/goal/GoalCall.cs

### Slash-qualified resolution (`GetGoalAsync`, lines 64–105)

Walks the caller's ancestor folders for `{folder}/.build/{leaf}.pr` before
falling back to root- and context-relative. The folder-walk is bounded (loop
condition `!string.IsNullOrEmpty(dir)`; `up > 0 ? dir[..up] : ""` always
shrinks `dir` strictly), so termination is guaranteed.

Leaf-match in `LoadFromFile` (lines 151–161) correctly strips the folder
prefix before comparing against the loaded `goal.Name` — necessary because
the saved goal's own `Name` never carries the folder prefix.

**Verdict:** clean. Per the coder v6 summary, deterministic self-rebuild
confirms zero slash-resolution failures.

### Simplifications / Readability

None worth filing — the diff is mechanical and well-commented.

---

## PLang/app/data/this.cs — `Data<T>.From(@this source)` factory

The factory's job is to forward a base `@this` as `@this<T>` without invoking
the implicit operator (which would double-wrap). The implementation:

```csharp
new @this<T>(source.Name, source.Value is T t ? t : default, source.Type) {
    Error = source.Error, …
}
```

**F1 (Low) — `From()` silently drops the value when `source.Value` is not a T.**

`source.Value is T t ? t : default` — when the source carries a non-T Value
(e.g. `Data<bool>.From(byteArrayResult)`), the new Data has `Value = null`.
At every current call site this is fine: From() is only invoked after
`if (!source.Success) return …` (or for the dedicated `From(action.Data)` in
`SignAsync` where T = object and every Value is an object). So the
value-drop only fires on errored Data whose Value was null anyway.

The risk is future maintenance: someone reading the docstring sees
"Preserves all wrapper state" and assumes a successful Data round-trips
through From() losslessly across types. It doesn't — it silently coerces
non-matching values to default.

- **Fix:** tighten the docstring to say "Intended for error/sentinel
  propagation across typed boundaries — when the source carries a successful
  Value not assignable to T, Value silently coerces to default(T?). The
  Properties dictionary is forwarded by shared reference (not deep-cloned)."
  That last clause is already true and worth surfacing.

This is a **doc-only** finding; no code change requested.

---

## PLang/app/modules/this.cs — `DescribeReturnTypeName`

Reads `Run()` return type via reflection, surfaces the PLang name of T.
Treats both bare `Task<Data>` and `Task<Data<object>>` as `"data"` (the
polymorphic default). Correct — those two are equivalent intents.

One minor:

**F2 (Low) — duplicate `<summary>` block on `DescribeReturnTypeName`** (lines
377–384). The existing summary comment on `DescribeReturnType` was left in
place above the new method's own summary, so the new method has *two*
adjacent `<summary>` XML doc blocks. Compiler warns CS1591/CS1573 on
duplicate XML, but more importantly only the first one shows up in IDE
tooltips. Trivial: delete the orphaned block at lines 377–380.

---

## PLang/app/modules/builder/code/Default.cs

### `Actions(GetActions)` filter (lines 21–40)

Optional filter on `module.action` names; null/empty → full catalog. Honest
HashSet with `OrdinalIgnoreCase`. Clean.

### File-list type change (lines 91–135)

Pivoted from `path[]` to `List<path>` to match `IPath.List`'s new typed
return (`Task<Data<List<path>>>`). All array indexers were already
list-compatible. Clean.

### `ResolveGoalCallsInAction` comment (line 912)

Comment-only. Documents that slash-qualified Names keep their prefix in the
saved .pr because `LoadFromFile` leaf-matches at dispatch. Honest.

---

## PLang/app/modules/builder/this.cs — inverted `File.Exists`

Line 110: `if (!System.IO.File.Exists(appPrPath) && !_app.Create)`. The
inversion is correct (the comment explains the prior bug). Clean.

---

## PLang/app/modules/identity/code/Default.cs — typed pipeline

Every method that previously did `(Identity)result.Value!` after a
`Task<Data>` propagates a typed `Data<Identity>` (or `Data<List<Identity>>`
for `LoadAllAsync`). The cast count dropped from ~12 to zero. `SaveAsync`
and `RemoveAsync` deliberately stay bare `Task<Data>` — they're write-only;
callers only read `.Success`/`.Error`. Honest scoping.

All cross-boundary `From()` calls are guarded by `if (!result.Success)` — no
value-loss exposure.

---

## PLang/app/types/path/file/this.Operations.cs — IPath verbs

Each verb's success Value now matches the declared T. Notable
**behavior change** worth flagging:

**Observation O1 (informational) — Write/Append/Mkdir/Delete return the path
itself instead of empty Data.**

Old:
```csharp
public override async Task<data.@this> WriteText(string content) {
    …;
    return data.@this.Ok();   // empty: Value=null
}
```

New:
```csharp
public override async Task<data.@this<path>> WriteText(string content) {
    …;
    return data.@this<path>.Ok(this);   // Value=this path
}
```

This is per the typed-returns spec ("an action that genuinely never produces
a value still returns *some* Data" — the spec settles on `data` for that
case, but for path writes the path itself is the meaningful T). Any PLang
caller that previously read the trailing `%result%` as `null` after a write
now sees the path object. C# + PLang test suites are green, so no callers
relied on the old `Value == null` shape — but it's a behavior change worth
recording because if a downstream consumer was using truthiness of the
result to detect "did the write succeed but produce nothing," that
distinction is now gone (it always carries the path).

No fix requested — the new shape is more useful. Filing for the record.

---

## PLang/app/modules/signing/code/Ed25519.cs — `From(action.Data)`

`SignAsync` ends with `return global::app.data.@this<object>.From(action.Data)`.
`action.Data` is `data.@this?` carrying the user payload + signature. The
`From()` path with T=object always preserves Value (every value is an object).
Clean by virtue of T=object.

---

## PLang/app/modules/Schema/this.cs — scalar render simplification

Drops the `constructor(name: T), properties: …` verbose form for scalar
types, emitting only the constructor's input type. The comment captures the
rationale (LLM was emitting records for scalar params). The dropped
`properties: …` line is no behavior change at runtime — those properties
still exist; they're just not advertised in the catalog rendering. Honest.

---

## PLang/app/goals/goal/steps/step/actions/action/this.cs — `ReturnTypeName`

New `string? ReturnTypeName` property on Action. Populated by
`Modules.Describe()` from reflection. One property, one purpose, used by
Compile.llm. Clean.

---

## Tests reviewed

`PLang.Tests/App/Modules/assert/AssertTests.cs` — adds 4 path-truthiness
assertions (codeanalyzer v2 N3 follow-up): pass/fail × exists/missing.
Real deletion-test for the `ResolveTruthy` IBooleanResolvable branch
(remove the branch → all 4 go red).

`PLang.Tests/App/Types/PathTests/PathEqualityTests.cs` (new) — exercises
`path.Equals` / `GetHashCode` with the `RootComparison` rule applied in
codeanalyzer v2 N2. Equal-paths-different-case test is the explicit
deletion-test for that rule.

---

## Pass summary

- **Pass 1 (OBP)** — no new cross-file collections, locks, or duplicated
  state. The sweep is pure type-signature work.
- **Pass 2 (simplification)** — `From()` removes the implicit-operator
  double-wrap footgun documented in `coder/footgun.md`. The 12 `(Identity)`
  casts disappear. Net simpler.
- **Pass 3 (readability)** — verbose `data.@this<global::app.types.path.@this>`
  appears often. C# requires it because `path` is ambiguous in those scopes;
  no fix.
- **Pass 4 (behavioral)** — `From()` silent-value-drop is a doc gap (F1).
  Write/Append/Mkdir now return the path in Value (O1, intentional).
- **Pass 5 (deletion test)** — every line of `From()`, `DescribeReturnTypeName`,
  and the GoalCall slash-walk earns its place. The orphan `<summary>` on
  `DescribeReturnTypeName` (F2) does not.

---

## Verdict: NEEDS WORK (low-severity only)

Two trivial doc-class findings (F1 docstring sharpening, F2 orphan XML
summary block). No correctness bugs. The typed-returns sweep landed cleanly:
build clean, C# 2889/2889, plang 202/203 (1 external-LLM flake, 0 stale).

The branch is functionally CLEAN — F1 and F2 are docstring hygiene the
coder can knock out in one commit, or defer behind a "next pass" todo.
