# coder v5 — stage 5: faces and honest facts

The plan's stage 5 row and its "Faces" section are in `.bot/app-systems/architect/plan.md`.
This file holds the trace, the proposals and the order.

## Trace
| part | today | size |
|---|---|---|
| a type's face | `type.Write` is the identity `{name, kind?, strict?, template?}`. It is used for a Data's `type` slot (`json/writer.cs:88`) and, through item's default `Output`, for a type held as a value, in every view (Out and Store). | 1 class |
| facts | text and base64 declare a description; text's example is a filename; archive, binary and signature have placeholder examples | ~25 item classes |
| prompt C | `properties.template:82` prints `p.Type.Description`, `Values`, `Example` for each property type an action takes | builder-visible |
| internal items | `wire` and `clr` are `@this` classes, so they are in the type list (the name `clr` is the naming fallback, `PlangName`); `source` and `computed` are not | 2 in the list |
| view | `type/list/view` + `BuildTypeEntries` (Obsolete); module's `Schema` builds it; about 11 test files call `app.Module.Schema.Build()` | delete |
| goal description | 357 of 594 `.goal` files (os/ and Tests/) write it under the name, so it lands on step 0 | 357 files |
| `goal.Description` | set by the build (`build/code/Default.cs:213`), written by the `.pr` writer (`goal/this.Item.cs:43`), read back (`goal/serializer/Reader.cs:54`) | 4 sites |
| goal hash | name + each step's text as written (`goal/this.cs:130-156`); comments not covered | 1 member |
| docs | four `readme.md` under os/ templates; `SetLayout.goal:3` and `setlayout.pr` hold the path; the repo's README ↔ start.md pairs (root, PLang/, PlangConsole/, Skill/, tools/decider/) | 4 + 5 pairs |

## What needs a decision (sent to plang-40)
1. **The face is the Out view only.**
   - A type written as a value in the Store view (a `.pr` row holding a type, such as variable.set's `Type`) must stay its identity, or every `.pr` changes.
   - Proposal: `type.@this` overrides `Output`. In `View.Out` it writes the face; in every other view it writes the identity. `Write` (the Data's `type` slot) never changes.
2. **Honest facts change prompt C's text.** Real descriptions and examples replace placeholders, and prompt C prints them. So the twins re-record (C# and python render the same new text) and the change gets one eval run (C + nano, the 5 goals + the builder's 12), as the rule says for builder-visible stages.
3. **Internal items stay out of the face, not out of the list.** The name `clr` is what `PlangName` answers for an unowned C# class, so `wire` and `clr` stay in the list. They declare `Internal` (a fact on the type, read from the class like `Alias`), and the `%!app.type%` face and prompt C skip them.
4. **357 goal files.** The helper agent edits them with the Edit tool (the hook blocks shell edits of `.goal`), a folder at a time. The os/ `.pr` files are then re-saved by `plang build`: the hash now covers comments, so each goal's `.pr` no longer matches and is written again with every step still cached (no decider, no LLM). I verify by diffing each `.pr`: only comments and hashes may change.

## Order (each its own commit, green against the baseline)
1. Honest facts: each type's description and example; `Internal` on wire and clr. Twins re-recorded plus one eval run; this is the builder-visible commit.
2. Faces: `type.Output` in the Out view (`%!app.type%` → names; one type → name, description, example, alias, kind names; a kind → name, extension, mime; a choice's kind → name, values).
3. The view, `BuildTypeEntries` and module's `Schema` go (tests that only pin them go too).
4. `goal.Comment` is the one description (`goal.Description` goes); the hash covers comments.
5. The 357 goal files move their description above the name; os/ `.pr` re-saved (diff-checked).
6. `start.md`: the four template renames, SetLayout's path, the repo pairs and the test that they match.
