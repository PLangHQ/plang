# [Out] attributes per Data<T> payload type

Working from Ingi's rule: **"for FilePath, Relative is fine, Absolute is not"** — the wire carries what's *portable* and *meaningful across processes*, not what leaks local state or is derivable.

Three categories per property:

- **Out** — ship on the wire
- **Skip (derived)** — receiver can recompute from another property; sending it is bytes wasted and a drift risk
- **Skip (local)** — meaningful only on the originating host (filesystem layout, runtime caches, parser breadcrumbs)
- **Skip (sensitive)** — secrets; already covered by `[Sensitive]`
- **Skip (cycle)** — already `[JsonIgnore]` for cycle/runtime-graph reasons; carry over

## Inventory & proposals

### 1. `app.modules.identity.types.Identity`

| Property     | Today        | Proposal     | Rationale                                                                 |
|--------------|--------------|--------------|---------------------------------------------------------------------------|
| Name         | [LlmBuilder] | **[Out]**    | Public identifier                                                         |
| PublicKey    | [LlmBuilder] | **[Out]**    | Whole point of the type — receivers verify signatures with this           |
| PrivateKey   | [Sensitive]  | Skip         | Sensitive — never crosses the wire                                        |
| IsDefault    | [LlmBuilder] | Skip (local) | "Default identity" is a per-app-instance notion; not meaningful elsewhere |
| IsArchived   | [LlmBuilder] | Skip (local) | Local management state                                                    |
| Created      | [LlmBuilder] | Skip (local) | Per-host clock — receiver doesn't use it for verification or trust decisions |

### 2. `app.types.path.@this` (base for FilePath, HttpPath)

| Property              | Today       | Proposal       | Rationale                                                                          |
|-----------------------|-------------|----------------|------------------------------------------------------------------------------------|
| Scheme                | none        | **[Out]**      | Receiver needs to know which subclass to reconstruct                               |
| Raw                   | none        | Skip (local)   | Build-time input; not part of canonical form                                       |
| Absolute              | none        | Skip (local)   | **Leaks local filesystem layout** — this is the security carve-out Ingi called out |
| Relative              | none        | **[Out]**      | Portable canonical form                                                            |
| Extension             | [LlmBuilder]| Skip (derived) | Extrapolated from Relative                                                         |
| FileName              | [LlmBuilder]| Skip (derived) | Extrapolated from Relative                                                         |
| FileNameWithoutExt    | [LlmBuilder]| Skip (derived) | Extrapolated from Relative                                                         |
| Directory             | [LlmBuilder]| Skip (derived) | Extrapolated from Relative                                                         |
| MimeType              | [LlmBuilder]| Skip (derived) | Depends on receiver's mime registry; extrapolate locally                           |
| IsFile                | [LlmBuilder]| Skip (derived) | Extrapolated from Extension                                                        |
| IsDirectory           | [LlmBuilder]| Skip (derived) | Extrapolated from Extension                                                        |
| GoalCall              | [JsonIgnore]| Skip (cycle)   | Cycles                                                                             |
| Content               | none        | Skip (local)   | Runtime byte cache; receiver re-reads if it wants bytes                            |
| Source                | none        | Skip (local)   | Copy/move breadcrumb — runtime-only                                                |
| Context               | [JsonIgnore]| Skip (cycle)   | Runtime graph                                                                      |

**Net on-wire shape:** `{ Scheme, Relative }`. Receiver runs `path.Resolve(Relative, context)` and everything else regenerates.

### 3. `app.modules.list.types.list`

| Property | Today | Proposal | Rationale                                       |
|----------|-------|----------|-------------------------------------------------|
| count    | none  | **[Out]**| Convenience — saves receiver from .Count() walk |
| value    | none  | **[Out]**| The list itself                                 |

### 4. `app.variables.Variable`

| Property          | Today | Proposal       | Rationale                                            |
|-------------------|-------|----------------|------------------------------------------------------|
| Name              | none  | **[Out]**      | The canonical identifier                             |
| RawValue          | none  | Skip (local)   | Original raw expression — parser artifact            |
| WasPercentWrapped | none  | Skip (local)   | Parser state — irrelevant once the name is extracted |

