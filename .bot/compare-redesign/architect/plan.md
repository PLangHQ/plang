# Typed value model — plan

**Scope grew.** This started as "redesign comparison" and is now **the typed value model**, with comparison as its first consumer. The branch stays named `compare-redesign` (renaming would move all `.bot/` output and re-push for no real gain right after a container crash — we can rename to `typed-value-model` anytime). An earlier draft built on "value is raw CLR, the type is a view"; that's abandoned. The model is **the type holds the value**, and the whole thing — value model, the `file`/`directory` reference types, comparison, and the full public-surface typing — lands on this one branch, surface-typing last.

## Why

PLang is a typed language, so the runtime currency is the **typed value** — a `text`, `number`, `file`, `dict`, … that owns its behaviour (compare, truthiness, length, wire shape) and exposes its metadata as navigable properties. Raw CLR (`string`, `int`, `byte[]`) is not a thing developers or handlers pass around; it exists only at the irreducible C#-interop inch, gated. Comparison moves onto the value (async, antisymmetric, enum-valued); I/O is lazy (a file reads only when used); intent drives surfaces (`write out %file%` writes the *file*, i.e. its content).

## The model

1. **The value slot holds a PLang typed value** — an `app.type.item.@this` subtype. `set %x% = 5` stores `number`, not raw `5`. The value owns its behaviour (the existing `item.@this` design, finished and made uniform).
2. **`data.Value()` returns the typed value, async and lazy** — `ValueTask<...>`, sync-complete (zero alloc) when already materialised, async only when it must read. There is **no public sync `.Value` property**. A held value's own backing is read **privately** inside the type (sync, no I/O — it's in memory); never exposed publicly.
3. **No raw CLR on the public surface — and no generic `ToRaw`.** Raw is a private member; it leaves a type only through *that type's own methods*: `Write(IWriter)` (the type feeds the writer its primitive from its private backing), `As<T : item>` conversion (type→type, returns a typed value, never a CLR type), and **gated per-type interop accessors** (`path.Absolute` after `Authorize`, for take-over C# APIs like sqlite/`Assembly.LoadFrom`). Enforced by a build gate, the way `System.IO` is (PLNG-style). Pile 3 (below) extends this to *every* public property.
4. **The reference-fundamental hierarchy — and intent-based read/write:**
   ```
   path (a location)
     ├─ file       (path + lazy content + metadata: size, modified)   ← generic on-disk thing
     │    ├─ image (file + Width/Height)   ├─ audio/video (file + duration) …
     └─ directory  (path + entries + metadata)
   ```
   `read X` → a **`file`** (or a recognised specialisation like `image`; an unknown/unsupported type stays generic `file`). **`write out %file%` writes the file's content** — PLang is intent-based, "write out the file" means its bytes, not its properties (the file's wire form / `Write` is its content, the `image` precedent). Metadata is navigable: `%file.size%` → `number`, `%file.path%` → `path`, `%file.content%` → `text`/`binary`. `text` stays **pure content** — it has no `.Path`; the path lives on the `file`. `directory` and `file` are **new types** this branch adds; `image` becomes a `file` specialisation.
5. **One representation at a time, refine-and-replace.** A file's content materialises on demand — `read (async I/O) → binary/text → narrow/parse (sync) → dict/list` — each step replacing the prior, no `_raw` slot and no double-storage. `_raw` dissolves: the unparsed form *is* a `binary`/`text` value. `Peek()` (was `ScalarValue`) = the current rung without forcing the next. **Verbatim passthrough = the never-parsed path** (read → write-out without navigating emits the original bytes); **display is passthrough, not a parse**; navigation (`%x.field%`) is what refines to `dict`.
6. **`data.Type` tracks the value's current type**; `kind` (json/png/…) rides the tag. One type home.
7. **Comparison is owned by the value's type** (`await data.Compare(other)` → `Comparison`). Both operands are typed values; the higher-**ranked** type drives, coerces the other into its kind, orders two of its own. **Rank lives on the type** — Data asks `this.Type.Rank(other)` (whole other operand, never `other.Type`), gets the driving type (specificity: `number` > `text`, date-family > `text`, `text` the floor). Ordering is **caller order** (`Order(a,b)` → `Less` means `this < other`) — no winner-vs-loser flip. Same driver regardless of operand order ⇒ antisymmetry holds.
8. **Reading async, ordering sync.** `Order` runs on materialised values, returns the enum synchronously, no I/O. `sort` is two-phase — await/materialise keys (all I/O here, `sort by size` reads `file.size`), then order in-hand keys synchronously. No `GetAwaiter().GetResult()`; default compares stay sync (`path`/`file` order by name), I/O-bearing comparisons are written `sort by <key>`.
9. **`Comparison` enum** `{ Less, Equal, Greater, NotEqual, Incomparable }` — no sign numbers. `NotEqual` = reconciled-unequal-unordered (equality uses it, ordering errors). `Incomparable` = unreconcilable (every op errors — `dict == number`). `null` always equality-comparable (`%x% == null` never errors). **Membership never errors** (`contains`/`in`/`indexof`/`unique` match only on `Equal`, treat `NotEqual`/`Incomparable` as no-match). nulls last. The value never throws; the boundary makes it an operator value or a PLang error.

