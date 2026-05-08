# Stage 12 — coder plan (`build-branch-to-build-this`)

Move the 33-line build-mode bootstrap block out of `App.Start` and into
`Build.@this.RunAsync()`. App.Start's branch becomes one line.

## Files

- `PLang/App/Build/this.cs` — add `public async Task<Data.@this> RunAsync()`. Body mirrors today's logic (app.pr existence check, headless guard, y/n prompt for new-app creation, channel-wiring guard, switch CurrentActor → User, dispatch the system Build goal). All `app.X` reaches use the existing `_app` field.
- `PLang/App/this.cs` — `App.Start`'s `if (Build.IsEnabled) { ... }` block (33 lines, lines 480–513) collapses to `if (Build.IsEnabled) return await Build.RunAsync();`.

## Verification

- `App.Start`'s build branch is one line.
- `Build.@this.RunAsync()` exists.
- C# 2755/2755; PLang 199/199; build clean.
