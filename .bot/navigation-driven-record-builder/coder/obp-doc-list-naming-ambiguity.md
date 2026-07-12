# OBP doc — the `X.list` property-naming ambiguity (coder → architect)

**Context:** implementing the value's type-history collection (`item.list`), Ingi pointed me at the OBP
naked-collection rule *specifically so I wouldn't be confused about the name* — and I still named the
property wrong twice (`TypeList`, then `Prior`) before landing on `list`. Ingi wants the doc fixed so
this fight doesn't recur next session. This is that write-up.

## The confusion (what actually happened)

The item needed a type-history collection (the values it evolved through: an image born from a path
holds the path, so `image.Is("path")` answers from the history). Per OBP that's a naked-`List<T>` →
its own `X.list` type. I built the collection type fine. The property NAME is where I went wrong:

1. First I named it **`TypeList`** — Ingi: *"TypeList is obpv"* (the flat-compound / `App<Plural>`
   smell).
2. Then I proposed **`Prior`** (a singular concept noun) — Ingi: *"no, I'm not ok with prior. i want
   the property name to be `list`."*
3. Landed on **`item.list`**.

Ingi's point: he'd pointed me at the doc up front, and I *still* didn't arrive at `list`.

## Why the doc pointed me the wrong way (the real ambiguity)

Two rules in CLAUDE.md pull in different directions, and nothing states which applies when:

- **Line 10** (`app.X` collection node): *"…enumerate with `app.X.list`."* → the collection reads as
  `.list`.
- **Line 32** (naked-collection fix): *"…its own `X.list` type (private backing, own `Add`,
  `IReadOnlyList<T>` surface), **exposed as a singular property naming the concept** (`callStack.Error`)."*
  → the property is named after the **concept** (`Error`), not `list`.

Both are "true," but line 32 is the one that fires for a naked-collection fix, and it says **name it
after the concept**. So I reached for a concept noun (`TypeList`, `Prior`) — following the stated
rule. It just collides with what Ingi wanted here (`list`).

**The code confirms the ambiguity is real, not just me misreading.** The naked-collection-fix pattern
in the codebase names the property after the concept, PascalCase — and is itself inconsistent
(singular vs plural):

```csharp
public global::app.goal.list.@this    Goal      => _goals;       // singular concept
public global::app.error.list.@this   Error     { get; }         // singular concept  ← the doc's example
public global::app.channel.list.@this Channel    => _channels;    // singular concept
public global::app.service.list.@this Services  => …;            // PLURAL
public global::app.channel.list.@this Channels   { get; … }       // PLURAL
```

So the established pattern is "property named for the concept" (`Goal`/`Error`/`Channel`) — exactly
what led me to a concept noun. `list` is the deviation, and the doc gives no rule for when to use it.

The extra trap in this specific case: the natural concept name was **already taken**. A value's
*current* type is `item.Type`; its type-*history* can't also be `Type`. The doc's "name it for the
concept" has no answer for "the concept name is taken," so I invented synonyms.

## Proposed doc change (line 32, the naked-collection fix)

Replace:

> Fix: its own `X.list` type (private backing, own `Add`, `IReadOnlyList<T>` surface), exposed as a
> **singular** property naming the concept (`callStack.Error`).

With something that states both forms and the tie-break explicitly:

> Fix: its own `X.list` type (private backing, own `Add`, `IReadOnlyList<T>` surface). Name the C#
> property for the concept, **singular** (`Goal`, `Error`, `Channel` — never plural `Channels`, never a
> flat compound `TypeList`, never a bespoke synonym `Prior`); the plang enumeration is always `.list`.
> **When the concept name is already taken by a sibling on the same element** (e.g. `item.Type` is the
> *current* type, so the type-*history* cannot also be `Type`), name the property **`list`** directly —
> it reads as the enumeration (`item.list`).

## Two calls for the architect (before I draft the CLAUDE.md proposal)

1. **Is "`list` only when the concept name is taken" the intended rule**, or should *every* such
   collection property be `list` (which would rename `Goal`/`Error`/`Channel` across the codebase)?
   The former is a small doc clarification; the latter is a codebase-wide rename.
2. **The singular/plural inconsistency** (`Goal`/`Error`/`Channel` vs `Services`/`Channels`) — bake an
   "always singular" line into the rule and fix the two plurals, or leave that out of scope?

Once the architect settles (1) and (2), I'll append the exact wording to
`.bot/<branch>/claude-md-proposals.md` per the CLAUDE.md-proposal workflow (docs-owned; not a direct
edit).

## What shipped meanwhile (so the doc discussion doesn't block code)

The collection landed as `app.type.item.type.list.@this` (`type/item/type/list/this.cs`), exposed as
`item.list` (internal — a public property regressed 3 Modules tests via a reflecting surface; public
nav-exposure is a separate flagged follow-up). `Accumulate` is deleted; construction does
`list.Add(prior)`. Zero regression. See `followups.md`.
