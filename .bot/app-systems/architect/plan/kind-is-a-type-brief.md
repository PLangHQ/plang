# A kind is a type — brief, parked for a future design talk (Ingi, 2026-10-01)

## Why

A kind is a narrower type: "json text" is a kind of text, `30s` is a short duration. The code already half-thinks so: a type narrowed by a kind is its own type object (`type/this.cs:101–104`, "a different kind is a different type object"). But the kind is a second object beside it (`type/kind/this.cs`, a C# behaviour class: parse, write, navigate), so a program reading `%t!type.kind%` reaches a C# object through reflection, not `short`. Ingi asked "what if kind : type? would that be wrong?", then "yes, I like this".

## The idea

One object: the kind *is* the narrowed type. A type's kinds are its subtypes; a value's type is the narrowest one, which knows its base.

```
%t!type%          → the short type (a duration)
%t!type.kind%     → short
%t!type.base%     → duration         (a new member; the word is open)
written as        → {"name":"duration","kind":"short"}   (the wire unchanged)
```

The trap to avoid: `kind : type` as plain inheritance while values still hold `{duration, short}` gives two objects for one narrowed type (stored twice). It has to be one.

## What the trace answers first (report only, by the coder)

- Every place a kind and a type meet: the 21 kind classes, the type list's lookups (name, MIME, extension, C# class), how a value holds its type.
- The kinds made at run: text coins its format kinds (decision 398), and a name with no class (`md`) gets a bare kind.
- `list<text>`: the kind carries a generic's element.

## The decisions for Ingi

1. What `%t!type.name%` answers for a narrowed type: the base's name (`duration`, as the wire writes it) or `short`.
2. The base's member name: `%t!type.base%` or another word.
3. A type with no kind: the base itself (the "empty kind" object goes).
4. `list<text>`: a subtype of list in the same model, or the element kept separate.

## Meanwhile

`%t!type.kind.name%` reads the kind's name (the duration kinds' test uses it). No program reads a kind today, so nothing is blocked.
