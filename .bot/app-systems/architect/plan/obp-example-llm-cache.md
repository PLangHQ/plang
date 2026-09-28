# Worked example, kept unfixed on purpose: the llm query's own cache (`RestoreFromCache`)

Ingi, 2026-09-28: "I see code touch on RestoreFromCache, this is obpv. I dont want to fix it now, I want it to be a learning opportunity, so we can then spot them when we do full sweep on the code base."

**Don't fix this on app-systems.** It stays as it is so the full sweep has a known specimen: the sweep must find it with the tells below. If it doesn't, the tells are wrong.

Read at 40ae1a4e6, `PLang/app/module/action/llm/code/OpenAi.cs`:
- the key, `ComputeCacheKey(messages, model, temperature, schema, format)` (`:164`);
- the read, `settings.Get(CacheTable, key)` (`:165`);
- the restore, `RestoreFromCache` (`:170`, body `:894-949`);
- the store, a hand-built dict (`:455-479`);
- the answer's properties, set again one by one (`SetProp`, `:483` on).

## The one-line diagnosis

**The llm provider does the cache's job, and to do it, it builds an envelope around Data.** Every smell below falls out of that one misplacement. By the meta-test: taking the llm cache out would touch the key, the store, the properties and the restore, four places that are one concept that already exists (the cache, `on.cache`).

## The smells, by name

1. **envelope** (not in the catalog yet, proposed below). `:455` says it outright: "Properties are [JsonIgnore] on Data, so store metadata as the value itself". Then comes `new Dictionary<string, object?> { ["Value"] = …, ["RawResponse"] = …, ["Model"] = …, … }`. That's a parallel wrapper built to carry Data's properties past `[JsonIgnore]`, which CLAUDE.md forbids ("Data is not enveloped … If you find yourself building a parallel type to bypass `[JsonIgnore]`, … add an `[Out]`-aware filter"). The restore then unpacks it by hand (`entry.Name == "Value"`, `:919`).
2. **stored twice**, three times over:
   - **The answer is stored twice:** as `Value` and as `RawResponse`. The restore picks one (`:943-945`), and the comment admits they drift ("trusting the stored Value can yield a list/dict the consumer can't convert", `:924-928`).
   - **The field list is written three times:** the entry dict (`:458-478`), the `SetProp` list after it (`:483` on), and the restore loop (`:917-921`, `:947-948`). Adding one fact means editing three lists.
   - **A second cache:** `llm.query` has its own `Cache` parameter (the only action that does), its own key, its own table and its own entry format, beside `on.cache`, which since 8g is how any action is cached.
3. **fork:** the restore has two paths to one answer (`:943-945`): re-decode `RawResponse`, else the stored `Value`.
4. **fork (type-switch):**
   - `cachedValue switch { dict d => d.Entries(…), clr c => c.Enumerate(…), _ => null }` (`:908-913`): enumerating is the value's own job.
   - The local `AsText` (`:934-941`) switches on `string`, `wire`, `source` and `text` to get text: each value answers its own text.
5. **broken seal:** a cache is a courier. It stores Data and hands it back. Here it opens the stored value by type (`:908`, `:934`), and special-cases a value still encoded on the wire (`:937`, "still encoded: the stored Value stands").
6. **stray helper** and **verb+noun:**
   - `RestoreFromCache`, `ComputeCacheKey`, `SetProp` and the local `AsText` are members that missed their type.
   - Restoring is the cache's job.
   - "What makes two of my calls the same" is the action's own knowledge.
   - Setting a property is Data's job.
7. **clr leak:** `Dictionary<string, object?>`, `SetProp(data, string, object?)` writing CLR objects into Properties, and `t.Clr<string>()`.
8. **wrong store** (a tell, not a catalog name): the cache lives in the *settings* store (`settings.Set(CacheTable, …)`, `:479`). Settings are configuration; a cache is disposable. The parked `.data` brief keeps them apart (`.data/setting/`, `.data/cache/`).

## The direction (when it's fixed, not now)

- `llm.query` is cached like any action, through `on.cache`. The action answers what makes two of its calls the same (its key), as a member the cache asks. The cache doesn't compute it.
- The cache stores the Data whole. The properties that should persist are marked for the store view, as with every other Data. No envelope, no second copy of the answer, no restore method.

## Grep tells for the full sweep (checked against the tree at 40ae1a4e6)

| Tell | Command | Hits now |
|---|---|---|
| a Data shape built by hand | `grep -rn '\["Value"\]\|Name == "Value"' PLang/app --include=*.cs \| grep -v PLang/app/data/` | only this example (`:466`, `:919`) |
| a comment working around `[JsonIgnore]` | `grep -rn '//.*JsonIgnore' PLang/app --include=*.cs`, then read each: the smell is a comment saying the code goes *around* it ("…is [JsonIgnore], so store…") | 7 hits; this example is `:455` and `:892`; the other five explain a legitimate attribute |
| a non-setting kept in the settings store | `grep -rn 'settings\.\(Set\|Get\)' PLang/app/module --include=*.cs`, then read each table name | `LlmCache` (this); `LlmConfig`, `DeciderConfig` are real settings |
| a module caching on its own | a `Cache` slot on an action handler | only `llm/query.cs` |
| verb + preposition + noun | `grep -rnE ' (Restore\|Store\|Save\|Load\|Read\|Write)(From\|To\|In\|Into)[A-Z]\w*\(' PLang/app --include=*.cs` | only `RestoreFromCache` |
| a type-switch in a courier | `switch {` whose arms are `global::app.type.item.<x>.@this` outside that type's own folder | this example; the sweep will find more |
