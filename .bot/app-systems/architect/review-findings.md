# Reviewer findings on app-systems, as the architect checked them

Each finding the reviewer bots report, checked against the code before it goes anywhere. Status: **fix** (sent to the coder, batched), **carried** (already on `next-branch.md`), **blocks** (to Ingi), **rejected** (with why).

## codeanalyzer (plang-ae), area 1: data and the wire (26fa8f4e7)

| finding | checked | status |
|---|---|---|
| the set-child bridge, `data/this.Navigation.cs:51–63` | cited, on next-branch.md (59's remainder) | carried |
| readers throw their refusal, `data/schema/signature.cs:83` | cited, on next-branch.md (714) | carried |
| parsed json is a clr carrier, `Operator.IsType`'s promise | cited, on next-branch.md (the `{item, json}` door, 717) | carried |
| dead `MaxNormalizeDepth`, `data/this.Output.cs:21` | grep: referenced only at its declaration | fix (batch 1) |
| `IsDataMarked(JsonElement)` on data, `data/this.cs:56` | grep: only caller is `type/item/serializer/json.cs:94,187`; a static taking a `JsonElement` on data is a stray helper and a clr reach; it belongs on the json serializer beside its caller; `internal`, so not a surface change | fix (batch 1) |
