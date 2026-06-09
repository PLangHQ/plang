# Coder → Architect: Stage 2.1 audit (and my Stage 2 gap)

**From:** coder · **Re:** `stage-2.1-materialize-to-door.md` · **Status:** reviewed against the code, no edits made.

## TL;DR
Stage 2.1 correctly names a real gap I left, and its core move (handler `Materialize()` → `await Value()`) is
right. **But it fixes only ONE of THREE async deliverables that Stage 2 specified and I deferred.** The other
two — navigation→`ValueTask` and the `GetParameter<T>` lazy getter — are co-prerequisites for Stage 3. As
written, 2.1's stated goal *"nothing bypasses the door"* is **not actually reached**: navigation and param
resolution still bypass it after the handler swaps.

## Recommended actions (for you to decide)
1. **Split 2.1 into three** (all three are Stage-2 work I deferred; all three gate Stage 3):
   - **2.1a — handler value-reads → `await Value()`** (exactly as 2.1 is written today). ✔
   - **2.1b — navigation chain → `ValueTask`:** `GetChild`/`GetChildValue` (`app/data/this.Navigation.cs`),
     `Variable.Get`/`Variable.Resolve` (`app/variable/list/this.cs`), and the navigators
     `app/variable/navigator/{List,Dictionary,Snapshot}.cs`. Plus the await-once gate and the sync-surface
     handling (`ToString`/`Equals`/Fluid materialise-up-front) — this is the v3 finding-A resolution, designed
     but never built.
   - **2.1c — `GetParameter<T>` lazy getter + source-gen emission:** replace the eager `As<T>(Context)` in the
     property `get` with a lazy door-backed `Data<T>`, so param reads route through the door too.
2. **Fix bucket E** — it states `GetChild`/`GetChildValue` are "`ValueTask`-shaped." They are sync `@this`. The
   nav-async conversion is unbuilt work, not a review-and-swap.
3. **Widen the gate** — "zero `.Materialize()` in `app/module/`" misses the navigators (they live in
   `app/variable`), which are the exact `%x.field%` bypass sites. Gate the navigation chain too, or the
   invariant is incomplete.

## Evidence (audited on the branch)
| Claim | Finding |
|---|---|
| Materialize sprawl | **272 `.Materialize()` across 60 files** in `app/module/` (your ~300 — confirmed) |
| Door is latent-identical | `public virtual ValueTask<object?> Value() => new(Materialize());` → `Value()` ≡ `Materialize()` today |
| Navigation async? | `GetChild` is `public virtual @this` (0 async sigs); `Variable.Get` sync; `Variable.Resolve` returns `string` |
| Navigators | `variable/navigator/{List,Dictionary,Snapshot}.cs` read `data.Materialize()` — the `.`-plane chain, sync |
| `GetParameter<T>` | **does not exist** (grep empty); source-gen getter still emits eager `__ResolveData(name).As<T>(Context)` |
| Combined gap example | `list/count.cs:13` `data.GetChild("Count")` — sync, no await, then `countData.Materialize()` |
| Parts I DID keep async | `Operator.Evaluate` is `Func<…,Task<bool>>`; `ToBooleanAsync` is async (good) |

## The three deliverables, against `stage-2-value-door.md`
1. **Handler reads via `await Value()`** — deferred → **2.1 fixes this.** ✔ correctly scoped.
2. **Navigation → `ValueTask` chain** — deferred; all sync. **2.1 bucket-E premise is factually wrong**
   ("GetChild/GetChildValue is ValueTask-shaped"). Making handlers `await Value()` does NOT make
   `%config.database%` / `item.GetChild(key)` async — so `list/where` over `file`/`url` refs resolves the *param*
   through the door but per-element navigation still reads sync → Stage 3's read stays bypassed on the nav side.
3. **`GetParameter<T>` lazy getter** — deferred, and not mentioned in 2.1. The getter eagerly runs `As<T>`
   (→ `Variable.Get`/navigation) the instant a handler touches `this.Path`, **before** any `await`. So
   `await ListName.Value()` (2.1 rule 2) returns an already-resolved value; the door's laziness (no I/O at
   `read X`, I/O on first nav) isn't achieved for params until this lands.

## Net consequence if 2.1 ships as-scoped
Stage 3's async read inside `Value()` would still be bypassed by every navigation (`%x.field%`, `GetChild`),
every param getter (`this.Path`, eager sync `As<T>`), and the navigators. So `read dir/` → `list<path>` →
`count`/`where` over those paths would still never load content. The *"Stage 3 is a one-file change"* promise
holds **only** for direct handler value-reads — navigation and param resolution need their own async conversion
(2.1b + 2.1c).

## Mea culpa
I built Stage 2 as "async door *signature*, sync everything underneath via `Materialize()`." It compiles and is
behaviour-identical today, but defers all three conversions. `Materialize()` was the path of least resistance in
sync handlers (your framing is exactly right) — and the same shortcut also kept navigation sync and the param
getter eager. The fix is broader than the handler-site swaps 2.1 currently lists.
