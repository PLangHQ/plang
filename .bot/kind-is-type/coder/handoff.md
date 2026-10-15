# coder handoff — kind-is-type (paused mid-stage-4-json)

Paused per Ingi (save usage). Green is pushed; render WIP is uncommitted (red). Resume at commit 2.

## Where things are (branch `kind-is-type`)

**Pushed / safe:**
- `332911aa8` — **commit 1, json uses-parse model** (green producers). json's untyped `Value()` RESTS (`json.Load` → unread `{item,json}` source; `source.Value` answers itself when the kind returns a source). A USE opens it once + narrows (787): `data.Open()` parses a resting source through its kind (`source.Parse(ctx)`), rebinds `_item`, source rides as history (`MaterializeCount == 1`). Use-sites: `source.Get` (nav), `data.EnumerateItems` (foreach). A typed ask stays on `Value()` — the target's own `Create` opens what it needs (`dict.Create` parses, `text.Create` takes chars). `source.Unread(ex, data)` = shared MaterializeFailed story. 2 Wire malformed tests rewritten to touch via `.Get`. GREEN: foreach, parse-once, lazy-nav, url, text-of-kind-json, property-access, JsonStreamSerializer.
- `3f4f569bc` — re-pin `pick_golden.json` (separator left offered types, 783/788). PickListTests 8/8, PickOptionTests 19/19.

**Uncommitted WIP (red — do NOT commit as-is):** `PLang/app/type/item/source.cs` — the **render fix (commit 2)**: `Peek()` and `ToString()` now use `_type.kind.IsText` (the type's own answer) instead of `_type.Name=="text"`, so a text-kinded byte raw (plain text, **json**) decodes to its text face. `TextOfKindJsonTests` 5/5 green → plain-text decoding is NOT broken (architect's form confirmed). **BUT `RenderJsonFileIsItemOfKindJsonTests` still RED** — the text-face change alone didn't fix it. Needs investigation (next step).

## Next steps (in order)

1. **Finish commit 2 (render).** The `.json` template FILE read (`Fluid.cs:51-56`) yields a resting `{item,json}` source; `read.ToString()` gave `"(23 bytes)"` (byte form) → render output was `"(23 bytes)"`. Applied fix: source text-face via `_type.kind.IsText`. **Still red** — so either (a) `read` isn't reaching `source.ToString()`/`Peek()` (check what `await readResult.Value()` actually returns for a .json file — is it a `source`, a `binary`, something else?), or (b) the file-read content path differs from url/decode. DIAG: run RenderJsonFile directly, inspect `read.GetType()` + value. Once green → commit 2 (`coder: render — json source's text face is its type's answer`), rebase+push.
2. **Commit 3 (758 typed-dict slot).** Baseline red `ATypedDictSlot_FirstReadOfTheDict_GoesThroughJsonAndAnswers`. Root (DIAG'd): a `{text,json}` value Declared into a dict slot drops json → `{dict}` kind-empty → `source.Value` hits scalar `value.Reader` → "structured value needs a format parser" → fails. **Architect's shape:** the FORMAT (json) is the raw's own fact, not the declaration's — it rides through `source.Declared` as a field BESIDE `_type` (name it for what it is; **NOT** `{dict,kind:json}` — json isn't dict's kind); the declared type makes itself from what that format parses (`dict.Create` given the parsed value). Near 789's re-type area → **run the full PARALLEL sweep** (`./dev.sh test`), Ingi's stack-overflow catch came from one; say so in the report.
3. **Then (per summary):** navigator deletion (final commit — delete json's Descend/Data/Enumerate/IsSequence/Set/Clr/Output/Write/Scalar + ClrForm; retire 10 old-design tests; the ~25 `Kind(string)` test callers) + the single re-pin pass (`TheCoresSurface_IsThePinnedOne`, `TheSeparatorPage`, `StageOnesAnswer_*`, any json page golden).

## Open items / queued
- **Architect-queued (NOT done):** item's C# host kinds (`kind/reflection` `*`, `kind/dict`, `kind/list`) declare `Internal => true` so the stage-5 builder follow-up doesn't offer them; json stays offered; NOT text's `[Format]` machine formats. One commit with the suite. `Internal` is the static read by `type/list/Registry.cs:63`.
- **Make-loud pin** still needs a genuine old-shape WIRE fixture (pre-779 writer gone) → test-designer.
- Builder bot owns: 2 list `.pr` rebuilds (separator) + stage 5. If I need `os/system/builder/**` or pick/decider code → tell architect first.

## Architect decisions this session
- 3-commit plan for the remaining json work (green producers → render → 758).
- render: `_type.kind.IsText`, the type's own answer (sync, no registry), not a name check or stored flag.
- 758: format (json) rides `source.Declared` as the raw's own fact (interior field), declared type makes from the parsed value; not a kind on dict.
- `data.Open()` internal member accepted as interior plumbing (narrowing is data's).
