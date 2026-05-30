# Coder summary — singular-namespaces

**Latest version:** v2 (response to tester v1)

## What this is

Singular-namespaces refactor across 4 stages: namespace rename, non-null invariants, accessor reshape, type-entity move. v1 landed Stages 1–4 with explicit Stage 2 nullability + Stage 4 fold-dissolve deferrals; tester v1 returned 7 findings (1 CRITICAL each on Stages 2/4, 4 MAJOR, 1 MINOR). v2 addresses all 7.

## What was done in v2

### Stage 2 nullability — completed

- 5 structural back-refs (`step.Goal`, `channel.Actor`, `channel.Channels`, plus App back-refs on `goal`/`module`/`error`) flipped non-null with `= null!`.
- **`Data.Context` flipped non-null** (last of the architect's "9 Context fields"). `_context` field declared `= null!`; public `Context` non-null. Internal `_context == null` defensive guards stripped where dead; `EnsureSigned`'s throw kept (real producer-stamping contract enforcement).
- **`type.@this.Null` sentinel** replaces the historical `Data.Type == null` state. `Data.Type` is non-null end-to-end. Wire converter skips Null emission so the on-wire shape is unchanged. The Type setter clears `_type` when assigned the Null sentinel — call sites copy `source.Type` unconditionally.
- `type.@this.Promote()` throws on unstamped non-primitive reads (silent return was a footgun). Primitive-fallback path marks `_foldLoaded = true` at construction so `app.Type["string"].ClrType` stays callable without a stamped App.
- Producer stamping at SQLite rehydration: `Permission.Find()` stamps `_actor.Context` on grants returned from `SettingsStore.GetAll`.
- Routed Sqlite RehydrateValue + variable/set ValidateBuild through `data.Type.ClrType` (the entity's own resolver) — no consumer-side second fallback chain.
- The legitimate fixture-supporting `App?.Type ?? GetTypeNameStatic` chains in `module/this.cs` Describe stay (test fixtures `new module.@this()` mint without App; documented no-App surface).

### Test-quality fixes (F2–F7)

- **F2**: `BuilderSchemaGoldenTests.BuilderCatalog_*` now pins SHA256 of both `schema.ToJson(indent:false)` and `schema.TypeSchemas`. Length sanity guards added.
- **F3**: `DataType_OnStampedData_ResolvesDomainType_ViaRegistry_NotStaticFallback` uses `"path"` (registry-only) instead of `"int"` (resolvable via both paths); asserts ClrType resolves to `app.type.path.@this`.
- **F4**: `DataTypeReadsEntity.test.goal` now uses `set %name% = "alice", type=text/plain` + `assert %name!Type% equals "text/plain"` — exercises the type entity through PLang's `!` navigator.
- **F5**: `ChannelIndexMissThrows.test.goal` uses a literal absent channel name (`absent-channel-xyz`), captures `%!error.Key%` in the on-error clause, and asserts `equals 'ChannelNotFound'`.
- **F6**: `Stage0_BuildMethodTests` annotated `[NotInParallel]` — `BuildOrdered.InvocationLog` is process-static; per-instance log infeasible since handlers construct per invocation.
- **F7**: `ChannelWriteThroughAccessor` moved to a subfolder with sibling `Capture.goal`. Test registers channel `"log"` → Capture goal, writes through it, asserts `%captured%` matches the written value.

## Files modified

### Production (data + type)
- `PLang/app/data/this.cs` — `Context` non-null; `Type` non-null returning `type.Null`; Type setter clears `_type` on Null sentinel assignment; defensive `_context == null` guards stripped.
- `PLang/app/data/this.Navigation.cs` — defensive `_context != null` guard removed.
- `PLang/app/data/this.Transport.cs` — `_context == null` early-returns in Wrap/CompressAsync stripped (kept Type-null guard, which is a different invariant).
- `PLang/app/data/Wire.cs` — emit-type guard now checks `data.Type.IsNull`.
- `PLang/app/type/this.cs` — `Null` static, `IsNull` helper, 2-arg ctor marks `_foldLoaded = true`, Promote throws on unstamped non-primitive reads.

### Production (other)
- `PLang/app/actor/permission/this.cs` — stamps Context on grants from SQLite.
- `PLang/app/module/settings/Sqlite.cs` — RehydrateValue uses `data.Type.ClrType` / `data.Type.IsNull`.
- `PLang/app/module/variable/set.cs` — ValidateBuild uses `value.Type.ClrType`; `minted.Type = Value.Type` unguarded (setter absorbs Null-means-clear).
- 5 back-ref classes (`step/this.cs`, `channel/this.cs`, `channel/list/this.cs`, `goal/this.cs`, `module/this.cs`, `error/Error.cs`) — `= null!` declarations.

### Tests
- `PLang.Tests/App/SingularNamespaces/BuilderSchemaTests/BuilderSchemaGoldenTests.cs` — F2 byte-diff with pinned hashes.
- `PLang.Tests/App/SingularNamespaces/NullabilityTests/NonNullInvariantTests.cs` — F1/F3 rewrites.
- `PLang.Tests/App/DataTests/DataTests.cs` — `Type_NullValue_ReturnsNull` renamed to `_ReturnsNullSentinel`.
- `PLang.Tests/App/TypedReturnsTests/Stage0_BuildMethodTests.cs` — `[NotInParallel]`.
- `Tests/SingularNamespaces/DataTypeReadsEntity.test.goal` — F4 rewrite + .pr rebuilt.
- `Tests/SingularNamespaces/ChannelIndexMissThrows.test.goal` — F5 rewrite + .pr rebuilt.
- `Tests/SingularNamespaces/ChannelWriteThroughAccessor.test.goal` → moved to `ChannelWriteThroughAccessor/Start.test.goal` + `Capture.goal` for the channel-callee.

## Code example — the Null sentinel pattern

```csharp
// type/this.cs — the sentinel
public static @this Null { get; } = new("null", typeof(object));
public bool IsNull => Value == "null";

// data/this.cs — Type getter returns Null instead of literal null
get
{
    if (_type != null) return _type;
    if (_value == null) return type.Null;  // was: return null;
    var typeName = _context?.App.Type.Name(_value.GetType())
                   ?? AppTypes.GetPrimitiveName(_value.GetType())
                   ?? _value.GetType().Name.ToLowerInvariant();
    var derived = new type(typeName);
    derived.Context = _context;
    _type = derived;
    return _type;
}

// data/this.cs — Type setter absorbs the "Null means clear" rule
set
{
    _type = value.IsNull ? null : value;
    if (!value.IsNull) value.Context = _context;
}

// Wire — skip emission for the sentinel; on-wire shape unchanged
if (!data.Type.IsNull) writer.WriteString("type", data.Type.Value);

// Call site (variable/set.cs) — unguarded copy; setter handles Null sentinel
minted.Type = Value.Type;
```

## Tests

- C# (TUnit/.NET 10): 3694/3694.
- PLang: 253/253. (HTTP tests fail intermittently against an external server; not on this branch's surface.)

## What's still in flight

Nothing on the v1 finding list. Stage 3b's full property rename (`app.Goals → app.Goal` sweep) remains explicitly deferred per coder v1 report — Roslyn-based symbolic rename out of v2 scope.
