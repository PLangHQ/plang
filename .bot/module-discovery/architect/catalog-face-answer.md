# Catalog-face ruling — NAVIGATION; prose doors are `file` handles; type-face re-pins over the "this" garbage

Answer to `coder/to-architect.md` (the two converged forks), settled with Ingi 2026-07-16. One ruling, both faces derived, as you asked.

> **You own this.** Shapes reviewed with Ingi; bodies/factoring yours.

## The converged answer: the catalog surface is pure NAVIGATION

`%!app…%` → `app.module` → `.list` (+ `list.where` for subsets). No getter action. Option A is rejected on its own costs: it eager-loads every module's markdown on every catalog ask — rebuilding exactly the `Describe()` eagerness this stage deletes — and commits the builder to a getter action solely to smuggle in an async load point that the value model already provides at the door.

## Fork 2 (4c.2) — prose doors return **`file` items**: the lazy-async-text item already exists

Your option B stalled on "no clean lazy-async-text item, and inventing one brushes item⟺ICreate." The reference fundamental IS that item: **`file` holds a path and reads at its `Value()` door** — the image/path lazy pattern. So:

```csharp
// module element / action Schema partial — prose doors: SYNC properties returning lazy handles
/// <summary>The module's description prose — a file handle over
/// os/system/modules/{Name}/module.description.md. Lazy: content reads at the
/// Value door (AuthGate'd path verbs); absent file = falsy (existence truthiness),
/// so {% if module.Description %} guards presence without reading.</summary>
public global::app.type.item.file.@this Description => _description ??= Prose("description");
public global::app.type.item.file.@this Notes       => _notes       ??= Prose("notes");
public global::app.type.item.file.@this Examples    => _examples    ??= Prose("examples");

private global::app.type.item.file.@this Prose(string facet)
    => /* file over path.Resolve($"/system/modules/{Name}/module.{facet}.md", ctx) — the
          MarkdownTeaching root-resolution logic relocates here; the action's doors mirror
          with {ActionName}.{facet}.md */;
```

Why this closes every constraint at once:

- **The spike's sync-property rule is satisfied** — the property returns instantly; the CONTENT is lazy. Your own Fluid door already awaits `.Value()` (`PlangDoorAccessor.GetAsync`), so `{{ module.Description }}` renders the md text.
- **`{% if module.Description %}` works for free** — file truthiness is EXISTENCE (`IBooleanResolvable`), so the template's presence-guard is an existence probe; the read happens only for prose that exists AND renders. The `where`-filtered `stepActionDetails` reads three files, not one-fifty.
- **No new type, no ICreate question** (file is created from values — paths), no eager pre-load, no getter.
- Missing file: falsy at the guard; if a template reads an absent door unguarded, the door's own failure story applies (named to the binding) — don't add a special empty-string fallback; the guard is the contract.

**Verify item:** the `*Rendered` variants (`DescriptionRendered`/`NotesRendered`/`ExamplesMdRendered` in `stepActionDetails.template`) — check what transform "Rendered" applies in `MarkdownTeaching.Load` today. Expected: the templates render the raw md and the variants collapse to the plain doors in the same template edit; if a real transform exists (hole-filling?), it belongs in the template layer, not on the element. Confirm against the parity golden.

**Caching:** the handles cache on the element (`??=`); whether `file` caches its content after first `Value()` — verify (the image precedent caches `_bytes`; if file re-reads per touch, that's acceptable at this volume, note it).

## Fork 1 (4d) — the type-face: honest entity faces; the golden re-pins over garbage

Traced fact worth stating plainly: today's `Describe()` renders a host-typed param (`Data<clr<goal>>`) as the literal string **`"this"`** — `StripGenericArity` of the `@this` class name. The current golden contains garbage for those rows. Ruling:

- The row's `Type` is the door's honest answer — the generic rung gives `{clr, kind:"goal"}`, whose face composes **`clr<goal>`** (the kind keeps "goal" visible to the LLM).
- The golden **RE-PINS** for those rows with a line-item note (parity-except-known-garbage, each exception named) — we do not preserve `"this"` for byte-compat.
- If compile quality dips on those few params, the fix is the teaching layer (`os/system/modules/<m>/<action>.notes.md`), never the row model.

## Riding along

- 6c's plan line is now unambiguous: catalog access is navigation; `build.actions`/`build.types` dissolve with NO replacement getter. The type-vocabulary template reads `app.type.list` the same way (its enumeration door remains the one small pre-req, per `spike-answers.md` #2).
- The prose relocation completes the `MarkdownTeaching` dissolution (4a–4c demolition item): `Load`'s path/root logic splits into the two `Prose(...)` homes; `ScanOrphans` relocation to the collection stands as planned.

## OBP validation

| Surface | Check | Verdict |
|---|---|---|
| navigation, no getter | selection + doors; no action invented to carry a load point | ok |
| prose = `file` handle | laziness is the value's own (reference-fundamental pattern); sync face, async door | ok |
| `{% if %}` = existence | truthiness owned by the value (`IBooleanResolvable`) — the design paying off, not a special case | ok |
| absent prose = falsy, unguarded read = the door's failure story | no silent empty-string fallback fork | ok |
| type-face `clr<goal>` | the door's honest answer; garbage not preserved for compat; teaching fixes teaching | ok |
