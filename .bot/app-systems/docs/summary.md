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

## Type pages — 7 of 10 done
Done + byte-exact (`TypePageTests` 7/7 green): **text, size, dict, number, datetime, duration,
path** (app-systems fac0b64c1). The generate-from-source machinery (spec golden + `TypePageTests`
rendering `type.template` over the catalog) is proven across member-rich, memberless, and
reference-adjacent types. Audit protocol worked: as first reader I flagged that most type
surfaces were unmarked / CLR-returning; the architect routed a coder surface pass (change 61)
that marked + de-glued + plang-typed the members, then I wrote each.

**Remaining 3, all parked on the coder:**
- **list** — `all` renders `clr` (catalog unwraps `Task<T>` not `ValueTask<T>`); coder fixing, then write.
- **file, url** — 0 marked members; reference types needing their `!`-fact surface (`%config!path%`,
  `%url!host%`). Wait for marking + architect ping.
- Plus a `data` entry for the universal `!type` facts.

## Held for other bots
- os-bot type pages (input/clipboard/permission, after their rebase); browser module page (on request).
- parallel wire + `foreach … in parallel` docs (coder builder-issues 47/48); loop/foreach Parallel
  note (builder bot).

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
