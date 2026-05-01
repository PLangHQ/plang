# v2 review summary (what I PASSed last round)

`v2` reviewed coder commit `60b8d1f3 coder v1 review: address codeanalyzer/v1
findings`. All four v1 findings were closed:

1. `AsCanonical` dead `if (!resolved.Success)` collapsed.
2. `WrapAs` IEnumerable transient inlined.
3. `Variables.Set` `dv.Type = type` mutation deleted.
4. Three JSON-roundtrip clones extracted to `Data.SnapshotClone(object)` +
   `Json.SnapshotClone` options.

I closed v2 with **CLEAN**, flagging two cosmetic carryovers (`??` defensive
fallbacks in `set.cs:117–118`, `global::` qualification in 3 sites) and one
behavioral observation: the `SnapshotClone` extraction quietly added
`UnwrapJsonElement` at two callsites that previously omitted it
(`Variables.cs` dot-path, `list/add.cs`). The new behavior is correct (no
JsonElement leaks downstream); the commit message framed it as pure dedup
which it wasn't.

## What's new in v3

`v3` reviews coder commit `24cba238 coder v2: nested %var% resolution in
plain Data + JsonNode conversion`. This is **new bug-fix work**, not a
review response — coder ran `plang --test`, found the LLM builder NRE,
traced it to two value-resolution gaps, and fixed both. The v2 cosmetic
carryovers (`??` fallbacks, `global::` prefixes) are still present but
weren't touched.

## What changed structurally

- `PLang/App/Data/this.cs` — `WalkContainerVars` + `IsWalkableContainer`
  extracted as private statics. `AsCanonical` now walks
  `IList<object?>` / `IDictionary<string, object?>` shapes for nested
  `%vars%`, returning a fresh transient. `AsT_Impl` refactored to reuse
  the same helper.
- `PLang/App/Utils/TypeConverter.cs` — `JsonNode` added to the
  complex-source dispatch (covers `JsonObject` / `JsonArray` / `JsonValue`
  which don't implement `IDictionary<string, object?>`). Parallel
  `JsonArray` element-iteration arm added, mirroring the existing
  `JsonElement`-array arm.
- 6 new tests pin both surfaces.
