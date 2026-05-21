# Coder v5 — filesystem-permission

## Version
v5 (Scenario4 fixed: persisted grants survive `new App()` on the same root)

## What this is

The deferred Scenario4 bug from v4. The `[Skip]` reason claimed an STJ
deserialiser stack overflow in `SmallObjectWithParameterizedConstructorConverter`
when a fresh App loaded the persisted row. **That diagnosis was wrong** — the
serialised JSON is clean, small, and round-trips fine. The actual blocker was
much simpler: `PermissionRecord.AppId` scoped grants to a per-instance
`App.Id` (a fresh GUID per `new App()`), so app2 never found app1's grant
even though the sqlite row was right there.

## Diagnosis walk-through

Wrote a one-shot dump test that:
1. Added a signed grant via app1.
2. Read raw sqlite cell directly (proves the bytes are on disk, not in-memory).
3. Called `Find` on a fresh `App` instance.

Dump output:

```
[dump] db path: /tmp/plang-dump-X/.db/system.sqlite      ← real file
[dump] data = {"value":{"appId":"cf2e2c658d3a",...},      ← app1's GUID
                "name":"","type":"permission","signature":{...ed25519...}}
[dump] same-app find  = FOUND
[dump] fresh-app find = null                              ← not a crash
```

No stack overflow, no deserialiser issue — just `TryCover` returning false on
the `grant.AppId == request.AppId` check. Confirmed by adding `app1.Save() +
app2.Load()` round-trip — once both Apps share the same `Id`, the fresh-app
find returns FOUND.

## Fix

Drop `AppId` from `PermissionRecord` and the cover check. Grants are now
identified by `(Actor + Path + Verb)`, with the per-actor sqlite store
providing the root scope. App-level isolation comes from the store living
under the root directory, not from per-instance GUIDs.

`App.Id` itself stays — it's still used to scope in-memory sqlite for
parallel tests (`Sqlite.InMemory($"system-{Id}")`), and remains useful in
logs. Only the permission system unhooks from it.

## Files changed

- `PLang/App/FileSystem/Permission/this.cs` — record dropped from 5 args to 4
  (removed `string AppId`); `Covers` no longer compares `AppId`.
- `PLang/App/Actor/Permission/this.cs` — `Find` request construction lost
  `_actor.App.Id`; `TryCover` lost the AppId equality guard; `Revoke`'s
  in-memory `FindIndex` lost the AppId clause.
- `PLang/App/FileSystem/Path.Authorize.cs` — `BuildRequest` no longer passes
  `AppId: Context!.App.Id`.
- All call-site updates in tests (PermissionCoversTests,
  PathAuthorizeTests, ActorPermissionStorageTests, NarrowVerbRoundTripTests,
  Stage5MessagesEndToEndTests).
- `Stage5MessagesEndToEndTests.Scenario4_RestartStillNoPrompt_*` — `[Skip]`
  removed; real body added that constructs app1, grants via "a", constructs
  app2 on the same root, asserts the second read succeeds with no prompt.
- `PathAuthorizeTests.Authorize_ConstructedPermission_*` — renamed
  (`HasExpectedAppId...` → `HasExpected...`); the AppId assertion removed.

## Suite state

- C# (`dotnet run --project PLang.Tests`): **2853 pass, 0 skip, 0 fail**

Scenario4 went green. The whole "a" answer's "always allow" semantics now
holds across App restarts on the same root.