### 5. `app.data.@this` (Data itself)

| Property     | Today        | Proposal       | Rationale                                          |
|--------------|--------------|----------------|----------------------------------------------------|
| Value        | none         | **[Out]**      | The payload                                        |
| Success      | none         | **[Out]**      | Receiver needs to know if this is a success/failure|
| Error        | none         | **[Out]**      | Failure detail                                     |
| Type         | none         | **[Out]**      | The type label                                     |
| Signature    | [Out] today  | **[Out]**      | Already correctly marked                           |
| RawSignature | none         | **Delete**     | Legacy — was a no-lazy-populate peek when Signature.get auto-signed; after stage 2a.7 removed ICallback, this is now identical to `Signature.get`. Remove the property; migrate the 4 callers (signing.verify, actor/permission, plang serializer) to `Signature` directly. |
| Context      | [JsonIgnore] | Skip (cycle)   | Runtime graph                                      |

### 6. `app.types.path.@this.StatInfo`

| Property | Today | Proposal | Rationale            |
|----------|-------|----------|----------------------|
| Exists   | none  | **[Out]**| Stat result          |
| IsFile   | none  | **[Out]**| Stat result          |
| Length   | none  | **[Out]**| Stat result          |
| Modified | none  | **[Out]**| Stat result          |

### 7. `app.goals.goal.GoalCall`

