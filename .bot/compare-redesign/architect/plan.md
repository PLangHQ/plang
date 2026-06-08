# Typed value model — plan

**Scope.** This began as "redesign comparison" and is now **the typed value model**, with comparison as its first consumer. Everything below lands on one branch (kept named `compare-redesign`; renaming to `typed-value-model` is cosmetic and deferred). The earlier "value is raw CLR, the type is a view" draft is abandoned — the model is **the type holds the value**.

## Why

PLang is a typed language, so the runtime currency is the **typed value** — a `text`, `number`, `file`, `dict`, … that owns its behaviour (compare, navigate, serialize) and exposes its metadata as properties. Raw CLR (`string`, `int`, `byte[]`) is not something developers or handlers pass around; it exists only at the irreducible C#-interop inch, gated. Comparison moves onto the value (async, antisymmetric, enum-valued); I/O is lazy (a file reads only when used); intent drives surfaces (`write out %file%` writes the *file*, not its properties).

## The meta-rule: the type decides

The single principle the rest hangs on: **each type owns its own behaviour — its navigation, its property surface, how it serializes, how it compares.** There is no central taxonomy of "kinds of value" to enumerate or maintain; the resolver/serializer/comparer ask the type, and the type answers. The concrete types below are **examples**, not a fixed case-table — a future type decides for itself. (This is the existing OBP line: behaviour lives on the element, never a `is X.subtype` switch in a central place.)

## The model

1. **The value slot holds a PLang typed value** — an `app.type.item.@this` subtype. `set %x% = 5` stores `number`, not raw `5`. The value owns its behaviour (the existing `item.@this` design, finished and made uniform).
2. **One async, lazy door — `await data.Value()`** (`ValueTask`): sync-complete (zero alloc) when already materialised, async only when it must read. **No public sync `.Value` property.** A held value's own backing is read **privately** inside the type (sync, in-memory); never a public accessor. `Peek()` (was `ScalarValue`) = the current state without forcing the next materialisation step.
3. **Two access planes — `.` and `!` (the type decides what each means for it):**
   - **`.` = the data plane** — navigate the value's content (a `dict`'s keys, a `list`'s elements, a record's fields, a `file`'s parsed content).
   - **`!` = the property plane** — the value's *typed* properties and metadata: `%list!count%`, `%text!length%`, `%x!type%`, `%file!size%`, `%url!host%`, `%file!content%`. This plane **is the Pile-3 surface** (stage 7) — every `!` accessor returns a PLang type.
   - **Leading `!` = the property plane against the implicit root**: `%!app%` ≡ `%this!app%`, `%!actor%` → the app's `actor`. (`!` already exists in the grammar; this is not new syntax. It does not collide with `if !%x%` — the LLM resolves that into `module.condition` at build.)
   - The split is by *content vs about*: held content/state → `.`; derived/metadata about the value → `!`. The **sigil picks the plane**, so the resolver never guesses and a content key named `size` (`.size`) never shadows the property (`!size`). Examples — not a rule: a `file` forwards `.` into its content (`%file.x%` ≡ `%file!content.x%`); a `dict` navigates its entries directly; a `text` has only the `!` plane. Each type decides; new types decide for themselves.
