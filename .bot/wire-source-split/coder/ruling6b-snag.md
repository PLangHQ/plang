# Ruling 6b — mapping snag: "stop answering object" is not sufficient

Branch: `wire-source-split`. Mapped Shape A before cutting (as asked). The caller web is small
and clean, but "`TypeOf("json")` stops answering `object`" does **not** make json materialize as
`clr` — there are two more facts in the way. Surfacing before I touch anything.

## What's clean (ready to cut)
- The json parse's only live callers: the json **kind's `Load`** (`kind/json:69`) + a **dead**
  delegate (`item/serializer/json.cs:31`, no callers — confirmed). `Reader.TypeOf`'s only caller
  is `kind.Type` (`kind/this.cs:50`). So moving the parse onto the json kind and deleting
  `object/serializer/json.cs` is straightforward and de-registers `(object,json)`.

## Snag 1 — `Format.TypeOf("json") = "text"`, so kind.Type resolves to `text`, not `binary`
`kind.Type` (`kind/this.cs:50`) is `Reader.TypeOf(name) ?? Format.TypeOf(name) ?? "binary"`.
Removing the object reader kills the first term — but the second answers:
`Format.TypeOf("json")` reads the **extension→family** map `_extensionToKind` (`format/list:42`),
where **`.json = "text"`** (json content's family legitimately IS text, alongside `.png=image`,
`.mp4=video`). So `kind.Type` for json becomes **`text`**, the narrow lands on the **text type
reader**, and the json string reads back **unparsed as text** — same regression, different type.

So json is not `{binary, json}` after the object removal; it's effectively `{text, json}`. Making
it stay "no type / binary" means kind.Type must ignore the family term for json — but the family
(text) is a real, separately-used fact (compression, etc.), so I can't just delete `.json=text`.

## Snag 2 — the narrowing uses the BASE kind, not the json subclass
The binary→kind narrow (`reader/this.cs`) and `_type.Kind` both use a **base** `kind.@this("json")`
(the type entity stamps `new kind.@this(name)`, `type/this.cs:116`) — never the json **subclass**.
So a json-kind override (of `Type`, or a "I decode myself" signal) on the subclass never runs on
the narrowing path. The subclass is only reached via `App.Type.Kind[name]`.

## So the real question — how does "the kind owns its materialization" get expressed?
The most-specific-owner law is right; the open bit is the **discriminator** — "this kind owns a
raw decode (→ `Load`), so don't narrow it to a family type." Options, pick one:

1. **A signal on the kind** — e.g. the json kind overrides a base member ("I materialize my own
   raw", or `Type => binary`), and `source.Read` resolves the REAL subclass (`App.Type.Kind[name]`)
   to consult it, then calls `Kind.Load`. Clean owner-on-the-kind; needs source to use the subclass
   (the narrowing keeps using base for family types like png→image, which is fine).
2. **`Format.TypeOf` stops conflating family with type-claim** — a kind whose family is `text` but
   which decodes to something else (json) shouldn't resolve its *narrow type* from the family map.
   Bigger: it's the family-vs-materialization split, touches how every kind resolves its Type.
3. **`kind.Type` consults the subclass** — mint the real subclass in the narrow instead of a base
   `new kind.@this(name)`, so a kind that owns its decode can say so. Fixes snag 2 generally; may
   ripple to every kind.Type caller.

My lean is **1** — smallest, and it puts the fact on the kind (the behavior owner), matching "the
kind IS the behavior." `source.Read` asks `App.Type.Kind[_type.Kind.Name]`; if that subclass owns
a raw decode, `await it.Load(_value, Context)`; else the type reader (narrowing, unchanged). The
"owns a raw decode" signal is one virtual on the kind base (default no), json overrides — this is
NOT the rejected `StringIsContent` face-fact (that was on the type/reader); it's the kind declaring
its own behavior, which is what kinds already do (`Descend`/`Load`/`Clr` overrides).

Which discriminator do you want before I cut? (1 / 2 / 3)

— coder
