# docs — summary

**Version:** v1

## What this is
PLang types now get the same learner teaching layer that modules got (decision 567): a
generated reference page per type, rendered from source so it cannot drift. This version
delivers the first type end-to-end (`text`) plus the generator that produces every type's
page. It solves the problem that a reader (or the builder) could not discover which members
a type has or how to write them — that knowledge lived only in C#.

## What was done
- **text teaching** (`os/system/type/text/`): `type.guide.md` (learner cross-member prose) +
  per-member `<member>.notes.md` for the final member set — `length`→number (dot property),
  `toUpper()`/`toLower()`/`trim()`/`replace(old,new)`/`maxLength(max)`→text,
  `grep(pattern, lines?)`→list<text>, `grepCount(pattern)`→number. All read with a dot;
  methods mirror their C# names with parentheses (decision 574). Semantics verified against
  `PLang/app/type/item/text/this.cs` (grep's `lines` = context lines, default 0; `maxLength`
  marks a cut with `"..."`, 0 = no limit). `type.description.md` / `type.examples.md` were
  left as the landed one-liners (they feed the builder prompt; editing re-pins its golden).
- **Generator** mirroring the module page machinery:
  - `Documentation/v0.2/type-reference-generation.md` — spec + byte-exact `text` golden.
  - `docs/Types.goal` + `docs/templates/type.template` — mirror `Modules.goal` /
    `module.template`; save `/system/type/<type>/start.md` (lands in os tree by decision 522).
- **Doc fix:** `path-polymorphism-plan.md` repointed off the deleted hand-written module pages
  to the generated `os/system/modules/{file,condition}/start.md`.

## In progress / next
- **One coder dependency raised:** a parsed member `.Note` (`Line`/`Returns`/`Warning`),
  generalizing the action note parser (`PLang/app/goal/step/action/note/this.cs`) to compare
  against a member's `.Arguments`, treating the member's own name and `Returns` as allowed
  non-argument line names. The template is written against this API.
- **Once `.Note` lands:** build `Types.goal` with `--build={"files":"Types.goal","cache":"skip"}`
  from `docs/`, reconcile the golden's member order against the real render, add `TypePageTests`
  (mirror `ModulePageTests`).
- **Then:** `defining-plang-types.md` kinds pass; the remaining types in order (path, list,
  dict, number, datetime, duration, size, then file/url + a `data` entry); the os-bot request
  (screen/input/clipboard/permission pages on branch `plang-os-stable` — screen examples can't
  be run here, they need PlangOS/Windows).
- Left the `loop/foreach` Parallel note to the builder bot (architect heads-up: its stage 4,
  gated on coder 592).

## Code example
A member note (`replace.notes.md`) — line 1 keyed by the member name carries the summary and
the call form; one line per argument; a `Returns —` line:

```
replace — The text with every occurrence of one substring swapped for another. · say: %text.replace("old", "new")%
old — the substring to find · say: the first argument
new — what to put in its place · say: the second argument
Returns — the text with every match replaced.
```

The template renders this as a `## replace` section: summary, the call form in backticks, an
argument table (names/types/required/default from the catalog, `say:`/prose from the note),
then **Returns**.
