# item-doors — Stage 1: a value is born whole (v1)

Decision 867/880 (Ingi's fork answer: **(a)**). Every value is born with its type from the app's
list — the app always exists, so no value is made without one. `item.@this` holds the `type.@this`
it's born with; each subtype forwards it. No fallback, no late stamp.

## The shape

- **Base `item.@this`** gets `private readonly type.@this _type;` set by a required ctor
  `protected @this(type.@this type)`. `protected internal override ... Type => _type` on the base
  (the rebuild-per-read at `item/this.cs:422` goes). Every subtype ctor chains the type up.
- **`Create(object? raw)` → `Create(object? raw, type.@this type)`** per subtype (the pure core,
  called only by the type's own `Make`, which looks the type up once). Create only builds; it holds
  the type it's handed.
- **The ~28 INTO-plang implicit operators go** (`@this(string)`, `@this(int)`, the 15 number lifts,
  guid/duration/choice/bool/…). The OUT-to-CLR operators (`string(@this)`, `int(@this)`) stay. The
  compiler lists every use; each caller makes the value with the context it has (`context.Ok(…)`, or
  the owning type's `Make`); a derived value inside a value method forwards `this.Type`.

## What collapses once a value holds its registered type

- **The three re-resolves** — `type.Get:886`, `Out:63`, `Setting:900` (`type.list[this, ctx]`): the
  value already holds the registered type, so navigation reads it directly.
- **The text floor** — `source.cs:115` `_type.Name.ToLowerInvariant() is "text"`: the registered
  text kind carries `IsText`. Add `Text = true` to text's `[Format("", "text/plain", ".txt")]`.
- **`type/this.cs:754`** — `_kind ?? new kind.empty(Name)`: the kinded-from-family ctor reuses the
  family's kind instead of minting a fresh bare empty.
- **`source.cs:240`** — the `_type.Name is "dict" or "list"` round-trip-loss check → **(c)**: the
  declared type's birth refuses (not a Name list).
- **`source.cs:372`** — the `!_type.Is("text")` inside the sync Write (dies with the Write door in
  stage 2; here only what stage 1 needs).
- **Action-slot check** — "is this an action slot?" by **type** (ClrType), not by Name.
- **Image `_mime`** — the image's kind knows its mime from the registered `image.kind.png`, not an
  inline copy minted from a name.
- **Name-sniffing fallbacks** under `type/` and `data/` → ask the type/kind, not its Name string.
- **Bare-type makers** — `new type.@this("name")` route through the registry.
- **List doors** for named types resolve the registered entity.

## Reds this turns green
- 3 C#: `ActionSlotIsByTypeTests`, `ImageKindKnowsItsMimeTests`, `TextKindIsTextByItselfTests`.
- 3 plang: `BytesKnowItsMime`, `NamedTypeIsRegistered`, `TextKindIsText` (+ the four kind/strict
  tests adopted as the stage's proof).

## Process (born-whole is a big-bang — base ctor required)
1. Core: base `item.@this` field+ctor+`Type`; `type` entity `Make`/`Create(raw,type)` pattern.
2. Propagate: every subtype ctor forwards the type; delete the 28 `Type` overrides.
3. Remove the INTO-plang operators.
4. Land the collapses (re-resolves, floor+`Text=true`, :754, :240 as (c), action-slot, image
   `_mime`, name-sniffing, bare-type makers, list doors).
5. Build once → work the compiler wall (operator callers); fix each at the call site.
6. `./dev.sh full`: 6 reds green, guards green, no new reds vs runtime2.
7. Re-pin the surface golden under 867 and 880 (item's `Type`, `Create(raw, type)`, removed
   conversions). Read the diff before committing a moved pin.

## Guards (must stay green)
`SetAsImageGifStrictMatching/Mismatch/NoKind`, `SetAsTextSlashMarkdownStrictUnverifiable`, the whole
kind-is-type suite, the Wire/resume baseline (known-red on runtime2 too — not mine).

LIMITS: stage 1 only. Nothing lands until the whole stage is green.
