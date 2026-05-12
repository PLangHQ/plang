# On enum — what name/path mean per category

The `On` enum is the primary axis. `name:` and `path:` on a binding
have category-specific semantics. This file is the contract.

```csharp
public enum On
{
    App,
    Goal,
    Step,
    Action,
    Read,
    Write,
    Ask,
    Variable
}
```

(Error deferred from this branch.)

## Per-value semantics

### `On.App`
- **Fires from:** App.Start (BeforeAppStart) and App teardown (AfterAppStart). Implementation of those fire sites is Thread 3 (separate branch).
- **`name:`** — null. There's only one App.
- **`path:`** — null.
- **Source Data carries:** the App instance (`new Data<App>(this)`).
- **Use case:** boot-time cross-cutting hooks (channel registration, debug wiring).

### `On.Goal`
- **Fires from:** the engine, before/after invoking a Goal's RunAsync.
- **`name:`** — goal name pattern. `"Start"`, `"Api/*"`, regex with flag.
- **`path:`** — null.
- **Source Data carries:** the Goal instance.
- **Use case:** "log every goal," "block goals matching Admin/*," etc.

### `On.Step`
- **Fires from:** the engine, before/after running a Step.
- **`name:`** — step text pattern. The matcher compares against `Step.Text` (the human-readable step line).
- **`path:`** — null.
- **Source Data carries:** the Step instance.
- **Use case:** "audit every `- send email *`," "fail fast on `- delete *`."

### `On.Action`
- **Fires from:** the engine, before/after dispatching an Action (the runtime invocation of a module's handler).
- **`name:`** — action name pattern, format `"<module>.<action>"`. Examples: `"http.get"`, `"file.*"`, `"*.delete"`.
- **`path:`** — null.
- **Source Data carries:** the Action instance (the record carrying module, action name, parameters).
- **Use case:** rate-limiting all `http.*`, mocking `file.write` in tests.

### `On.Read` / `On.Write` / `On.Ask`
- **Fires from:** Channel methods — `Channel.Read`, `Channel.Write`, `Channel.Ask`.
- **`name:`** — typically null (any channel). Could be a channel-name pattern if the binding wants to filter — but the more idiomatic check is inside the handler: read `source.Value` (a Channel) and inspect `Channel.Name`.
- **`path:`** — null. (If we later want sub-operations like "write a specific format," add path. Not now.)
- **Source Data carries:** the Channel instance. For Write, the source's Properties also carry the payload being written (so handlers can transform it).
- **Use case:** logging every channel write, encrypting payloads before write, transforming asks.

### `On.Variable`
- **Fires from:** `Data.Value` getter (Before/After) and setter (Before/After).
- **`name:`** — root variable name pattern. `"step"`, `"goal"`, `"my*"`.
- **`path:`** — sub-path within the variable. `"Text"`, `"Action.Module"`, `"*.Name"`.
- **Source Data carries:** itself — the Data being read/written.
- **Use case:** trace property accesses, transform values on read, validate on write.

Note: `On.Variable` is the only category where `path:` is meaningful.
Everything else uses null path. Could be encoded as "path is only
declared for `On.Variable`" — but keeping it uniform on the binding
record is cleaner (one struct shape) and the cost of a null field
is nothing.

## Pattern matching rules

- `name:` and `path:` are strings or null.
- **null** means "match any" (wildcard).
- **No wildcard chars** means exact match. `name:"step"` matches only `step`.
- **Glob chars (`*`, `?`)** mean glob match. `name:"step.*"` matches `step.foo`, `step.bar`. Compiled to regex at registration.
- **Regex** requires `isRegex: true` on the binding. PLang surface: `(isRegex: true)` parameter to `event.on`.

Today's heterogeneous `goalNamePattern`/`stepPattern`/`actionPattern`/`channelName` fields all collapse to `name:` with the same glob+regex semantics. Today's `IsRegex` parameter on `event.on` becomes the new `isRegex` field on the binding.

## What about cross-category bindings

You can't bind "any event on any thing" — each binding declares its `On`. If a user wants to log every step AND every action, they register two bindings. This is more honest than today's enum where `BeforeStep` and `BeforeAction` were unrelated values that nonetheless lived in one matcher's switch.

## What if a category needs more than name+path

If a future event needs a third filter axis (e.g. "before write of MIME type X"), add it as a new field on the binding record. The On enum value clarifies which fields apply. We're not pre-extending now — name+path covers everything settled.

## Error category (deferred)

When Error lands, it'll be `On.Error` with:
- `name:` — error code/type pattern.
- `path:` — null.
- Source Data carries the error.
- Both Before (transform/suppress) and After (observe/log) fire.

Not implemented in this branch. Add it as a new enum value + the
corresponding fire site in the error propagation path.
