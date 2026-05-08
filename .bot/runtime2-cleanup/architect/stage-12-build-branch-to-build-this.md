# Stage 12: `build-branch-to-build-this`

**Read first:**
- `plan/principles.md` — OBP discipline; specifically smell #4 (allocate-here / mutate-there / clean-up-elsewhere — App.Start has Build-mode logic that should live on Build).
- `plan/scope-map.md` — `app.Build` is App-level (shared); same scope, just gains a method.

**Goal:** Move the Build-mode branch (~25 lines) out of `App.Start` and into `Build.@this.RunAsync()`. App.Start's branch shrinks to one line: `if (Build.IsEnabled) return await Build.RunAsync();`. Build owns the prompt logic and the build-mode goal call; App.Start becomes a clean dispatcher.

**Scope:**
- *Included:* extract the entire `if (Build.IsEnabled) { ... }` block at App.this.cs:481–513 into `Build.@this.RunAsync()`. Build.@this gains the method; App.Start's branch becomes a one-liner. The `CurrentActor = User;` switch moves into Build.RunAsync (it's part of the build-mode bootstrap, not a generic App.Start concern).
- *Excluded:* anything else in App.Start (the goal-file resolution, the actor switch for non-build runs). Other stages can clean those if needed.

**Deliverables:**
- `PLang/App/Build/this.cs`:
  - Add `public async Task<Data.@this> RunAsync()` containing the extracted block. Build already has `(App.@this app)` ctor (line 54) so it can reach `_app.User`, `_app.User.Channels`, `_app.RunGoalAsync`, etc. via its existing App field.
  - The body mirrors today's logic: y/n prompt for new-app creation (if `!Create` and no `.build/app.pr`), then sets `CurrentActor = User`, then runs the build goal via `app.RunGoalAsync(buildCall, app.User.Context)`.
- `PLang/App/this.cs`:
  - Replace lines 481–513 (the entire `if (Build.IsEnabled) { ... }` block) with a single line:
    ```csharp
    if (Build.IsEnabled) return await Build.RunAsync();
    ```
- C# tests pass: `dotnet run --project PLang.Tests`.
- PLang tests pass from a clean rebuild: `cd Tests && ../PlangConsole/bin/Debug/net10.0/plang --test`.

**Dependencies:** None. Independent of stage 11; either order works. Both touch App.this.cs but in different sections (Errors construction near line 165/297 vs Build branch near line 481–513).

## Design

### The smell this closes

App.Start has been collecting bootstrap logic for every mode (build, test, normal goal-run). The build branch is a 33-line block of build-specific concerns: file-existence check, headless detection, channel-wiring guard, y/n prompt, goal-call construction. None of that is App's concern — it's Build's concern. App.Start should be a dispatcher that routes by mode; each mode owns its own bootstrap.

The same realignment lands in stage 4's earlier theme — App.DisposeAsync stops doing manual cleanup; sub-systems own their own dispose. Here: App.Start stops doing manual build-mode bootstrap; Build owns its own bootstrap.

### The new shape

**`Build.@this`:**

```csharp
// Build/this.cs already has:
public @this(App.@this app) { _app = app; }

// (Verify the field name is _app or app — read existing ctor at line 54+. The
// brief uses _app below; adapt to whatever the actual field is named.)

public async Task<Data.@this> RunAsync()
{
    // Safety check: confirm new app creation if no app.pr exists.
    // --app={"create":true} skips the prompt.
    var appPrPath = _app.FileSystem.ValidatePath(".build/app.pr");
    if (!_app.FileSystem.File.Exists(appPrPath) && !_app.Create)
    {
        if (Console.IsInputRedirected)
            return Data.@this.FromError(new global::App.Errors.ServiceError(
                $"No app found at {_app.AbsolutePath}. Run plang build from your app's root directory, or use --app={{\"create\":true}}.",
                "NoAppFound", 400));

        var outputChannel = _app.User.Channels.Get(global::App.Channels.@this.Output)
                            as global::App.Channels.Channel.Stream.@this;
        var inputChannel = _app.User.Channels.Get(global::App.Channels.@this.Input)
                            as global::App.Channels.Channel.Stream.@this;
        if (outputChannel == null || inputChannel == null)
            return Data.@this.FromError(new global::App.Errors.ServiceError(
                "Default channels not wired — cannot prompt for app creation.",
                "MissingRequiredChannelAtBoot", 500));

        await outputChannel.WriteTextAsync($"No app found at {_app.AbsolutePath}. Create new app? (y/n): ");
        using var reader = new StreamReader(inputChannel.Stream, leaveOpen: true);
        var answer = (await reader.ReadLineAsync())?.Trim().ToLowerInvariant();
        if (answer != "y" && answer != "yes")
            return Data.@this.FromError(new global::App.Errors.ServiceError(
                "Build cancelled. Run plang build from your app's root directory.",
                "BuildCancelled", 400));
    }

    _app.CurrentActor = _app.User;
    var buildCall = new GoalCall { Name = "Build", PrPath = "/system/builder/.build/build.pr" };
    return await _app.RunGoalAsync(buildCall, _app.User.Context);
}
```

