# Architect review — host-carrier spec (`type=item` revision)

**Reviewing:** `.bot/compare-redesign/coder/host-carrier-spec.md` (rev "a C# object is just an `item`", 2026-06-16).
**Verified against:** code at HEAD `805699509`. Findings below are grounded in the current source + one test run, not the spec's prose.

> **You own the final shape.** Line numbers, the exact `Mint()` body, and the navigator wiring below are evidence and suggestions, not a patch. If you see a cleaner cut, take it — these notes exist to point at what the spec gets wrong about the current code, not to dictate the diff.

## Verdict

The core decision is right: keep one closed carrier, report `type=item` with the C# identity in `kind`. The `type=item` reframe is better than the old host/external version and the naming debate is correctly dissolved. Ship the design. But four things need attention before coding — one is a rule that can't ship as written, two I raised on the prior revision and re-verified still hold, one is a sizing miss.

## 1. The `kind` derivation rule is wrong as written — fix before coding

The spec states the discriminator as **"the type registry"**: registered → registry short name, not registered → `FullName` (§"What goes in `kind`"). That does not produce the spec's own examples for the primary case.

- The runtime-handle types are **not registered** — `app.@this` and `app.callstack.@this` carry no `[PlangType]` (verified: grep empty). By the stated rule they hit the "not registered → external → `FullName`" branch and report a `FullName`, not `kind=app` / `kind=callstack`.
- The current `clr.Mint()` (`type/item/clr.cs:36`) calls `Context.App.Type.Name(clrType)` → `GetTypeName`, whose final fallback is `StripGenericArity(type.Name).ToLowerInvariant()`. For an `@this`-named class that is `"@this"`. So today every handle would report `kind="@this"` — `%!app%` and `%!callStack%` indistinguishable.

**The real discriminator is the `@this`/namespace convention, not the registry.** The handles follow `app.<concept>.@this`, so **namespace-tail gives the right short name for free**: `app.@this` → `app`, `app.callstack.@this` → `callstack`. That method already exists: `item.@this.NamespaceTail`. `FullName` is correct only for genuinely external POCOs (which are not `@this`-named — their namespace tail is wrong, e.g. `MyCompany.Models.Customer` → `Models`).

So `Mint()` should be roughly: `type = "item"`, `kind = NamespaceTail(clrType)` when the carried type is a PLang `@this`-convention type, else `clrType.FullName`. Stop routing through `App.Type.Name` for this case — it yields `"@this"`. (The spec's own impl-note under "Discriminator" already gestures at this; promote it to the rule and drop the "registry" framing.)

## 2. The navigation claim is wrong — host navigation is broken right now

The spec asserts the runtime handles "navigate through the generic reflection navigator (`Object.cs`, **reflects over `Peek()`**)" and that closing the box is "the one coupled change" needed to *preserve* working navigation.

Re-ran on HEAD:

```
ContextVar_AppProperty_AccessibleViaDotNotation — FAILED
  Expected "test", but found "!app"      (PLang.Tests/Runtime/App/Context/ContextVariableTests.cs:207)
```

`!app.Name` does not resolve. `Object.cs:14` reflects over `await data.Value()`, **not** `Peek()`. For a carrier, `Value()` returns the carrier *item* (it doesn't override the door), so reflection runs on the wrapper — which exposes only `Value`/`Context` — and never reaches the host. The non-leaf carrier skips the leaf-backing unwrap at `Object.cs:33`. `git blame` shows the door-resolution commit `c3910993a` flipped `var value = data.Value;` → `await data.Value()`; that is the regression.

**Implication:** closing the box does not break navigation — navigation is already broken. The carrier-owns-navigate change (the spec's **option 1**) is the **repair**, not a preservation, so take it without hesitation. It puts the host-unwrap on the one type that wraps, and `Object.cs` keeps correctly serving items that *are* their own object (`error` and domain values reflect fine, because the item IS the object). Drop "the one genuinely open call" — option 2 only looks competitive if you believe navigation works today.

(The branch is mid-slice with many RED tests, so this failure may already be on your radar. The point is the spec's description of the *mechanism* is wrong, which changes how option 1 is framed.)

## 3. The consumer inventory undercounts

§B lists "7 reach-ins." Re-grepping HEAD (`grep -rnE 'item\.clr \{' PLang/app`) finds three the list misses:

- `module/llm/code/OpenAi.cs:1032` — `clr { Value: Dictionary<string,object?> }` (production)
- `module/http/code/Default.cs:951` — `clr { Value: Dictionary or JsonElement }` (production — whole file unlisted)
- `module/test/discover.cs:299` — `clr { Value: IDictionary }` (spec lists only `:294` in that file)

All read parked data, so the thesis ("no reach-in reads a live foreign object") holds — but the close-the-box sizing is short by three. Re-scan before committing to slice size.

## 4. Two design points to decide

- **Serialize = `[Out]` graph walk over the live app.** `write %!app% to %snapshot%` reflecting the app's `[Out]` graph hits a deeply cyclic graph (App → CallStack → Action → Step → Goal → App). `[Out]` narrows it but gives no cycle/identity story. The spec states it as done; it needs a cycle guard.
- **Reflect-write authority.** Navigate-read is gated for untrusted input (`SecurityFixTests` confirms `skipInfrastructure` keeps `%!app.AbsolutePath%` literal). Reflect-**write** mutating the live singleton (`set %!app.serializer% = "json"`) is a new capability with no gate, and PLang routes capability through the actor permission model everywhere else. Decide whether write needs authorization.

## What I verified as fine (no action)

- **`type=item` does not collide with the un-narrowed JSON sentinel.** The sentinel is specifically `{type=item, kind=json}` (`item/serializer/json.cs:6`); narrowing is driven by the `source`/`json` item's own `Value()` door, not by any global "type==item → parse" check (grep found none), and `clr.Narrow()` is the base no-op. So `{item, kind=app}` is never re-narrowed. The reframe is safe and lattice-consistent.
- **The fix list (close box, own navigate/write/serialize, delete courier-label cruft, `Peek() => self`)** is sound. `Mint()` is not on the `Peek`/`Value` path (its callers are `.Type`/`.Kind`, comparison, navigation type-checks, error messages), so the three-questions table holds.

---

Net order of operations: rewrite the `kind` rule around `@this`/namespace-tail (item 1 — blocks correct `kind`), take option 1 as the navigation repair (item 2), re-scan the inventory (item 3), add the cycle guard + a write-authority decision (item 4).
