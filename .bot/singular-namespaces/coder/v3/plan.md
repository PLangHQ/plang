# Coder v3 — plan (tester v2 response)

## F1-RESIDUAL — Promote throw coverage

- **Rename** `DataType_OnUnstampedData_ThrowsHard_NoSilentFallback` → `ClrType_OnUnstampedDomainType_ReturnsNull`. The body always pinned the silent-null ClrType behaviour; the name now matches.
- **Add** `TypeFoldRead_OnUnstampedDomainEntity_ThrowsHard` — reads `t.Fields` on an unstamped non-primitive entity and asserts `Throws<InvalidOperationException>`. Mutation-confirmed: replacing the throw with `return this;` makes this test fail.
- **Add** `TypeFoldRead_OnPrimitiveEntity_DoesNotThrow_EvenWithoutContext` — reads `app.Type["string"].Example` and asserts no throw. Pins the primitive-fallback carve-out (`_foldLoaded = true` in the 2-arg ctor); deleting that line surfaces the regression here.

## N1 — fail-loud on missing guard file

Replace `if (!File.Exists(path)) continue;` with `Assert.That(File.Exists(path)).IsTrue()`. Tightens the assertion: if the BaseDirectory→repo-root relative walk ever breaks, the test goes red instead of vacuously green.

## N2 — Capture echoes the channel payload

Change `Capture.goal` to `set %captured% = %!data%`. `%!data%` is the channel payload variable (same pattern as `Tests/Channels/GoalChannelRecursion/EchoBack.goal`). If the accessor delivers the wrong bytes, `%captured%` diverges from the expected literal and the assertion in `Start.test.goal` goes red. Pins value-flow, not just reachability.

## Tests

- C#: 3696/3696.
- PLang: 253/253 (1 intermittent timeout on the same builder.validate flake the runner can't fully suppress; not the F6 fixture race, which is solid 3-of-3 from `[NotInParallel]`).
