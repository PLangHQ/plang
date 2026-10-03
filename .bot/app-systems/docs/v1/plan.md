# docs v1 — text type teaching + type-page generator

## Context
Types get the same teaching layer modules got (Ingi decision 567). Q20 is resolved
(decision 574): type methods mirror their C# names — case-insensitive, with parentheses,
chainable (`%x.toUpper()%`, `%x.replace("a","b")%`); `length` is the one property
(`%x.length%`). The coder's loader is live and the verbatim `type.description.md` /
`type.examples.md` exist for 40 types. `docs/Types.goal` is docs' (Ingi: "docs bot can
write plang… for what the doc bot needs").

## Plan
1. **text type teaching** (`os/system/type/text/`): write `type.guide.md` + per-member
   `<member>.notes.md` for the final member set (length→number; toUpper/toLower/trim→text;
   replace(old,new)→text; maxLength(max)→text; grep(pattern, lines?)→list<text>;
   grepCount(pattern)→number). All dot. Verify each member's semantics against
   `PLang/app/type/item/text/this.cs`. Leave `type.description.md`/`type.examples.md` as
   the landed one-liners (editing them re-pins the builder golden).
2. **Generator** mirroring the module page machinery:
   - `Documentation/v0.2/type-reference-generation.md` — the spec + a byte-exact text golden.
   - `docs/Types.goal` + `docs/templates/type.template` — mirror `Modules.goal` /
     `module.template`, saving `/system/type/<type>/start.md` (decision 522).
   - Raise the one coder dependency: a parsed member `.Note` (generalize the action note
     parser) so the template can render the argument table and orphan warnings.
3. **Doc fix:** repoint `path-polymorphism-plan.md` off the deleted hand-written module pages.

## Deferred to next version(s)
- Build `Types.goal` (cache:skip), reconcile golden member order, add `TypePageTests` — once
  the member `.Note` lands.
- `Documentation/v0.2/defining-plang-types.md` kinds pass (kind = behaviour, one class per kind).
- Remaining types in order: path, list, dict, number, datetime, duration, size, file, url, data.
- os-bot request: screen/input/clipboard/permission pages on branch `plang-os-stable` (cannot
  run screen examples here — needs PlangOS/Windows).

## Not blocked.