**`App.this.cs`** (lines 481–513 replaced):

```csharp
// Today (33 lines):
if (Build.IsEnabled)
{
    var appPrPath = FileSystem.ValidatePath(".build/app.pr");
    if (!FileSystem.File.Exists(appPrPath) && !Create) { /* prompt */ }
    CurrentActor = User;
    var buildCall = new GoalCall { ... };
    return await RunGoalAsync(buildCall, User.Context);
}

// After (one line):
if (Build.IsEnabled) return await Build.RunAsync();
```

### Files touched + caller propagation

**Files modified (2):**
- `PLang/App/Build/this.cs` — gain `RunAsync()` method (~30 lines).
- `PLang/App/this.cs` — Build branch in App.Start collapses from 33 lines to 1.

**No external caller migration.** The behavior is hidden inside App.Start's dispatch; outside callers don't see the change.

### Risk + dependencies

**Risk: low-medium.** Mostly a copy-paste extract. The risk is in subtle reaches that don't translate cleanly:

1. **`_app.CurrentActor = _app.User;`** — `CurrentActor` is on App, settable internally. Verify it's accessible from inside Build (same assembly; same partial-class access — should be fine).
2. **`_app.RunGoalAsync(...)`** — same accessibility. App's RunGoalAsync is `public`.
3. **`Console.IsInputRedirected`** — static check, works anywhere.
4. **Imports / using statements** — Build/this.cs may need new usings for `App.Channels.@this`, `App.Channels.Channel.Stream.@this`, `App.Errors.ServiceError`, `GoalCall`. Compiler complains if missing.
5. **The `_app.FileSystem.ValidatePath` call** — verify FileSystem is exposed from App as a public property (it is — App.this.cs:148).
6. **Build's existing methods (SnapshotPrFile, GetPrSnapshot)** — these stay; RunAsync is additive.

**Dependencies: none.** Independent of stage 11.

### Tests

**No new tests required.** Behavior preserved.

**Existing test coverage to verify:**
- `PLang.Tests/App/Build/` if it exists.
- Boot/start tests that exercise `--builder` mode.
- `Tests/` — full PLang suite.

**Definition of done:**
- `dotnet build PlangConsole` clean.
- `dotnet run --project PLang.Tests` green (baseline 2755/2755).
- `cd Tests && ../PlangConsole/bin/Debug/net10.0/plang --test` green from a fresh rebuild (baseline 199/199).
- `App.Start`'s `if (Build.IsEnabled)` block is one line: `return await Build.RunAsync();`.
- `Build.@this.RunAsync()` exists.

### Watch for (coder eyes-on)

- **The `_app` field name** in Build.@this — verify by reading line 54+. If the existing ctor stores it as a different name (e.g., `_engine`, `app`), use that name throughout the new method.
- **`CurrentActor` setter accessibility** — should be internal-set or public-set, accessible from the same assembly. Verify before assuming.
- **The Channel cast pattern** (`as Channel.Stream.@this`) — there's a flagged smell in stage 1's brief about these casts. Stage 12 just preserves them; the cast cleanup is a future concern.
- **Other modes' bootstrap branches in App.Start** — the test mode has a similar shape (`if (Testing.IsEnabled) ...`). Stage 12 doesn't touch test mode; if you see a similar extraction opportunity, flag it for a future stage but don't fold in.

### Stages that follow this one

- **Stage 11** (`errors-app-backref-drop`) — same Tier 3 batch; independent.
- **Stage 13** (`settings-collection-rework`) — also Tier 3, but bigger; carved separately later.
- **Stage 22** (`app-shortcuts-drop`) — caller sweep; can fit anywhere in the remaining sequence.

### Out of scope

- Test mode extraction (parallel pattern, but plan doesn't have a corresponding stage today).
- Any change to `Build.@this`'s existing surface (`SnapshotPrFile`, `GetPrSnapshot`, `IsEnabled`, etc.) — stays as today.
- The Channel cast pattern in the prompt logic — preserved; stage 1's "Watch for" already flagged it.

## Commit plan

```
runtime2-cleanup stage 12: Build-mode bootstrap moves to Build.@this

App.Start had a 33-line `if (Build.IsEnabled) { ... }` block doing
build-mode-specific bootstrap: app.pr existence check, headless
detection, y/n prompt for new-app creation, channel-wiring guard,
goal-call construction. None of that is App's concern.

Build.@this gains a public async Task<Data.@this> RunAsync() that
holds the extracted block. Build already has an App back-ref via
its ctor (Build.@this(App app)), so all the app.X reaches inside
the moved block become _app.X.

App.Start's build branch shrinks to one line:
  if (Build.IsEnabled) return await Build.RunAsync();

App.Start is a cleaner dispatcher; Build owns its own bootstrap.
```