4. **No raw CLR on the public surface — and no generic `ToRaw`.** Raw is a private member; it leaves a type only through *its own* methods: `Write(IWriter)` (feeds the writer its primitive from private backing), `As<T : item>` (type→type conversion, returns a typed value, never CLR), and **gated per-type interop accessors** (`path.Absolute` after `Authorize`, for take-over C# APIs like sqlite/`Assembly.LoadFrom`). Enforced by a build gate, the way `System.IO` is (PLNG-style). Stage 7 extends this to *every* public property (the `!` plane).
5. **Kinds are not values.** `json`/`csv`/`xml`/`yaml` are **kinds** — they tell a deserializer how to turn bytes into a value. After deserialisation it is just a `dict`/`list`, i.e. an `item`, navigated like any item. We never "work with json"; we work with an `item` whose creation happened to go through the json deserializer. So a content key named `type` is just a field (`%x.type%`), while `%x!type%` is the value's PLang type (`dict`/`item`) — different planes, no special-casing.
6. **The reference-fundamental hierarchy:**
   ```
   path (a location — local or remote, identified by scheme)
     ├─ file       (local file: path + lazy content + metadata: size, modified)   ├─ image (file + Width/Height)  ├─ audio/video (file + duration)
     ├─ directory  (local dir:  path + lazy Entries: list<file|directory> + metadata)
     └─ url        (remote: http/https/s3/ftp/… — path + lazy fetched content + metadata)
   ```
   `read X` → a **`file`** (local), a **`url`** (remote), or a recognised specialisation (`image`); an unknown/unsupported local type stays generic `file`. **Content-kind inference**: the extension picks the content's *kind* (`.json` → json → the deserializer that yields a `dict`; `.csv` → table/list; unknown → `binary`). `file`/`directory`/`url` are **new types** this branch adds; `image` becomes a `file` specialisation. `text` stays **pure content** — no `.Path`; the path lives on the `file`/`url`. (`url` over `uri` — it locates a fetchable resource; reuses the existing `app.type.path.scheme` registry, where today's `HttpPath` ≈ `url`.) `directory`'s content property is its listing (`Entries` — name is the type's call); it has no "content".
7. **A reference is a *stable* value; only its content *facet* refines.** A `file`/`url`/`image` is stable — path + metadata + a lazy content facet — so `%file!size%` always works. The **content facet** materialises on demand (`read → bytes → narrow/parse → item`) *within* the reference; the reference is not replaced by its content. **"One representation at a time, refine-and-replace"** describes a **bare value** (e.g. raw bytes straight off a channel: `bytes → item`, the `_raw` slot dissolved into a `binary`/`text` rung). A reference persists; a bare value refines in place. Either way: **verbatim passthrough = the never-parsed path** (read → write-out without navigating emits the original bytes); display is passthrough, not a parse; navigation is what forces the parse.
8. **`write out %x%` is type-owned serialization.** It serializes the instance, and **the type's `Write` decides its wire shape** (OBP rule 9 — the value owns its wire form), then the channel's format serializer renders the primitives. `file` serializes its content; `directory` serializes its `Entries`, each entry serializing *itself* (recursive); a scalar serializes itself. There is **no** universal "forward to `!content`" — that is one type's choice (`file`'s), not a rule. `%file!content%` is the explicit content value (`text`/`binary`/`dict`); `write out %file%` happens to coincide with it because `file` says so.
9. **Comparison is owned by the value's type.** `await data.Compare(other)` → `Comparison`. Both operands are typed values; the higher-**ranked** type drives, coerces the other into its kind, orders two of its own. **Rank lives on the type** — Data asks `this.Type.Rank(other)` (whole other operand, never `other.Type`), gets the driving type (specificity: `number` > `text`, date-family > `text`, `text` the floor). Ordering is **caller order** (`Order(a,b)` → `Less` means `this < other`) — no winner-vs-loser flip. Same driver regardless of operand order ⇒ antisymmetry holds.
10. **Reading async, ordering sync.** `Order` runs on materialised values, returns the enum synchronously, no I/O. `sort` is two-phase — await/materialise keys (all I/O here, `sort by size` reads `file!size`), then order in-hand keys synchronously. No `GetAwaiter().GetResult()`; default compares stay sync (`file`/`path` order by name), I/O-bearing comparisons are written `sort by <key>`.
11. **`Comparison` enum** `{ Less, Equal, Greater, NotEqual, Incomparable }` — no sign numbers. `NotEqual` = reconciled-unequal-unordered (equality uses it, ordering errors). `Incomparable` = unreconcilable (every op errors — `dict == number`). `null` always equality-comparable (`%x% == null` never errors). **Membership never errors** (`contains`/`in`/`indexof`/`unique` match only on `Equal`, treat `NotEqual`/`Incomparable` as no-match). nulls last. The value never throws; the boundary makes it an operator value or a PLang error.

