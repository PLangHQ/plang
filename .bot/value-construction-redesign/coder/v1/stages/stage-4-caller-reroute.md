# Stage 4 — caller reroute (set + validateResponse drop their eager convert)

**Goal:** the two sites that eagerly convert *before* constructing stop doing so, and let construction (the ctor → `type.Build` → case 2b) do the one conversion. Removes the cross-file convert split (`set` ↔ ctor) — invariant #2, and OBP smell #4 (allocate-here/convert-there).

**Kind:** flip. Depends on Stage 3 (the ctor fork + case 2b exist).

---

## Step 4.1 — `set` (`variable/set.cs:248-273`)

Current shape:
```csharp
if (Value.RawUntouched) sourceValue = await Value.Value();   // :248 — materialize a %var% (type-differs path)
object? converted = sourceValue;
var keepAsIs = … Value.Type.Is(type) …;                       // :255 — KEEP (richer-type-under-different-name)
if (!keepAsIs && converted != null && !targetType.IsInstanceOfType(converted))
    var convResult = type.Convert(converted, Context);        // :264 — eager convert — DROP
    converted = convResult.Peek();
var typedData = new data.@this(name, converted, keepAsIs ? null : type, context: Context);  // :273 — re-converts in ctor
```

The type-**matches** case already returned at `:240-246`. So the fall-through is a **materialized, type-differs** value — exactly case 2b.

**Reroute:**
- **Drop** the eager `type.Convert(converted, Context)` block (`:263-269`). Construction does it once — the ctor delegates to `type.Build`, whose case 2b converts.
- **Construct with the declared type**, handing the ctor the value (`sourceValue` — built but type-differs → case 2b converts it; or still-raw → case 3). `new data.@this(name, sourceValue, keepAsIs ? null : type, context: Context)`.
- **Keep** `keepAsIs` (`:255-257`) untouched — an image bound to a `path` slot stays an image. It already passes `type = null`, so it never enters typed construction. (This is the same facet-match semantics 2a respects; here it short-circuits before construction.)
- Preserve the strict-kind-rides-to-load-seam behavior below `:273` — confirm it still fires on the case-2b/case-3 output.

**Subtlety:** `IKindValidatable` branch — today `:266-268` swallows a failed convert for `IKindValidatable` targets (defers to load-seam validation). After reroute, case 2b's convert must preserve that: a kind-validatable type that can't convert *now* should still defer, not hard-fail at `set`. Verify case 2b (via `type.Convert(item)`) routes a kind-validatable type to its deferred validation, matching the old `:266` behavior. If 2b hard-fails where `:266` deferred, that is a regression — fix in the convert op, not by re-adding the `set` branch.

---

## Step 4.2 — `validateResponse` (`builder/validateResponse.cs:222`) — THIS IS THE SAFETY NET

Current: `var conv = p.Type.Convert(resolved, ctx); if (conv.Success) continue;` — `resolved` is a *materialized* value; a bad literal (`"abc" as number`) fails the convert → build error.

**Reroute (carefully):** the goal is to keep the bad-literal catch. The plan routes this to "construct with the declared type so case 2b converts." The risk (caught in addendum v2): if construction merely *held* the value, the check would no-op and `"abc" as number` would pass build.

- Construct `new data.@this(p.Name, resolved, p.Type, context)` and **force materialization** so case 2b's convert runs and can fail: `var built = await data.Value();` then check `data.Success` / `built` for the failure.
- Because `resolved` is a built value of a different type, this is case 2b → `type.Convert(item)` runs → `"abc"` → `number` fails → `data.Fail(... )` → caught. Confirm the failure surfaces as a build error with the same message quality as today (valid-values hint, etc.).
- **Keep the choice branch above (`:215-220`)** — the explicit `Choices.Get` + `ValidValues` membership check for enum types runs before the generic convert and should stay (it gives the "Valid values: …" hint).
- Do **not** replace this with a construct-and-hold — assert in a test that `"abc" as number` and an out-of-range enum **fail build** after the reroute.

---

## Exit criteria

- [ ] `set.cs` no longer calls `type.Convert` itself; constructs once with the declared type; `keepAsIs` + strict-kind behavior preserved.
- [ ] `validateResponse.cs` converts via construction+materialization; `"abc" as number` and a bad enum **still fail build** (test asserts this — it is the safety net).
- [ ] Grep: `set`/`validateResponse` no longer call `type.Convert(object?)` eagerly. The only `.Build(` callers are the ctor + `Declare` (the legit construction entry); `.Judge(` has no live caller (Stage 5 deletes it).
- [ ] `IKindValidatable` deferral preserved (test a kind-validatable type that defers).
- [ ] Global exit gates green.

## What must NOT happen

- Do not leave `validateResponse` as construct-and-hold — that silently disables build-time bad-literal detection.
- Do not re-add a `set`-local convert to work around a 2b regression — fix 2b.
- Do not delete `Build`/`Judge`/`Convert` yet (Stage 5).
- Do not drop the enum `Choices`/`ValidValues` pre-check in validateResponse.
