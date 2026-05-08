# Stage 14: `timespan-iso-8601-sweep`

**Read first:**
- `plan/principles.md` — OBP discipline.
- `plan/scope-map.md` — Callback is shared (App-level config holder); type change doesn't change scope.

**Goal:** Replace `int? ExpiresInMs` with `TimeSpan? Expires` on `Callback.Signature.@this` and the corresponding parameter on `App.modules.signing.sign`. Wire the `TimeSpanIso8601` converter (exists in `App/Channels/Serializers/`) where the parameter serializes/deserializes. Closes the 2026-05-06 TODO in `Documentation/Runtime2/todos.md`.

**Why:** `int? ExpiresInMs` requires the LLM (and any human writer of `.goal` files or `app.callback.expires` settings) to count zeros — error-prone for "5 minutes," "1 hour," "30s." `TimeSpan?` parsed/rendered as ISO 8601 (`PT5M`, `PT1H`, `PT30S`) is unambiguous and well-known to LLMs.

**Scope:**
- *Included:* type change + property rename (`ExpiresInMs` → `Expires`) on `Callback.Signature.@this`; same on `App.modules.signing.sign` (the action record); update all use sites; verify the ISO 8601 JsonConverter wires through serialization paths the property touches.
- *Excluded:* other `*Ms` properties in the codebase. `CacheSettings.DurationMs` (Step/CacheSettings.cs:9) and `RetryOverMs` (modules/error/handle.cs:78) have similar shape but aren't in stage 14's plan-stated scope. Flag for future stages if anyone takes them on.

**Deliverables:**

### Property changes

**`PLang/App/Callback/Signature/this.cs:14`:**

```csharp
// Today:
public int? ExpiresInMs { get; set; }

// After:
public TimeSpan? Expires { get; set; }
```

