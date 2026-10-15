# The parent app, a C# developer's guide

The parent is the app that started this plang, if one did. A plang app can
call a goal in its parent, and the goal runs there. The call and its arguments
go up over a link, the parent runs the goal, and its result comes back as the
call's result. This page covers the one class that does this, for the developer
working on Runtime2 in C#, and answers three questions: **what is this, where
is the source, and how does the plang I write reach this C#?**

```
app.parent.@this        PLang/app/parent/this.cs   namespace app.parent
app.parent.goal         PLang/app/parent/this.cs   private nested class goals
app.parent.Link         PLang/app/parent/this.cs   internal, set by the link
```

> **Scope.** `app.parent` is a plain property on the app root
> (`PLang/app/this.cs`), created once and never replaced. The root's shape is
> described in `PLang/app/start.md`. This page covers only the parent class.

---

## 1. What it is

`app.parent` is a `type.item`, so it navigates like any other value. It holds
no state of its own except the link and the waiting calls. With no link set, it
is an empty node: `%!app.parent.goal["Claude"]%` still resolves to a goal, and
calling that goal answers with an error (`NoParent`, 404, see §3).

Two things set the link. Both are in the modules, not in `app/parent/`:

| Set by | Source | Effect |
|---|---|---|
| the `screen` display, when it has an output pipe | `PLang/app/module/screen/type/screen/display/this.cs` (line 73) | `context.App.parent.Link` writes a call up the pipe. The host's answer comes back down its input and is passed to `Answered`. |
| the `terminal` process, for a plang this app started | `PLang/app/module/terminal/type/process/this.cs` (lines 202 and 318) | The process holds its own `parent` instance, `Remote`. Its `goal` writes the call to the process's input. |

The link is a `Func<string, Task>?`. The class does not know what it writes to.
It only builds the call and waits for the reply.

---

## 2. Members

| Member | Kind | Use |
|---|---|---|
| `Get(parent, key)` | `override`, `public` | One dot step. `goal` returns the `goals` node. Every other key goes to `base.Get`. |
| `goals` | private nested class | The parent's goals, picked by name. `Get(data, key)` returns a `goal.@this` with `Name = key` and `Elsewhere = parent`. |
| `Link` | `internal Func<string, Task>?` | How a message reaches the parent. `null` means nothing links them. |
| `Run(goal, context)` | `internal async Task<data.@this>` | Sends the call, waits for the reply, and returns it as `Data`. |
| `Answered(reply)` | `internal void` | Takes a `reply` element from the link and completes the waiting call with the same `id`. |

`goal.@this.Elsewhere` (`PLang/app/goal/this.cs`, line 74) is the only field
that points at a parent. It is `null` for every goal of this app. Those goals
run their own steps.

---

## 3. How a call runs

A goal with `Elsewhere` set skips its own steps. From `goal/this.cs`
(line 400):

```csharp
if (Elsewhere is { } app) return await app.Run(Name, context);
```

`Run` then does this, in order:

1. **No link, no call.** If `Link` is `null`, it returns
   `Error` `NoParent` (404), naming the goal.
2. **Arguments.** It walks the call's own frame (`context.call.Scope`) and
   writes each argument with the `application/json` encoder. Only the call's
   arguments go up, not the caller's variables. If one argument cannot be
   written, the call answers with that failure. It does not send half of the
   arguments.
3. **Send.** It registers a `TaskCompletionSource` under a new 12-character id,
   then calls `Link` with the call.
4. **Wait.** It waits up to ten minutes. A timeout answers
   `ParentGoalTimeout` (504). A cancelled context is not answered: the
   cancellation is thrown up the call.
5. **Answer.** An `error` in the reply answers `ParentGoalFailed` (500), with
   the parent's message. A `result` is parsed by the JSON serializer
   (`app.type.item.serializer.json`) and returned as `Ok`. No result returns
   `Ok()`.

The wire is one JSON object each way:

```json
{"call":  {"id": "3f9c1e0a7b2d", "goal": "Claude", "parameters": {"message": "hi"}}}
{"reply": {"id": "3f9c1e0a7b2d", "result": "..."}}
```

> **Rule.** `Answered` only completes a call whose `id` is waiting. A reply
> for an unknown or finished id is dropped. The `finally` in `Run` removes the
> id on every path, so a late reply cannot complete a call that already
> answered.

---

## 4. Find ...

| You want | Look at | Where |
|---|---|---|
| the parent node on the app | `app.parent` | `PLang/app/this.cs` (line 227) |
| the goal a plang name picks | `goals.Get` | `PLang/app/parent/this.cs` |
| the call that goes up | `Run` | `PLang/app/parent/this.cs` |
| the place a reply comes back | `Answered` | `PLang/app/parent/this.cs` |
| the link the screen sets | `context.App.parent.Link` | `PLang/app/module/screen/type/screen/display/this.cs` |
| the link a started process sets | `Remote` | `PLang/app/module/terminal/type/process/this.cs` |
| the goal that runs in the parent | `goal.@this.Elsewhere`, `Enter` | `PLang/app/goal/this.cs` (lines 72-74, 398-400) |

---

## The tree from here

| Doc | Covers |
|---|---|
| `../start.md` | the app root, where `parent` is a property |
| `../goal/start.md` | the goal, and the `Elsewhere` path in `Enter` |
