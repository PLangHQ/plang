# Stage 2 — `text` flows native

**Seam:** the first end-to-end type slice. Build `text.@this` out, make strings born as `text`, sweep the `is string` consumers. This is the biggest sweep in the branch — `string` is the most common scalar.

> **You own the final shape.** The op surface on `text`, method names, which sites are behavioral vs. perimeter — all yours. Keep the disposition: strings flow as `text.@this`, and `is string` / `(string)value` in handler bodies become methods or `.Value` unwraps.

## Why text first (after the base)

It's the highest-volume sweep and the riskiest transition, so doing it early surfaces the transition pattern (born-native + sweep-consumers + shrink-the-raw-fallback) while the change set is one type. Everything after follows the same shape with fewer sites.

## Build

`type/text/this.cs` is thin today — `Value` (string), `implicit operator string`, `ToString`. Build it out as a `: item.@this`:
- **Ops** (the behavioral targets of the sweep): length, case (`upper`/`lower`), `contains`/`startsWith`/`endsWith`, `substring`, `split`, `trim`, `replace`, index-of, … — driven by what the consumer census actually needs, not a speculative kitchen sink.
- **Compare / equality** — override `item.Order` (ordinal; settle culture/case policy — `number` shows the shape) and `item.AreEqual` (value equality: two `text.@this("a")` equal; wire `Equals`/`GetHashCode` so it works as a dict key / in a `HashSet`).
- **Truthiness** — `IBooleanResolvable` via `item`: empty string falsy, non-empty truthy.
- **Serializer** — renders **bare** on `application/json` (`text.@this → "abc"`), rides the `application/plang` wire as self-describing Data. Add it to `Normalize`'s leaf set + a converter (mirror the dict/list serializer pattern). **No parallel envelope.**
- **Atomicity** — `text.@this` must not be iterable as chars. `foreach %s%` over a text value is a no-op-or-error, not a char loop. Extend the `IsPlangAssignable`/`IsPlangIterable` carve-out (`data/this.cs`) that exempts raw `string` to cover `text.@this` (it's not `IEnumerable`, so verify it's already safe and add a regression test).
- Keep `implicit operator string` (transition aid + perimeter convenience). **Do not** add an implicit `object` operator (double-wrap footgun).

## Construction (born native)

Make every birthplace of a string value produce `text.@this`:
- `data/this.cs` `UnwrapJsonElement` `JsonValueKind.String` arm → `text.@this`, not `GetString()`.
- `variable.set` parsing, CLI arg parsing, action results that today yield a bare `string`.

## Sweep the consumers (the `is string` sites)

From the census, the `text`/`string` rows. Per the law:
- **Behavioral** (`is string s => s.ToUpper()`, `.Length`, `.Contains(...)`, …) → a method on `text.@this`. The `if` disappears.
- **Perimeter** (`is string s` right before a `Regex`, `JsonSerializer`, a SQL param, a BCL call) → a single `.Value` unwrap at the edge.
- `data/this.cs` itself has many (`:144` `IsVariable`, `:152` `HasVariableReference`, `:248`, `:322`, `:346`, `:560` `IsEmpty`, `:692`/`:839` `%var%` detection) — most are perimeter (string-shape probes); decide each.

## Collapse

- `ScalarComparer` — its string/text handling becomes unreachable for wrapped values; leave the raw arm only for any not-yet-swept perimeter, to be deleted in Stage 7.
- `ToBoolean` raw `is string ""` fallback — unreachable for `text.@this`; keep for the perimeter, delete in Stage 7.

## Acceptance

- `set %s% = "Hello"` → `%s%` is a `text.@this`; `%s.length%` / upper / contains behave; `→ returns string` reconstructs `"Hello"`.
- Two `text.@this("a")` are equal — usable as a dict key and in a `HashSet`.
- `foreach %s%` over a text value does **not** iterate characters.
- A signed text value round-trips through `.plang` (signature survives); the same value to `.json` is bare (`"Hello"`, no signature).
- Empty text is falsy in an `if`.

## Green

Both suites pass. Other scalars (datetime/duration/bool/null) still flow raw — their `ScalarComparer`/`ToBoolean` arms are untouched this stage.
