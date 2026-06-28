# Stage 3 — thin `source` + the `Cacheable` narrow

**Design authority:** `plan.md` "Phase 3" + Leg B. Firmed up after Stage 2 green + pushed (`4e63ad3cd`). Line numbers verified against `PLang/app/type/item/source.cs` + `PLang/app/data/this.cs`.

## Entry
- ✅ Stage 2 green + pushed.

## Already done (earlier in the branch — verify, don't redo)
- ✅ `source._raw` → `_value` (the field is `_value`, `source.cs:16`).
- ✅ `%ref%` full-match → variable judgement lives in the `text`/`variable` reader (`ReadContext.Template`) — the data reader no longer special-cases it.
- ✅ `IsFinal` unchanged (`=> Template == null`), still driving `dict`/`list` inner re-render.

## Exit (remaining)
- `source.Value(data)` becomes thin: `(item, err) = await app.type.Create(this); if (err) { data.Fail(err); return Absent; } return item` — **no try/catch, no branches** in `source.Value`. The serializer dispatch + the parse-failure authoring move INTO `app.type.Create` (which returns `(item, err)`).
- `Data._type` field (holds the value ITEM, not a type — `this.cs:35`) → rename to `item`. `Data.Value` rebind stays, keyed on `Cacheable`.
- Rename the `item.Value(asking)` parameter → `data` (where still `asking`).

## Dies / Stays
**Dies (in `source.Value`, `source.cs:88-130`):**
- the `try` (`:91`) + `catch … MaterializeFailed` (`:112`) — authoring moves into `app.type.Create`.
- branch 1 inline dispatch `serializers[_format].Read(…)` (`:99`) — moves into `app.type.Create`.
- **branch 2** `else if (_value is string s) → …Convert(s)` (`:103-104`) — the context-less (WireLocal/Judge) fallback. ⚠️ **COUPLED TO STAGE 4:** it exists only for sources born without context. Removing it before context-never-null (Stage 4) would break any context-less source. **Either** confirm no context-less source reaches `Value` (then delete here) **or** leave it as the lone fallback and delete it in Stage 4. Decide at implementation against the live state.
- branch 3 `else return this` (`:106`) — folds away once `app.type.Create` owns the dispatch.

**Stays:**
- `source.Peek` / `Write` / `Navigate` / `IsTruthy` / `Clr` / `Raw` (`:47`).
- `Cacheable` (base + `text`/`dict`/`list`/`path`/`computed` overrides — the narrow needs it).
- `IsFinal` (drives `dict`/`list` inner re-render).
- `module.Cacheable` (unrelated).
- No `Data.Narrow` — the narrow is `Data.Value`'s own line.

## Shipped + deltas from plan
_(coder fills as Stage 3 lands.)_
