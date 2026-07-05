# Coder review — "Setting: one concept" plan

**Author:** coder
**Reviewing:** `.bot/settings-config-unification/architect/plan.md`
**Verdict:** **Accept the shape.** The model (one word `setting`; dissolve `app.Config`
registry + `IConfig` + `Config` records + `For/Apply/Resolve/ModuleView`; generator inserts
one middle layer; `%!%` in-memory vs `%setting.%` persistent) is OBP-sound and a net verb+noun
reduction. I verified the load-bearing seams against source below. I have **one blocker** (the
`%!%` overload is bigger than framed and gates two other decisions), **two scoping concerns**,
and recommended answers to all six open questions.

---

## What I verified (grounded, not taken on faith)

| Claim in plan | Source | Holds? |
|---|---|---|
| Generator has 4 param branches; setting = one inserted layer | `Emission/Property/Data/this.cs:78-135` (`IsPlainData` / `IsNullable` / `DefaultValue` / else) | ✅ four branches exist — but see **Concern C** on the `IsPlainData` one |
| `context.ConfigScope` walk `this→Parent→App.Config.Defaults→classDefault` | `actor/context/this.cs:143-145,362` | ✅ exists, clone-on-child at `:362` |
| `MathPolicy` is the sole cross-action reader of `environment.number.Config` | `math/MathPolicy.cs:21` (only `For<...number.Config>`); consumed as a passed `policy` in `number/this.Arithmetic.cs` | ✅ **confirmed** — answers Q5/Q6 |
| Other `For<T>` readers all read their **own** params | http `Default.cs`, llm `OpenAi.cs:65`, signing `Ed25519.cs:80` | ✅ only math is non-param |
| `%!%` is dispatched to a settings scope today | `variable/list/this.cs:685,761,803,862`, `variable/this.cs:185-214` | ❌ **NO** — see **Blocker A** |

---

## Blocker A — `%!%` is already a populated namespace of transients, not a free sigil

The plan treats `%!path%` as "the setting sigil" (read+write, §"front door", Q3). But `%!%` is
**not** dispatched anywhere today — it's the prefix for *every* system/transient variable, and
all `StartsWith("!")` sites merely **skip** them (exclude from persistence/enumeration):

```
!data           action result handle      (action/this.cs:278, cache/wrap.cs:39)
!buildData      build-pass result          (builder/code/Default.cs:635 → variable/set.cs:94)
!ServiceIdentity  http identity            (http/Default.cs:559,584,869)
!ask.answer     interactive answer         (output/ask.cs:71)   ← DOTTED, like a setting
!build.cache    subsystem mirror           (Executor)           ← DOTTED, like a setting
!app !context !callStack !variables !trace !channels   infra DynamicData (context/this.cs:175-180)
```

So `%!path%` → navigate-the-setting-tree **collides with all of these**. The plan's Q3 asks
only about `!buildData` / `!build.cache`; the real set is much larger and includes the infra
`DynamicData` accessors that plang code relies on (`%!app%`, `%!callStack%`).

**Why it's a blocker, not a footnote:** the discriminator you pick here *determines* Q2 (one
read door vs two) and the build-validator's job. A tempting rule — "dotted ⇒ setting" — fails
immediately: `!ask.answer` and `!build.cache` are dotted transients, `!data`/`!app` are
single-token. There is no syntactic tell.

**My recommendation:** do **not** overload the variable resolver (this answers Q2 → *separate
door*). Keep settings on their own resolution path (`context.Setting`), and let the `%!%`
front-end route by **schema membership**, resolved at build time:

- build resolves `%!foo.bar%` against the settable app-tree schema. **Match ⇒ setting**
  (compile to a `context.Setting` read/write). **No match ⇒ existing `!`-variable** (the
  transients/infra keep working unchanged).
- "One concept to the developer" (they type one sigil) does **not** require one *resolver*.
  The sigil is the concept; the routing is invisible, exactly like the two backends under `%!%`
  the plan already accepts (scope vs Direct).

This keeps `!data`/`!app`/`!buildData` working, needs no migration of the infra vars, and makes
the build-validator the single arbiter — which the plan already wants for `%!%`.

---

## Concern B — build-time schema validation is the biggest new lift, and it's under-specified

§Validation-seam says: *"the builder must resolve `%!path%` against the real app-tree settable
schema and reject unknown paths."* That's a **new build-time capability** — there is no
build-time variable-path validation today (`!`-vars are purely runtime-dynamic). It needs:

1. a **settable-schema** built by reflecting the app tree — which nodes exist, which props have
   public setters, each prop's plang type. (This is also what routes Blocker A.)
2. a **hook** in the builder's step/variable validation to catch `set %!path%` and `--` keys.
3. an **error surface** for unknown/mistyped paths.