## What this replaces / deletes

- `app.data.Compare` (static mediator), `ScalarComparer`, `Operator.NormalizeTypes` — comparison + coercion move onto the value (rank + per-type `Compare`).
- `IEquatableValue` / `IOrderableValue` + per-type `AreEqual`/`Order` — unified into one `Compare` returning the enum (one interface, ordering opt-in so `dict` answers equality but not order).
- The `_raw` byte slot on `Data` — dissolves into the `binary`/`text` rung.
- The public sync `.Value` property; the generic `ToRaw` — both gone.
- `ScalarValue` → `Peek()`; golden-diff `data.Compare` → `Diff`.

## Stages (value model + comparison first; surface-typing last)

To be carved into files from this spine once approved. Each stage describes the *mechanism*; per-type behaviour is the type's own (the meta-rule), shown by example.

1. **`Comparison` enum** — the sign-free result type + boundary mapping (incl. the membership column).
2. **The typed value door + the `.`/`!` resolver** — async/lazy `ValueTask Value()`; remove public sync `.Value`; private backing; `_raw` → `binary`/`text` rung for bare values; `Peek()`; no generic `ToRaw`. The navigation resolver: `.` → the value's data plane, `!` → the type's property plane (the type answers both).
3. **`file` + `directory` + `url` reference types** — the `path → file/directory/url → image` hierarchy; `read` → `file`/`url`; content-kind inference; references are stable with a lazy content facet; `write out` = type-owned serialization (each type's `Write`); metadata on the `!` plane; `text` loses any path notion.
4. **Per-type `Compare`** — rank + coerce-into-own-kind + the enum, unifying `AreEqual`/`Order`. Prove `text`/`number`/cross-pair, then replicate.
5. **`data.Compare` entry** — async, caller-order, via the existing name→family routing; no `Type.Name` switch.
6. **Consumers + the Pile-2 conversions** — condition operators, `assert`, two-phase `sort`, list ops + boundary mapping; convert the decompose sites (`is string` → `is text`, call the typed method, growing type surfaces where missing — no `ToRaw` escape).
7. **Pile 3 — full public-surface typing (FINAL, large).** Every public property/method that returns CLR → returns the PLang equivalent (the `!` plane: `path!absolute` → `path`, `text!length` → `number`, `dict!keys` → `list<text>`, `file!size` → `number`). Raw CLR survives only at the gated per-type interop inch. Stand up the build gate (PLNG-style, scoped to **public members of `item.@this` subtypes** — internal/private C# is untouched; warning during migration, error once clean; carve-outs for infrastructure markers like `IsLeaf : bool`). This is the bulk of the diff and rides behind the gate so it converges.

Test docs (`plan/test-strategy.md`, `plan/test-coverage.md`) rewritten alongside the stages.

## Open points

- **`directory` listing property name** (`Entries`/`children`/`items`) — the directory type's call.
- **the gate's exact scope** — which infrastructure members on value types are carve-outs (predicates/markers vs value-data members).
- per-type `.`/`!`/serialization decisions that aren't obvious — settled with the relevant type as we carve it (the meta-rule means these are local, not central).

## You own this (coder)

Shapes/names are suggestions; you own the final shape, and **each type owns its own `.`/`!`/serialization/compare behaviour** — these stages give the mechanism, not a frozen per-type table. Non-negotiable: the value is the typed value (`data.Value()` returns it, async/lazy, no public sync `.Value`); no raw CLR on the public surface and no generic `ToRaw` (raw leaves only via a type's own `Write`/`As<item>`/gated interop); `.` = data plane, `!` = the typed property plane; references are stable with a lazy content facet (no `_raw`, no double-storage); `write out` is type-owned serialization; comparison owned by the value with rank-on-the-type + caller-order; ordering sync with all I/O hoisted (no `GetAwaiter().GetResult()`); membership never errors. If implementing forces one to bend, stop and flag it.
