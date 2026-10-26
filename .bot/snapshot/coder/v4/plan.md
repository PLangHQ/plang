# snapshot — coder v4 — stage 8: what a program adds is its `property`

Go: architect, after 66/67 (Ingi: `property` at two levels; added properties saved with every value).

## The change

- `app.type.item.kept.list` → `app.type.item.property.list` (folder `PLang/app/type/item/property/list/`), an item:
  - `["name"]` selects one added property (the Data it was given);
  - `.list` lists them (a plang list of those Data);
  - it writes itself (each Data whole, under its name, in name order: the digest stays order-free);
  - it is made from what it wrote (a dict of Data → the list), through its own `Create`;
  - `Dict(types)` and `Replace(dict, ctx)` go.
- Each keeper declares its plang node `property`, `[Store]` only: app, actor, call, goal, step, module.
- item's fall-through (`%!app.home%`, `item.Set`'s keep) keeps an internal hook (name: see Q1).
- Data: `Kept`/`HasKept` → `Property`/`HasProperty` (attachment too). The wire member `kept` → `property` (plang writer and
  data reader). The Data's own `Properties` untouched.
- The app's snapshot part keeps writing its property by hand until stage 7 (renamed only): node `property`.

## Tests (red first where new)

- Each keeper: `%!x.property["y"]%`, `%!x.y%`, `%!x.property.list%` (six keepers, one class).
- A value's added property through the plang wire, under the member `property`.
- An actor's added property through a snapshot's text (red today).
- Surface golden re-pinned citing "snapshot 66, 67"; no other golden moves.

## Questions to the architect (sent before building)

- Q1: the base hook's C# name.
- Q2: the actor's property in a snapshot needs its ISnapshot part to write it (an actor writes as its name in every
  view, so the general write never reaches its members). By hand like the app's until stage 7?

## Smells checked as I go

glued name, creative name, naked collection, middleman, stored twice, verb+noun, late stamp, envelope.
