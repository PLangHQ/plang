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

## Done since v1
- **Member `.Note` landed (coder, 720c87df8)** and the **text type page is complete + verified**
  (app-systems ae77688c9): rendered byte-exact, `TypePageTests` (TheTextPage) green,
  `os/system/type/text/start.md` committed, `type.template` caches `m.Note` once per member.
  The generator is proven end-to-end. Member order: length, toUpper, toLower, trim, replace,
  maxLength, grep, grepCount.
- **`defining-plang-types.md` kinds pass** (kinds = behaviour variants, one class per kind) +
  **`timer.md`** (sleep → Duration, not Ms int).
- **Screen module page** delivered + merged on `plang-os-stable` (3a2ad7050).

## Next
- **Write the next types: list, dict, number, datetime, duration, size** (architect routing).
  Hold path/file/url — their surface isn't ready (CLR-returning members, glued names; a coder
  pass is queued). As the first reader of each type's surface, **audit each for members that
  return a CLR type (string/bool/int) or have a glued name, and report per type to the architect
  before pinning its golden.**
- Per type, same flow as text: audit → notes + guide → render (DumpType) → golden +
  `TypePageTests` → commit. Then refs file/url + a `data` entry once path's surface lands.
- Held for other bots: os-bot type pages (input/clipboard/permission, after their rebase);
  parallel wire + `foreach … in parallel` docs (coder builder-issues 47/48); loop/foreach
  Parallel note (builder bot).

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
