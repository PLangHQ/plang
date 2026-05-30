# Coder summary — singular-namespaces

**Latest version:** v3 (response to tester v2)

## What this is

Singular-namespaces refactor across 4 stages. v1 landed Stages 1–4 with deferrals. v2 addressed tester v1's 7 findings + Data.Context non-null + Null-type sentinel. Tester v2 returned **fail** on F1-RESIDUAL (Promote throw uncovered, named test verifies the opposite) + 2 minors. v3 addresses all 3.

## What was done in v3

### F1-RESIDUAL — Promote throw is now covered

- **Renamed** `DataType_OnUnstampedData_ThrowsHard_NoSilentFallback` → `ClrType_OnUnstampedDomainType_ReturnsNull`. The body pinned the silent-null ClrType behaviour all along; the name now matches.
- **Added** `TypeFoldRead_OnUnstampedDomainEntity_ThrowsHard` — reads `t.Fields` on an unstamped non-primitive entity, asserts `Throws<InvalidOperationException>`. Mutation-confirmed.
- **Added** `TypeFoldRead_OnPrimitiveEntity_DoesNotThrow_EvenWithoutContext` — pins the primitive-fallback carve-out (`_foldLoaded = true` in the 2-arg ctor).

### N1 — fail-loud guard

`GetPrimitiveOrMime_ExternalFallbackCallSites_AllRemoved` now asserts `File.Exists` instead of silently `continue`-ing past missing files.

### N2 — Capture echoes the payload

`Capture.goal` changed from `set %captured% = "hello from channel accessor"` to `set %captured% = %!data%`. Same pattern as `Tests/Channels/GoalChannelRecursion/EchoBack.goal`. Value-flow now pinned: corrupt the channel and the assertion diverges.

## Files modified in v3

- `PLang.Tests/App/SingularNamespaces/NullabilityTests/NonNullInvariantTests.cs` — rename + 2 new throw-coverage tests + tightened source-grep guard.
- `Tests/SingularNamespaces/ChannelWriteThroughAccessor/Capture.goal` — echoes `%!data%`.
- `Tests/SingularNamespaces/ChannelWriteThroughAccessor/.build/capture.pr` — rebuilt.

## Tests

- C# (TUnit/.NET 10): **3696/3696** (+2 new tests vs v2).
- PLang: **253/253** (intermittent timeout on builder.validate flake; not the F6 fixture race, which is solid 3-of-3 under `[NotInParallel]`).

## What was done in v2 (still load-bearing)

- Stage 2 nullability completed: structural back-refs non-null; `Data.Context` flipped non-null end-to-end; `type.Promote()` throws on unstamped non-primitive reads; Permission/SQLite/variable.set route through the entity's own resolver.
- **Null type sentinel** — `Data.Type` is non-null end-to-end, returning `type.@this.Null` (`IsNull=true`, `ClrType=typeof(object)`) instead of literal null. Wire converter skips Null emission. The Type setter clears `_type` on Null assignment so call sites copy `source.Type` unconditionally.

## Code example — v3's throw-coverage pattern

```csharp
// Three tests, three responsibilities.

[Test] public async Task ClrType_OnUnstampedDomainType_ReturnsNull()
{
    // ClrType chain: _clrType ?? Context?... ?? GetPrimitiveOrMime.
    // For a non-primitive name with no Context the chain falls off → null.
    // Does NOT go through Promote().
    var d = new app.data.@this<int>("", 0, new app.type.@this("not-a-primitive-domain-name"));
    await Assert.That(d.Type.ClrType).IsNull();
}

[Test] public async Task TypeFoldRead_OnUnstampedDomainEntity_ThrowsHard()
{
    // Fields/Values/Example/etc. all route through Promote().  Unstamped
    // non-primitive → throw (the contract codeanalyzer v4 praised as
    // "fail-loud-at-source").  Mutation-confirmed.
    var t = new app.type.@this("not-a-primitive-domain-name");
    await Assert.That(() => { _ = t.Fields; return Task.CompletedTask; })
        .Throws<InvalidOperationException>();
}

[Test] public async Task TypeFoldRead_OnPrimitiveEntity_DoesNotThrow_EvenWithoutContext()
{
    // Primitive-fallback carve-out: 2-arg ctor marks _foldLoaded=true so
    // app.Type["string"].Example is reachable without an App stamped.
    await using var app = new PLangEngine("/test");
    var prim = app.Type["string"];
    await Assert.That(() => { _ = prim.Example; return Task.CompletedTask; })
        .ThrowsNothing();
}
```
