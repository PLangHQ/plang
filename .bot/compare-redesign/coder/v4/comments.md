# Coder follow-up — architect's resolution of v3 (v4)

Pulled `d887baf88`. Findings **A, B, C all resolved well**, and A is resolved the *right* way:

- **A** — keep lazy read, navigation async via `ValueTask` (sync-completing in memory, awaits only the
  first content read), and the three sync surfaces each handled: param getters → `GetParameter<T>`
  returns a **lazy `Data<T>`** and the handler does `await Param.Value()`; `ToString`/`Equals`/
  `GetHashCode` read materialised backing only; Fluid materialises up-front at `SetValue`. This is
  the correct shape — it pushes the read behind an `await` in the async handler body, and the
  `__ResolveData(name).As<T>(Context)` collapse is a genuine OBP win. No notes.
- **B / C** — aliasing semantics named, `name`-read grep flagged as verify-not-change. Good.

**Build it.** One mechanical consequence of A's resolution to name in Stage 2 — then I think the plan
is ready.

## The `GetParameter<T>`-lazy switch silently moves the param resolution-error guard

Verified: `GetParameter<T>` is **net-new** (only the non-generic `GetParameter(name, context)` exists
today, `action/this.cs:220`), and **42 handlers read `param.Value!` synchronously — usually before the
first `await`** — paired with a resolution-error guard. The canonical shape (`file/read.cs:31`):

```csharp
if (!Path.Success) return Path;          // resolution error (e.g. bad scheme) — caught BEFORE any await
var channel = new …file.@this(Path.Value!);
```

Today the getter **resolves eagerly on access**, so `Path.Success`/`Path.Error` are populated before
the first await. Under A's lazy `Data<T>`, resolution (and any resolution error) only fires at
`await Path.Value()`. So **`if (!Path.Success)` before the await now inspects the *unresolved* lazy
Data and silently stops catching resolution failures** — it still compiles, still runs, just no longer
guards what it did. A bad-scheme `path`, an unset `%var%`, a convert failure — all would slip past the
pre-await guard and surface later as an NRE on `Path.Value!` instead of the clean typed error.

This is the same class as A (a sync-surface assumption) but one layer up, at the **handler** body, and
it's **silent** — which is why it's worth a line. The plan's existing "`.Value` → `await Value()`"
migration covers the mechanical swap but not this ordering/timing shift.

**Ask (one sentence in Stage 2):** state that the param resolution-error guard moves **after**
`await Param.Value()` — `await` the value first, then check `.Success`/`.Error` on the *resolved*
result (or have `await Param.Value()` itself surface the resolution failure the handler returns). The
42 `param.Value!` sites are the migration list, and the guard-reorder is part of each, not just the
`.Value` → `await Value()` swap.

(Not a design change — A's shape is right. Just the one consequence to write down so the handler
migration doesn't silently drop resolution-error handling at 42 sites.)
</content>
