# docs v1 — result

## Written
- `Documentation/v0.2/type-system.md`: json's layout setting; the jsonl kind (names, read, write, the list virtual, bare-token refusal); a kind walkable from a goal.
- `Documentation/v0.2/architecture.md`: serializer list (json layout, jsonl).
- `Documentation/v0.2/cli-changes.md`: `--type.kind.json.setting.layout=compact` among the examples.
- `Documentation/v0.2/good_to_know.md`: one index line.
- `docs/modules/ui.md`: the `| json` line restored (its `<`, `>`, `&` escapes had been lost) and now says the layout follows the setting and that only this filter escapes.

## Checked by running (branch built, `./dev.sh build` exit 0)
- `plang --test plan/json-lines`: ReadsItsLines, RoundTrip, LayoutSetting pass.
- `./dev.sh test`: JsonlKindTests 12/12 (lines, CR LF, blank line skipped, bad line names its number, dict one record, list a line per element, newline in a value stays one line, every character as itself, bare token refused), JsonLayoutSettingTests 8/8, KindWalkWriteProbeTests 5/5.
- `plang --type.kind.json.setting.layout=compact|formatted` accepted; `=bogus` answers `[ChoiceInvalid] 'bogus' is not a layout option. Valid: formatted, compact`.

## Sweep
No other doc says json is compact or HTML-escaped.

## CHANGELOG
- json is written formatted by default (`--type.kind.json.setting.layout=compact` for one line) and writes every character as itself; the `| json` page filter still escapes `<`, `>`, `&`.
- New kind jsonl (`.jsonl`, `.ndjson`): a file of one json value per line reads as a list and writes a list a line per element.
- `%!app.type.kind.json%` reads in a goal.

## Open
- The `file` module's and the json/jsonl type's learner pages are generated from teaching markdown; saying `.jsonl` there is a teaching edit (re-pins the decider fixtures and goldens). Not done; the architect's call.
