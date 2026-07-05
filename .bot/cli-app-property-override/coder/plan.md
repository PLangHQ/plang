# CLI as app-property override — `--flag={...}` sets `app.<flag>`

**Branch:** `cli-app-property-override` (off `variable-as-value`).
**For:** architect review. One structural question is open (see §Open question).

## Origin

`plang '--build={"files":["Scratch/Hello.goal"]}'` **crashes at startup** —
`InvalidCastException: String cannot lower to this` — before any build runs.

Root cause: `Executor.Configure` loads the `--build` config onto `app.Builder` via
`catalog.@this.Populate`, which does `type.Create(rawValue).Clr(propertyType)`. `Builder.Files`
is `List<path>`; the config is strings; `list.Clr(List<path>)` tries to **lower** each `text`
to `path.@this`, and the lower door is terminal (`item.ClrConvert` throws — a text can't lower
to a foreign plang type; text→path is a *convert*, not a lower).

The narrow fix is "use `TryConvert` in `Populate`." But the real problem is the pipeline shape:
today the CLI does `JSON → raw CLR Dictionary/List → plang value (Create) → CLR again (Clr)` —
three hops, the middle plang value created and immediately thrown away. And `--build` / `--debug`
/ `--test` / `--app` are each special-cased with their own branch in `Configure`.

## The design (settled with Ingi)

**Every `--flag` is a property on the app root. The console user can override any app property that
has a public setter, to any depth.** One uniform, reflection-driven mechanism replaces the four-way
flag branch and the double-parse.

```
--build={"files":[...], "cache":false}
   → app.Build                          (property named after the flag, case-insensitive)
   → walk the JSON object against Build's properties:
       Files  (List<path>)  → type.Create(List<path>, ["Scratch/Hello.goal"])  → [path(...)]
       Cache  (bool)        → type.Create(bool, false)                          → false
```

The **type builds itself from the raw value** — the conversion catalog already does this
(`catalog.Conversion.TryConvert(raw, targetType, context)`): a collection converts element-wise,
and `string → path` routes through the path family's own converter → `path.Resolve(string,
context)`, born with context. Nothing new to teach the type system; the CLI just isn't calling it.

Future depth works the same — `app.environment.culture.number.decimal = 0` is reached by the same
recursive walk (`--environment={"culture":{"number":{"decimal":0}}}`).

### Rules (locked with Ingi)

1. **Flag = app property.** `--X` resolves to the app-root property named `X` (case-insensitive).
   Nested JSON keys resolve to sub-properties, recursively, to any depth. **Nested JSON only** —
   no dot-path syntax (keeps parsing simple).
2. **Public setter = config.** Every property with a public setter is user-overridable. Anything
   that must stay off-limits must not have a public setter. (A `[NoCliOverride]`-style opt-out can
   come later if a genuinely-public property must be exempt.)
3. **Value builds via the type.** `type.Create(propertyType, rawJsonValue)` — the target type owns
   "make me from this raw value" (via the conversion catalog). Plang types enter at the perimeter
   and flow; CLR appears only at real use-points, never re-lifted.
4. **Bare flag → `new T()`.** `--build` with no value assigns a fresh instance of the property's
   type (`new Build()`), whose constructor sets its own defaults (`Build()` sets `IsEnabled = true`).
   So the value-less "enable" ergonomic falls out of the same rule — no special enable path.
5. **Typo / no public setter → throw.** A flag with no matching app property, or a target property
   with no public setter, is a **hard error** at startup (clear message). A mistyped config flag
   silently doing nothing is a bad surprise.

### Prerequisite: rename `app.Builder → app.Build`

So the flag *is* the property (mirrors `app.Debug`, not `Debugger`). Drop the `--builder` alias and
the `builder`/`build` normalization in `Configure`. Mechanical rename across the tree
(`app.Builder`, `engine.Builder`, the `Builder` type/namespace, tests).

## What replaces what

- `CommandLineParser.Parse` **stays the syntactic step** — argv → `{ flagName → rawJsonValue }`
  (JSON to raw). It does NOT need app/type knowledge.
- `Executor.Configure`'s four-way flag branch (`--builder`/`--debug`/`--test`/`--app` each parsed
  by hand) → **one recursive reflective walk** from the app root: for each flag, find the app
  property, walk the JSON against its sub-properties, `type.Create(subType, subRaw)` at each leaf,
  assign. `catalog.Populate` becomes this recursive walk (or is replaced by it).
- The narrow "`Populate` uses `TryConvert`" fix is subsumed — the walk uses the conversion catalog
  throughout.

## Open question (§1 — for the architect)

**Where does the reflective walk run, given it needs the *live* app to reflect property types?**

The walk must reflect `app.Build.Files`'s type (`List<path>`) to know how to convert — so it needs
the app instance. But the CLI parse is *upstream* of building the app (its output helps decide how
to build it). Today the sequence is: `CommandLineParser.Parse(args)` → build the engine (app) →
`Configure` applies `--build` config onto `engine.Builder` (app already exists).

So the type-aware walk naturally lands **in `Configure`, after the engine is built**, fed by
`CommandLineParser`'s raw `{flag → json}`. That satisfies "plang types enter early" for the *config
apply* stage, but it means `CommandLineParser` itself stays type-blind — the app-aware assignment is
a *separate* stage.

Ingi's instinct is that `CommandLineParser` (being plang-specific) should own the typed assignment
directly. The tension: to do that, the app must exist before/at parse time. Options for the
architect:

- **(A) Two stages (current shape, generalized).** `CommandLineParser.Parse` → raw `{flag→json}`;
  a recursive `app.ApplyConfig(rawFlags, context)` walk runs in `Configure` after the engine exists.
  Simplest; the walk is app-aware, the parser is not.
- **(B) Parser holds the app.** Build (or partially build) the app earlier so `CommandLineParser`
  can reflect-and-assign directly. Removes the two-stage split but reorders bootstrap — the app must
  be constructable before its own CLI config is applied (which config *is* part of? e.g. `--app`
  overriding the app root itself, `path` for the startup dir).
- **(C) Split the app's config surface from its runtime.** A lightweight config tree the parser
  populates, consumed when the engine is constructed. More moving parts.

Bootstrap ordering is the crux: some flags configure *how the app is built* (`path`, `--app`),
others configure *subsystems on the built app* (`--build`, `--debug`, `--test`). (A) handles the
latter cleanly and leaves the former where it is; (B)/(C) try to unify but must sequence app
construction vs config application carefully.

**Recommendation from the coder:** (A) — it's the smallest change, keeps the parser honestly
syntactic, and the "types flow" goal is met by the *walk* (one lift, one convert, no CLR
round-trip). But the bootstrap-ordering call is the architect's.

## Scope checklist (once §1 is decided)

1. `app.Builder → app.Build` rename; drop the `--builder` alias + `Configure`'s flag normalization.
2. `Build()` ctor sets `IsEnabled = true` (bare-flag rule).
3. Recursive `type.Create(propertyType, rawJson)` walk from the app root (the conversion catalog
   does the per-type build); nested JSON; born-with-context.
4. Delete the four-way flag branch in `Configure`; route all `--flag` through the walk.
5. Errors: unknown property / no public setter → hard `InvalidOperationException` (clear message)
   at startup.
6. `Builder.Files` stays `List<path>` (plang types); consumer (`builder/code/Default.cs`) unchanged.
7. Regression: `--build={"files":[...]}` builds `Hello.goal` (build + run) without the startup crash.