| Property   | Today              | Proposal       | Rationale                              |
|------------|--------------------|----------------|----------------------------------------|
| Event      | [JsonIgnore]       | Skip (cycle)   | Cycles                                 |
| Name       | [Store, LlmBuilder]| **[Out]**      | Identifier                             |
| Parallel   | [Store, LlmBuilder]| **[Out]**      | Execution mode — part of the contract  |
| Parameters | [Store, LlmBuilder]| **[Out]**      | Bound args                             |
| PrPath     | [Store]            | **[Out]**      | Resolves to a path (which is itself wire-shaped per #2) |
| Action     | [JsonIgnore]       | Skip (cycle)   | Cycles                                 |

### 8. `app.types.path.permission.@this`

| Property | Today | Proposal | Rationale                              |
|----------|-------|----------|----------------------------------------|
| Actor    | none  | **[Out]**| Whole envelope is meant to travel signed |
| Path     | none  | **[Out]**| Subject of the permission              |
| Verb     | none  | **[Out]**| Read/Write/Execute/Delete              |
| Match    | none  | **[Out]**| Match semantics (Exact/Prefix/...)     |

### 9. `app.modules.settings.types.setting`

| Property | Today | Proposal       | Rationale                              |
|----------|-------|----------------|----------------------------------------|
| key      | none  | **[Out]**      | Setting name is observable — receiver can know that "DATABASE_URL" is configured, useful for diagnostics |
| value    | none  | **[Out, Masked]** | Value is replaced with `"****"` on the wire — name travels, secret doesn't |

**The `[Masked]` attribute (new — see cross-cutting #5).** Combines with `[Out]`: the property is included in the normalized tree, but its *value* is replaced with `"****"` rather than the real value. The receiver sees the property exists and its name, but never its content. Distinct from `[Sensitive]` (which excludes entirely). Lets settings be observable-but-redacted.

### 10. `app.http.Response.@this`

| Property | Today | Proposal | Rationale                              |
|----------|-------|----------|----------------------------------------|
| Status   | none  | **[Out]**| HTTP status code                       |
| Headers  | none  | **[Out]**| Response headers                       |
| Body     | none  | **[Out]**| Response body                          |
| Duration | none  | Skip     | Observation, not response payload. Debug mode picks it up automatically (debug = dump everything) |

### 11. `app.modules.output.Ask`

| Property | Today | Proposal | Rationale                              |
|----------|-------|----------|----------------------------------------|
| Answer   | none  | **[Out]**| The whole point — user's response      |

### 12. `app.mock.Mock.@this`

| Property        | Today | Proposal     | Rationale                                          |
|-----------------|-------|--------------|----------------------------------------------------|
| Id              | none  | Skip (local) | Local handle ID — meaningless cross-process        |
| Pattern         | none  | Skip (local) | Local test config                                  |
| CallCount       | none  | Skip (local) | Local counter                                      |
| IsSpy           | none  | Skip (local) | Local mode flag                                    |
| Calls           | none  | Skip (local) | Captured calls — runtime test state                |
| EventBindingId  | none  | Skip (local) | Local handle ID                                    |

**Net:** Mock is test-time only. Probably never serialized cross-process. If it ever is, the answer is "none" and we should question why.

### 13. `app.modules.condition.Operator`

| Property | Today | Proposal       | Rationale                                       |
|----------|-------|----------------|-------------------------------------------------|
| Value    | none  | **[Out]**      | The operator name ("==", "contains", ...)       |
| Evaluate | none  | Skip (delegate)| Can't serialize a `Func<...>`                   |

### Externals (out of scope)

- **`System.Net.Http.HttpContent`** — framework type. If it flows as a payload, the wire form needs a custom adapter; can't tag `[Out]` from outside the type.
- **`System.Reflection.Assembly`** — framework type. Never serialized; references local AppDomain.

## Cross-cutting observations

1. **Default-out vs. opt-in.** Today the codebase uses `[Sensitive]` to *exclude* and `[Out]` to *include*. The proposal above is consistent with `[Out]` as a whitelist — properties without `[Out]` don't ship. That matches what's in `PLang/app/View.cs` already.

2. **Derived properties skip pattern.** All `[LlmBuilder]` properties on `path` (Extension, FileName, etc.) skip on wire because they're derivable from `Relative`. This means `[LlmBuilder]` and `[Out]` are NOT the same set — that's correct. `LlmBuilder` is "show the LLM these for context"; `Out` is "ship across the wire."

3. **The Absolute carve-out.** `path.Absolute` is the one property where the rule isn't just "is it derived" — it's "is it safe." Sending `/home/user/secret/path` across the wire leaks server filesystem layout. The receiver shouldn't need it; if it does, that's a design smell.

4. **Debug mode dumps everything; no per-property `[Debug]` attribute needed.** When the wire serializer runs in debug mode, it bypasses the `[Out]` whitelist entirely and emits every property on every type. The only filters that *still* apply in debug: `[Sensitive]` (always excluded) and `[Masked]` (value still replaced with `"****"`). Properties like `Duration` don't need any tag — they're skipped in production by absence of `[Out]`, and picked up automatically in debug because the filter is off.

   The existing `View.Debug` enum + `[Debug]` attribute in `PLang/app/View.cs` remain for the *other* views that already use them (the LLM-builder pipeline, for instance); the wire serializer just doesn't consult them.

5. **`[Masked]` — new attribute for observable-but-redacted properties.** A property tagged `[Out, Masked]` rides on the wire with its name intact, but its value is replaced with the literal string `"****"`. Distinct from `[Sensitive]` (which excludes the property entirely). The canonical use is `setting.value` — receiver sees that `DATABASE_URL` is configured (useful for diagnostics, monitoring, dashboards) but never sees the secret. `[Masked]` is honored in *both* `Out` and `Debug` views — debug mode never unmasks.

   Joins `[Out]`, `[Sensitive]`, `[In]`, `[Store]`, `[LlmBuilder]`, `[Debug]`, `[Default]` in `PLang/app/View.cs` as a new top-level attribute.

6. **No types need the escape hatch.** With these `[Out]` decisions, every type is reducible to a clean property bag — no type needs to "collapse to a single string" because the canonical property is just one of the marked-`[Out]` fields (path's `Relative`, Variable's `Name`, etc.). The receiver does the right thing via standard normalization + per-type `As<T>` (which already exists for `path` via `Resolve`).

   So my earlier "still open" question — "do we need IDataNormalizable on day one?" — answers itself: **no**, as long as `[Out]` discipline is applied consistently. The interface becomes unnecessary because the property bag form is always rich enough to carry the canonical info, and lean enough to skip the derived noise.