Two things make it heavier than one bullet:
- The schema must tag each leaf **scope-backed (action param)** vs **Direct (subsystem)** —
  because the Direct side is the **parent branch's** tree. So build-validating `%!build.files%`
  *depends on the parent*. (Ties to Q4.)
- "Which nodes/props are settable" is exactly the reflection the parent's Direct walk also
  needs — build it **once**, shared, or they drift (OBP smell #3).

**Recommendation:** scope this as an explicit deliverable with its own tests, and make the
schema the *single* source both the build-validator and the runtime dispatch read. If it risks
ballooning the branch, land the runtime setting-chain first and gate build-validation behind a
follow-up — the crash fix and the read-path win don't depend on it.

---

## Concern C — the setting layer does not belong in the `IsPlainData` branch

The seam inserts "into all four param branches." But the `IsPlainData` branch (`:86-93`) hands
the `Data` **ref as-is, no resolve** — that's the *Data-flows / no-eager-resolve* rule for
polymorphic forwarders (`goal.call`, `llm.query`'s relay slots). A setting middle-layer there
would force-resolve a relay value. Plain `Data` slots have no `[Default]` and no single type;
a setting read isn't meaningful for them. **The setting layer belongs in the three *typed*
branches** (`IsNullable` / `DefaultValue` / else), not `IsPlainData`. Small, but state it so we
don't wire a setting read onto a relay.

---

## Smaller notes

- **`context.Setting` (in-mem) vs `app.Setting` (sqlite) — same word, different backing/owner.**
  The plan calls the parallel "the point," and to the *developer* (`%!%` vs `%setting.%`) it's
  clear. In C# it's a readability trap: `context.Setting[...]` (scope) next to `app.Setting`
  (store). Defensible, but let's make the doc-comments load-bearing so nobody reads across them.
- **Precedence at the entry goal.** `--` writes the *root overlay*; `set %!%` writes the *goal
  scope*. For the entry goal (`Start`), pin whether its goal scope **is** the root level or a
  child — otherwise a top-level `set %!x%` and `--x` can target the same rung and the "code wins
  over `--`" rule gets ambiguous.
- **plang-typing the moved defaults** (§audit) — agree fully; `http timeout int→duration`,
  `maxResponseSize long→number`, `signing timeoutMs→duration`. This is the right moment (leaf
  values become plang types as they move to `[Default]`). One check: `[Default("30 sec")]` must
  round-trip through `DefaultRaw` → `new data.@this(..., context)` as a `duration` — confirm the
  `DefaultRaw` emission handles a string literal that builds a non-text type.

---

## Answers to the six open questions

1. **`configure` action** → **keep it**, rewrite its body to write `context.Setting`. Dissolving
   into N `set %!http.request.*%` steps breaks the v0.1 "can't combine modules in one step"
   ergonomics and every existing `.goal` that configures. It's legitimate multi-set sugar; only
   its *sink* changes (was `Apply<Config>`, becomes N scope writes).
2. **`context.Setting` vs the `%!%` variable resolver** → **separate door** (see Blocker A).
   `!` is already overloaded with transients; conflating settings into the variable resolver
   would entangle them. One sigil to the developer, two resolvers under it, routed by schema.
3. **The `!` overload** → **route by schema membership at build time** (Blocker A). Schema match
   ⇒ setting; else ⇒ the existing `!`-variable. No migration of `!data`/`!app`/`!buildData`.
4. **Staging vs parent** → **keep separate.** This branch builds the scope side + the dispatch +
   the hand-off point; the parent owns the Direct walk + crash fix. Pulling the parent's walk in
   couples two review cycles and re-opens the crash scope we deliberately deferred. `%!build.files%`
   demonstrable-here is nice-to-have, not worth the coupling — document the dependency, hand
   subsystem leaves to a seam the parent fills. **But** note Concern B: build-validation of
   subsystem paths genuinely needs the parent's schema, so the *shared schema* is the real
   coupling point to design now.
5. **Other cross-action settings?** → **No.** Verified: only `MathPolicy` reads a non-param
   `Config`; http/llm/signing all read their own params. `environment.number.Config` is the sole
   module-level setting.
6. **Number-policy home = `math`** → **confirmed.** `MathPolicy.cs:21` is the only reader;
   `number/this.Arithmetic.cs` consumes a *passed* policy, not the setting. `math` is the sole
   consumer and real node → `%!math.overflow%` / `%!math.precision%`. Agree the `number.*`
   remap dies and the Double/Error drift collapses to `MathPolicy`'s inline `Error`.

---

## Bottom line

The design is right and I want to build it. **Before I start I need one decision — Blocker A**
(how `%!%` routes settings vs the existing transient/infra `!`-vars), because it determines the
read door (Q2), the build-validator (Concern B), and the `configure` rewrite (Q1). My
recommendation is schema-based routing with a separate `context.Setting` door. Concern B
(build-validation scope) and Concern C (skip `IsPlainData`) are refinements, not blockers.
