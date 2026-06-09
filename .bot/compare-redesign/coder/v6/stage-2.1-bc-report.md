# Coder → Architect: Stage 2.1 status + the B+C decision I need from you

**From:** coder · **Re:** `stage-2.1` Parts A/B/C · **Status:** Part A landed green; B+C blocked on two decisions.

## TL;DR
Part A (handler reads → `await Value()`) is substantially done — `app/module/` `.Materialize()` **272 → 112**,
production green throughout. **B and C are one all-at-once interlocked change** (I proved the coupling
concretely), and before I build it I need you to settle **two things**: (1) a **design fork** in how
navigation goes async, and (2) **formalising the gate exemptions**. Detail below.

## Part A — done, green (what's left is intentional)
The 112 remaining `.Materialize()` in `app/module/` are NOT unfinished routing — they split:
- **54 optional `?.Materialize()` → belong to Part C.** They're the exact verbose intermediate
  (`(X==null?null:await X.Value()) ?? d`) that C's null model collapses. Doing them now = churn-then-retrofit.
- **~44 gate exemptions** — see `v6/gate-exemptions.md`. Genuinely-sync surfaces (JsonConverter `Write`,
  `IFileInfo`/Fluid, diagnostics-like-ToString), and build-meta handlers (builder/code/Default — process the
  LLM's *build output*, never runtime refs). Need you to formalise (like `System.IO`'s `app.type.path.**`).
- **~4 Stage-6-owned** (list/sort two-phase, condition/Operator mediator).
- **~10 scattered sync helper/predicate/service-resolution** sites — per-site flip-vs-exempt, noted.

## B+C are ONE change — the coupling, proven
1. `GetChild`/`Variable.Get` → `ValueTask` (B) ⟹ `Data.As<T>` must be async (As<T> calls `Variable.Get` to
   resolve `%var%`) ⟹ a **sync** source-gen property getter can't call async `As<T>` ⟹ the **lazy getter** (C).
2. **C's `.Value(fallback)` overload can't land additively.** I added it expecting it harmless — **208 errors.**
   A second `Value` overload makes `data.Value` (method group) **ambiguous**, breaking every remaining silent
   `data.Value` method-group site in production (the ones that compiled via single-overload method-group→`object`/
   delegate conversion — the CS8974-equivalents). So the overload, and thus C, must land **with** the migration
   of those ~200 sites. Reverted; production stays green.

Net: B's nav-async, C's lazy getter + null model + `.Value(fallback)`, and the ~200 method-group consumer
migration + the 54-optional retrofit are **one unit that's green-or-deeply-red, nothing between**. (I held the
door cutover red across 2130 errors and ground it green — B+C is that shape, but with a design choice and
careful null-model logic, not a mechanical signature swap. That's why I want the fork settled first.)

## DECISION 1 — the navigation-async design fork (your call)
**Design-1 (your plan): `ValueTask GetChild`.** GetChild awaits the parent's value to navigate. Forces
`Variable.Get`/`Resolve` → `ValueTask` (≈29 `Get` + 6 `Resolve` callers await), forces `As<T>` async, forces C.
Faithful to the door model; biggest blast radius.

**Design-2: lazy-child `GetChild` (stays sync).** `GetChild("field")` returns a **lazy child `Data`** that
defers the read — `await child.Value()` (the existing async door) does `await parent.Value()` → navigate. So
`GetChild`, `Variable.Get`, `As<T>` all **stay sync**; the async read is only ever at `Value()` (where Stage 3
already lands). Avoids the `Variable.Get`/`As<T>` ripple **and** decouples B from C.
- *Cost:* nav errors (key-not-found) surface at **touch-time** (`await child.Value()`), not access-time —
  which is actually consistent with the door's touch-time-error model. And it's a bigger `GetChild` restructure
  (build a lazy navigation source instead of eager navigate).

**I lean design-2** — it shrinks the blast radius, decouples B from C (B becomes a self-contained `GetChild`
restructure; C becomes just the getter rewrite + null model), and routes everything through the one async door
you already have. But it diverges from your stated design-1, so it's yours to pick.

## DECISION 2 — gate exemptions
Formalise `v6/gate-exemptions.md` (or tell me to route the borderline ones). The gate
`grep .Materialize() PLang/app/module PLang/app/variable/navigator → zero` needs the carve-outs for serializer
`Write`, `IFileInfo`, diagnostics, and build-meta — else it can't pass with those legitimately-sync surfaces.

## What I'll do once you decide
B+C as one focused unit on the chosen design: nav async (B), getter rewrite + non-null model + `[NotNull]` +
`[Default]`-on-null + `.Value(fallback)` (C), then migrate the ~200 method-group sites + the optional-param
retrofits in the same pass; land green. Then Stages 3–6.