**`PLang/App/modules/signing/sign.cs`** (find the property; today it's a `Data.@this<int>? ExpiresInMs`):

```csharp
// Today (likely shape):
public partial Data.@this<int>? ExpiresInMs { get; init; }

// After:
public partial Data.@this<TimeSpan>? Expires { get; init; }
```

The action record's `[LlmBuilder]` attribute (or whatever metadata the source generator reads) handles the per-property JsonConverter wiring. If the converter doesn't auto-attach to `TimeSpan` properties globally, the property may need an explicit `[JsonConverter(typeof(TimeSpanIso8601))]` attribute. Verify by reading how today's `int?` properties get their default int handling — the `TimeSpan` path should be similarly automatic if the converter is registered globally.

### Use-site updates

**`PLang/App/Data/this.Envelope.cs`** (around line 87 today):

```csharp
// Today:
var expiresInMs = Value is ICallback
    ? _context.App.Callback.Signature.ExpiresInMs
    : (int?)null;
// ...
var action = new App.modules.signing.sign
{
    Data = this,
    ExpiresInMs = expiresInMs.HasValue
        ? new @this<int>("", expiresInMs.Value) : null
};

// After:
var expires = Value is ICallback
    ? _context.App.Callback.Signature.Expires
    : (TimeSpan?)null;
// ...
var action = new App.modules.signing.sign
{
    Data = this,
    Expires = expires.HasValue
        ? new @this<TimeSpan>("", expires.Value) : null
};
```

### Caller sweep

- `grep -rn "ExpiresInMs" PLang/ PLang.Tests/ Tests/ --include='*.cs' --include='*.goal'` — find all remaining references. After the sweep, all should be either renamed (to `Expires`) or removed.
- Any PLang `.goal` files setting `expiresInMs` (lowercase) — sweep to `expires`. PLang's variable-name resolution is case-insensitive but the convention should follow.

### Verify the converter is wired

The `TimeSpanIso8601` converter exists today at `PLang/App/Channels/Serializers/TimeSpanIso8601Converter.cs`. It's registered globally on JsonSerializerOptions per the comments in stage 1's brief and the existing `Modifiers` setup. After the property changes, JSON serialization of `Callback.Signature` and the `signing.sign` action should produce ISO 8601 strings (`"PT5M"`, `"PT1H30M"`, `"PT30S"`) for the Expires field.

If the LLM-builder catalog re-renders this property's type tag, it should now read `[duration]` or similar instead of `[int]`. Check `App/Modules/Schema/Render.cs` (or wherever the formal-syntax type tags get derived) — `TimeSpan` properties might need a special-case to render `[duration]`. If today's renderer falls back to `[TimeSpan]` or similar, that's acceptable too — the LLM can read it.

### Definition of done

- `dotnet build PlangConsole` clean.
- `dotnet run --project PLang.Tests` green (baseline 2755/2755).
- `cd Tests && ../PlangConsole/bin/Debug/net10.0/plang --test` green from a fresh rebuild (baseline 199/199).
- `grep -rn "ExpiresInMs" PLang/ PLang.Tests/ Tests/ --include='*.cs' --include='*.goal'` — zero hits.
- Round-trip a `Callback.Signature.Expires = TimeSpan.FromMinutes(5)` through JSON serialization — produces `"PT5M"` (or equivalent ISO 8601 form).

**Dependencies:** None on stages 11/12/13 specifically. Independent.

## Design

### The smell this closes

`*Ms`-suffixed int properties for durations are an LLM and human ergonomics smell:
- Numbers obscure intent: `300000` doesn't read as "5 minutes" to most humans.
- Easy to miscount zeros — off-by-1000 errors at 1-minute or 1-hour boundaries.
- The Ms suffix is a workaround for a missing type; the right type carries its own units.

`TimeSpan` + ISO 8601 (`PT5M`) is self-describing. The format is well-known to LLMs (taught directly in their training data).

### Files touched

**Files modified (3):**
- `PLang/App/Callback/Signature/this.cs` — property type + name change.
- `PLang/App/modules/signing/sign.cs` — action property type + name change.
- `PLang/App/Data/this.Envelope.cs` — use-site updates.

Plus tests and possibly `.goal` files in `Tests/` that set this property — sweep.

### Risk + dependencies

**Risk: low-medium.** Type change is mechanical; the converter is already wired globally. Possible failure modes:

1. **JSON round-trip mismatch.** If the converter doesn't fire on the new property (e.g., the action record's serialization path doesn't run through the converter-aware `JsonSerializerOptions`), the property serializes as a TimeSpan-default `"00:05:00"` instead of ISO 8601 `"PT5M"`. Run a round-trip test.
2. **PLang `.goal` files with literal numeric values.** If a `.goal` writes `set callback.expires to 300000`, the int parses fine for `int?` but fails for `TimeSpan?`. Update such cases to `set callback.expires to "PT5M"` or `set callback.expires to 5m` (depending on PLang's TimeSpan literal support). Check `Tests/` for any such usage.
3. **The action's `Data.@this<int>` → `Data.@this<TimeSpan>` change** ripples through the source generator. The generator should handle the type change automatically (it's a generic on Data.@this); verify by reading the generated code if anything looks off.

### Tests

**Behavior preserved** for valid existing values; PLang code that wrote integer-ms forms needs migration (1-2 sites likely).

**Existing test coverage to verify:**
- `PLang.Tests/App/Callback/` — callback signing/verifying tests.
- Any test that round-trips a Callback through JSON.
- `Tests/` — full PLang suite.

### Watch for (coder eyes-on)

- **The PLang TimeSpan literal syntax** — check whether PLang has native TimeSpan literals (`5m`, `1h`, `PT5M`) or if `.goal` files have to use ISO 8601 strings. If the latter, `.goal` files with `expiresInMs to 300000` rewrite to something like `expires to "PT5M"`. If PLang doesn't parse ISO 8601 in `.goal` source today, this stage may need to extend the parser — that's a real additional scope. Flag if so and check with the architect.
- **The 2026-05-06 todos.md entry** — update to mark resolved (or remove) when this stage lands.
- **The `int? RetryOverMs` and `int DurationMs` other-Ms cases** — same shape smell, but out of scope. Flag in the commit message for a future stage.
- **The `.goal` builder prompt** — if the LLM builder is currently producing `expiresInMs: 300000`, after this stage the LLM should produce `expires: PT5M`. Verify the catalog re-renders the new type tag so the LLM is taught the new format.

### Stages that follow this one

- **Stage 20** (`channel-app-backref-drop`) — same Tier 4 batch; independent.
- The other `*Ms` properties (`DurationMs`, `RetryOverMs`) are candidate future stages.

### Out of scope

- `CacheSettings.DurationMs` and `RetryOverMs` — flagged for future stages.
- Any PLang parser change beyond what's needed for the migration (if any).

## Commit plan

```
runtime2-cleanup stage 14: ExpiresInMs → Expires (TimeSpan + ISO 8601)

int? ExpiresInMs forced humans and LLMs to count zeros: 300000 is
"5 minutes" but doesn't read that way. TimeSpan? + ISO 8601 ("PT5M",
"PT1H", "PT30S") is self-describing.

Callback.Signature.@this.ExpiresInMs (int?) → .Expires (TimeSpan?).
App.modules.signing.sign.ExpiresInMs (Data<int>?) → .Expires (Data<TimeSpan>?).
Data.this.Envelope.cs use-sites updated.

The TimeSpanIso8601 converter (exists today in Channels/Serializers/)
handles serialization. Round-trip produces "PT5M" / "PT1H" forms.

Closes Documentation/Runtime2/todos.md 2026-05-06 entry.

Out of scope: other *Ms properties (CacheSettings.DurationMs,
RetryOverMs in error/handle) — flagged for future stages.
```
