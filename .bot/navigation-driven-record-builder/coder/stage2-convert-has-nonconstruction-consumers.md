# For architect — Stage 2: `Convert` has consumers beyond construction (comparison coercion)

**From:** coder. **2026-07-09.** Blocking the start of Stage 2 (`create-unification`).
Ingi's call: stop and surface when the OBP shape isn't defined in the plan rather than force one.

## Where I am

Stage 1 is functionally complete and pushed (`6c1d2e8ca`): pr-graph hosts, kind redesign,
write-at-path, read-path defer, `*`-kind Read/Set, STJ-cheat deleted, write-side obpv deleted.
Started Stage 2 by tracing the convert hub before relocating `Convert` → `Create`.

## The problem the plan doesn't define

Plan line 70: "Relocate the per-type static `Convert` hooks → each type's own sync `Create`."
Line 75: "Delete `convert.OfStatic`/`Of`/`Invoke`/`Discover`."

But `Convert` is **not only a construction hook**. Tracing every hub caller (14):

```
 • ICreate default Create (ICreate.cs:53) ........ CONSTRUCTION  → the plan's target ✓
 • TryConvert (Conversion.cs:174/207/218/219) .... CONSTRUCTION  → plan's target ✓
 • type.Convert(value) (type.cs:206) ............. CONSTRUCTION  → plan's target ✓
 • type-lift (type.cs:491-494) ................... internal lift → plan's target ✓
 • builder NormalizeParameterTypes (Default:985) . build coercion → routes to Create ✓
 • builder GoalCall build (Default:1178) ......... construction   → Create ✓
 • BuildResponse.Validate (:176) ................. build check     → Create ✓
 ─────────────────────────────────────────────────────────────────────────────
 • bool.CoerceOwn  → bool.Compare ................ COMPARISON coercion  ← NOT construction
 • date/datetime/guid/duration/time — same shape:
     `private static @this? CoerceOwn(object? v) => v as @this
        ?? convert.OfStatic(typeof(@this), Backing(v), null, null)?.Peek() as @this;`
```

The load-bearing one is **comparison coercion**. Each scalar family's `Compare(a, b)` coerces the
OTHER operand into its own type via the Convert hook — the doc on it literally says *"the other side
coerces through this family's own Convert hook (`"true"` → `true`)."* So `%x% == "true"` where `x` is
a bool works because bool.Compare pulls `"true"` through bool.Convert.

If `Convert` dies into `Create`, comparison has to coerce through **something**, and the plan doesn't
say what. This is a real cross-cutting consumer, not an incidental caller.

## Why I didn't just pick one — both candidates smell

1. **Keep `Convert` as a thin adapter delegating to `Create`.** → *middleman* (a proxy whose only job
   is forwarding), and the signatures fight: `Create(item value, Data data)` vs `CoerceOwn` holding a
   raw `object` with no Data in hand. Bridging means synthesizing a throwaway `Data` per comparison.
2. **Route `Compare`'s coercion through `Create`** (wrap the operand in a temp `Data`, call `Create`,
   `.Peek()`). → construction machinery (needs a Data, sets `data.Fail` on decline) doing a
   comparison's pure in-memory coercion; the wrong door, and it drags Data-courier state into Compare.

Both feel like forcing construction to also be coercion.

## The design question for you

**Where does comparison coercion live once `Convert` relocates to `Create`?**

Candidate shapes (your call — I have no strong OBP preference, they all touch the boundary design):
- **(A) `Compare` coerces through `Create`** anyway — accept a lightweight coerce entry on the type
  that shares `Create`'s logic without the Data ceremony (e.g. `Create` split so the pure
  `object → TSelf?` core is reused by both construction and comparison).
- **(B) Coercion is the kind's job** — `Compare` asks `data.Type.Kind` / the value to coerce the
  operand, separate from construction; `Create` stays purely construction.
- **(C) `Compare` already receives typed values** in the new model (born-native everywhere), so the
  raw-object coercion is legacy and can be dropped — the operands arrive as `bool.@this` already.
  (Needs checking: does anything still feed `Compare` a raw string operand at runtime?)

I lean toward (C) being *partly* true (born-native reduces raw operands) but not fully — the compare
boundary still receives wire/literal operands (`== "true"`, `== 404`). (A) with a shared pure core
seems cleanest if coercion must stay, but that's a construction-door reshape I want your sign-off on.

Once you pick, Stage 2's per-type relocation is mechanical and I'll roll through the ~12 types +
migrate the construction callers + delete the hub.