## What this replaces / deletes

- `app.data.Compare` (static mediator), `ScalarComparer`, `Operator.NormalizeTypes` — comparison + coercion move onto the value (rank + per-type `Compare`).
- `IEquatableValue` / `IOrderableValue` + per-type `AreEqual`/`Order` — unified into one `Compare` returning the enum (one interface, ordering opt-in so `dict` answers equality but not order).
- The `_raw` byte slot on `Data` — dissolves into the `binary`/`text` rung.
- The public sync `.Value` property; the generic `ToRaw` — both gone (door + private-backing + gated per-type accessors).
- `ScalarValue` → `Peek()`; golden-diff `data.Compare` → `Diff`.

## Stages (expanded scope; surface-typing last)

To be carved into files from this spine once approved. Stages 1–6 are the value model + comparison; Stage 7 is the big surface-typing pass, deliberately last.

1. **`Comparison` enum** — the sign-free result type + boundary mapping (incl. membership column).
2. **The typed value door** — async/lazy `ValueTask Value()`; remove public sync `.Value`; `_raw` → `binary`/`text` rung; `Peek()`; private-backing access; no generic `ToRaw`.
3. **`file` + `directory` reference types** — the `path → file/directory → image` hierarchy; `read` → `file`; `write out` = content; metadata as navigable properties; `text` loses any path notion.
4. **Per-type `Compare`** — rank + coerce-into-own-kind + the enum, unifying `AreEqual`/`Order`. Prove `text`/`number`/cross-pair, then replicate.
5. **`data.Compare` entry** — async, caller-order, via the existing name→family routing; no `Type.Name` switch.
6. **Consumers + the Pile-2 conversions** — condition operators, `assert`, two-phase `sort`, list ops + boundary mapping; convert the ~22–30 decompose sites (`is string` → `is text`, call the typed method, growing type surfaces where missing — no `ToRaw` escape).
7. **Pile 3 — full public-surface typing (FINAL, large).** Every public property/method that returns CLR → returns the PLang equivalent (`path.Absolute` → `path`, `text.Length` → `number`, `dict.Keys` → `list<text>`, `file.Size` → `number`). Raw CLR survives only at the gated per-type interop inch. Stand up the build gate so a stray raw-CLR-returning public member fails compilation; convert the surface under it. This is the bulk of the diff and rides behind the gate so it converges.

Test docs (`plan/test-strategy.md`, `plan/test-coverage.md`) rewritten alongside the stages.

## Open points

- **`directory` shape** — entries as `list<file|directory>`; lazy enumeration like file content?
- **content-type inference** — `read X` maps extension → the file's content kind (`.json` → kind json on the content); default unknown → `binary` content.
- **the gate** — scope of the no-raw-CLR-public / no-`ToRaw` analyzer (mirror PLNG002); which per-type interop accessors are the sanctioned exceptions.

## You own this (coder)

Shapes/names are suggestions; you own the final shape. Non-negotiable: the value is the typed value (`data.Value()` returns it, async/lazy, no public sync `.Value`); no raw CLR on the public surface and no generic `ToRaw` — raw leaves a type only via its own `Write`/`As<item>`/gated interop accessors; `read` → `file`, `write out %file%` = content, `text` carries no path; one representation at a time (no `_raw`, no double-storage); comparison owned by the value with rank-on-the-type + caller-order; ordering sync with all I/O hoisted (no `GetAwaiter().GetResult()`); membership never errors. If implementing forces one to bend, stop and flag it.
