# Channels — v1 plan

## What this is

Channels are wrappers around `Stream`. Streams come from outside PLang — the console process opens them at boot, a future web entry would hand them in per request, an outbound HTTP call hands a response stream to the Service for that call's lifetime. PLang doesn't open streams; it accepts them. The current code opens `Console.OpenStandard*` inside `App/Channels/this.cs`, which is the placeholder we're replacing.

This branch is about three things, in order:

1. **Streams enter through navigation, not types** — entry points register channels onto the actors they want to wire.
2. **`Write` gets a `Channel` property via source-gen** (because it implements `IChannel`); builder is taught the list of available channels per actor so the LLM maps user intent to one of them — pattern-free.
3. **Goal-as-channel** — register a channel whose backing is a goal call instead of a stream. When written to, runtime invokes the goal.

`output.ask` is out of scope. It uses callback/suspend, not channels.

## The actor model

Two actors: **System** and **User**. Service is **not** an actor — it's its own concept living under whichever actor spawned it.

| Actor  | Who they are                                | Lifecycle           |
|--------|---------------------------------------------|---------------------|
| System | The app itself                              | One per App, created at App boot |
| User   | The caller who initiated work               | One per App in console; per-request in web |

App owns a flat `Services` collection of all in-flight outbound calls. Each Service has a `Parent` reference to the Actor that triggered it:

```
App
├── System  : Actor
├── User    : Actor
└── Services
     └── [Service { Parent=User, ... },
           Service { Parent=System, ... },
           ...]
```

The collection lives on App, not on each Actor, because identity is always System and the collection's real owner is App. `Parent` separates "who triggered this" (audit) from "who signs it" (always System). No icky split.

Namespace placement:

```
App/Actor/this.cs              — Actor (System, User)
App/Services/this.cs           — Services collection (rule 5)
App/Services/Service/this.cs   — single Service
```

A Service carries:

- `Channels` — its per-call channel set
- `Identity` — System's identity (signs all outbound I/O)
- `Parent` — the Actor that spawned it (audit / tracing)
- a lifetime — bounded by the call (or the connection, for TCP)

No `EscalationLevel`. Service doesn't run code, doesn't have privilege of its own. Privilege belongs to the parent Actor's Context, where the action that opened the Service is running.

Spawn / dispose pattern:

```csharp
await using var service = app.Services.New(parent: app.CurrentActor);
service.Channels.Register(new Channel.Stream("input", httpResponseStream, Role.Input));
// ... read response from service.Channels ...
// dispose: removes from app.Services, tears down channels
```

Two parallel outbound calls each get their own Service. No collision, no shared registry. Cleanup is structural.

### Service identity

Every I/O in PLang is signed. Service signs with **System identity**, always. Outbound calls go under the app's name regardless of which Actor triggered them — third parties know the app, not its individual users. This matches runtime1's single-system-identity model.

Per-user signing is deferred. Maybe a future version, not this branch.

### Ask-propagation routing rule

When a goal handler invoked through a Service issues `ask` without a target actor, unhandled propagation goes to System (not User). Service-handler asks are app-level concerns — admin territory, not whoever-happened-to-be-using-the-app territory.

## The shape

### Stream entry — by navigation, not by ctor

OBP rule 5 — collections are the API. `Channels` already exposes `Register`. Entry points navigate and register. No new ctor parameters, no `ActorStreams` record, no host abstraction.

```csharp
// PlangConsole.Main
await using var app = new App(absolutePath);

// In console, User and System share the same stream setup.
// Standard channel names match Roles: "output", "error", "input".
app.User.Channels.Register(new Channel.Stream("output", Console.OpenStandardOutput(), Role.Output));
app.User.Channels.Register(new Channel.Stream("error",  Console.OpenStandardError(),  Role.Error));
app.User.Channels.Register(new Channel.Stream("input",  Console.OpenStandardInput(),  Role.Input));

app.System.Channels.Register(new Channel.Stream("output", Console.OpenStandardOutput(), Role.Output));
app.System.Channels.Register(new Channel.Stream("error",  Console.OpenStandardError(),  Role.Error));
app.System.Channels.Register(new Channel.Stream("input",  Console.OpenStandardInput(),  Role.Input));

// Service: nothing at boot. Outbound modules spawn Services and register per-call.

await app.Run();
```

`App` ctor stops opening console streams. `Channels` ctor stops opening console streams. Both become structural — they create the registry, they don't populate it. Populating is the entry point's job.

A future web entry follows the same pattern, just with different streams. All three role-channels must be registered — that's an enforced invariant:

```csharp
// hypothetical web request handler
app.User.Channels.Register(new Channel.Stream("output", httpResponse.Body, Role.Output));
app.User.Channels.Register(new Channel.Stream("error",  httpResponse.Body, Role.Error));   // or a separate error sink
app.User.Channels.Register(new Channel.Stream("input",  httpRequest.Body,  Role.Input));
```

An outbound `http.request` action follows the same pattern for its response stream — spawns a Service from `app.Services` with the current actor as parent:

```csharp
// inside http.request handler
await using var service = app.Services.New(parent: app.CurrentActor);
service.Channels.Register(new Channel.Stream("input", httpResponseStream, Role.Input));
// ... read from it, then dispose closes the channel and removes from app.Services
```

### Standard role-channels are guaranteed to exist

**Invariant:** every actor that participates in I/O has `output`, `error`, and `input` channels registered. This is non-negotiable. The runtime relies on it — error reporting in particular needs `error` to always be reachable.

How it's enforced: entry points must register all three before running any goals. If they don't, `App.Run()` fails fast at startup with a clear message ("User actor missing required channel: error"). Loud, early, before any user code runs. No silent missing-channel state at runtime.

### Writing to a non-existent custom channel

When a write targets a name that isn't registered (e.g., `- write 'hi' to dbg` and `dbg` was never registered), the action raises a typed error (`ChannelNotFound`). That error propagates through PLang's normal error chain — the engine catches it, surfaces it through `/system/error.goal` (or last-resort) by writing the error message to the actor's `error` channel.

So in effect: write to a missing custom channel → error appears on the error channel. The error channel is guaranteed to exist (invariant above), so the error always has somewhere to land.

The program can also catch the error via standard error handling if it wants to recover. The runtime's job is to make the failure visible, not silently fall back to `output`.

### Drop `app.Channels` (was a shortcut, now redundant)

Today's `app.Channels` is a thin wrapper that, after this branch's changes, has no real job:

- Action handlers use the source-gen-resolved `Channel` (single resolved channel), not the registry.
- `Serializers` moves to `App.Serializers` (app-wide, not per-actor).
- Goal/Setup loaders that consult `app.Channels.Serializers` switch to `app.Serializers`.

So we delete `App.Channels` entirely. Callers that need the registry navigate to `app.System.Channels` or `app.User.Channels` directly. Smell #3 (same logical thing exposed twice) goes away.

### `Write` action

`Write` implements `IChannel`. The marker interface follows existing PLang convention (like `IContext`, `IStep`, etc.) — it declares the property being injected:

```csharp
public interface IChannel
{
    Channel.@this Channel { get; set; }   // single resolved channel, not the registry
}
```

The current `IChannel` (which declares `Channels` — the collection) is updated to this single-channel form. Source generator wires it accordingly.

`Write` becomes:

```csharp
[Action("write", Cacheable = false)]
public partial class Write : IContext, IChannel
{
    public partial Data.@this Data { get; init; }
    // Channel property is source-gen-emitted from IChannel.

    public Task<Data.@this> Run() => Channel.WriteAsync(Data);
}
```

`Channel.WriteAsync` takes the full `Data.@this` envelope (not `Data.Value`) — Rule 7, relay don't repackage. The Channel's serializer decides how to render based on Data's type, properties, and signature.

How the source generator resolves `Channel`:

1. The action's JSON carries a channel name (whatever the LLM emitted from user intent).
2. At setup, generator looks up that name in `(context.Actor ?? app.CurrentActor).Channels`.
3. If no name was emitted, falls back to the actor's `Output` role channel.
4. The resolved `Channel.@this` is set on the action's `Channel` property.

The source-gen change from today: `Channels = (context.Actor ?? app.User).Channels` → `Channel = (context.Actor ?? app.User).Channels.Resolve(action.Json["channel"])`. The lookup logic moves into Channels itself (`Resolve(name) -> Channel.@this`).

The choreography that today lives on `Channels.WriteAsync(Write action)` (reaching into `action.Data.Properties`, calling back into `action.Context.Variables.Resolve`) goes away — both because resolution moves into source-gen and because the action no longer talks to a registry, only to its single resolved Channel. The `WriteAsync(Write)` overload on `Channels` is deleted; `Channels` stops importing `App.modules.output`.

## PLang surface — channel actions

Module `channel`:

- **`channel.set`** — replace the channel registered for a role. Defaults to the current actor; may target `system` or `user` explicitly.
  ```
  - set output channel as OutputGoal              (current actor)
  - set system output channel as OutputGoal       (System)
  - set user output channel as OutputGoal         (User)
  - set input channel as InputGoal
  - set error channel as ErrorGoal
  ```

- **`channel.add`** — register a new custom-named channel.
  ```
  - add channel "logger" call Logger
  - add channel "audit" call AuditLog, buffer: 64kb, timeout: 30s
  ```

- **`channel.remove`** — undo a registration.
  ```
  - remove channel "logger"
  ```

- **`channel.migrate`** — lift a Session channel and ship it to another identity-aware runtime, where it resumes with state intact. (Cool.md territory; ships only the API surface this branch — the cross-device transport lands when there's an entry point that needs it. The action and Channel-side `Migrate()` plumbing are foundational so the future transport plug-in just hands data to it.)
  ```
  - migrate channel "chat" to %targetIdentity%
  ```

The standard role-channels (`default`, `error`, `input`) are pre-registered by the entry point. PLang programs override via `channel.set`. New custom channels go through `channel.add`.

## Channel config

Every `Channel` carries its own config. Sensible defaults; overridable via constructor / action parameters.

| Property      | C# type    | Default         | JSON shape           | Notes |
|---------------|------------|-----------------|----------------------|-------|
| `Buffer`      | `long`     | 4096            | int (bytes)          | Stream-backed honors; Goal ignores. `long` because file/stream sizes can exceed 2GB. |
| `Timeout`     | `TimeSpan` | 30s             | ISO 8601 string      | e.g. `"PT30S"` / `"PT5M"`. Custom JsonConverter reads via `XmlConvert.ToTimeSpan`. |
| `Mime` | `string`   | "text/plain"    | string (MIME)        | Drives serializer selection. |
| `Encoding`    | `string`   | "utf-8"         | string               | Text encoding name. |
| `Encryption`  | provider?  | none            | provider ref / null  | Optional crypto provider reference. |
| `Signing`     | provider?  | auto (System)   | provider ref / "auto"| Default signs with System identity. |

App-level defaults aren't shipped this branch. Per-channel config is the only knob. If a real need for app-wide overrides emerges, we add it the same way Callback added `app.Callback.Signature` — a small config holder that channels read on construction.

## Builder impact

The builder is a first-class concern, not an afterthought. Anything that changes runtime shape must be matched by builder catalog updates so the LLM emits correct `.pr` for it.

### Intent over patterns

The builder must understand user intent, not match syntactic patterns like `to <name>`. A user might write `- write 'hello' to debug` or `- write 'hello' at the best ever debugger` or write the whole step in Icelandic. The LLM should pick the right channel in all cases — provided it knows what channels exist.

Mechanism: the builder catalog passes the LLM the **list of available channels per actor** at build time. A channel is "available" if it was registered at boot (the standard role-channels) or registered earlier in the same goal via `add channel`. Cross-goal registrations aren't knowable at build; we accept that and let the runtime catch them.

Given the list, the LLM maps the step's intent to one of the channel names. No `to <name>` parsing rule. Just intent against an inventory.

### What the catalog must teach

- **`Write` has a `Channel` property** (source-gen-emitted via `IChannel`). The catalog describes it; LLM picks a channel name from the available-channels list.
- **`IChannel` is a typed marker.** The catalog includes per-actor channel lists for every `IChannel` action, so the LLM has inventory to map intent against.
- **`to <path>` (file writes) lives on different action types** (file modules). Disambiguation is by which module the LLM picked.

### Channel name validation

**Runtime-only, no build-time enum.** Channel names are open-ended:

- Standard names exist (`output`, `error`, `input`) but vary per entry point. PlangConsole picks them; PlangWindow may pick others.
- User-defined names register dynamically via `add channel <name> ...` steps. Some are knowable at build time (registered earlier in the same goal); others aren't (registered by another goal at runtime).

Builder emits whatever name the user wrote. If the channel doesn't exist at runtime, the action fails with a clear error. Cheap to learn, avoids false negatives.

### `channel.add` / `channel.set` / `channel.remove`

Regular module actions. Builder picks them like any other action by matching step text to action descriptions. `channel.add`'s goal-target property points at a goal name (open-ended like file paths and variable names — no build-time validation).

### Config properties on `channel.add`

The builder's catalog must teach the LLM the JSON shape for each optional config parameter — we never parse user input ourselves; the LLM emits the standard form, our JsonConverters read it.

- `buffer` → integer bytes (e.g. `65536` for 64KB). The LLM does the conversion from human language to bytes during build.
- `timeout` → **ISO 8601 duration string** (e.g. `"PT30S"`, `"PT5M"`, `"PT1H30M"`). C# property is `TimeSpan` with a custom JsonConverter using `XmlConvert.ToTimeSpan`. LLM never does math on time values.
- `mime` → MIME string, no validation.
- `encoding` → encoding name string (e.g. `"utf-8"`).
- `encryption` / `signing` → provider reference name or `"none"` / `"auto"`.

Builder catalog descriptions tell the LLM the standard for each. We match callback's existing convention for ints (callback's `ExpiresInMs` stays int for now — migration to ISO 8601 tracked in `Documentation/Runtime2/todos.md`).

### Existing closed enum to fix

`Actor.@this.ValidValues` today returns `["user", "service", "system"]`. Once Service is no longer an Actor, this drops to `["user", "system"]`. Builder picks one of these for actor-targeted actions (e.g. `ask user`, `ask system`).

### Channel-event syntax in `events` module

The existing `events.add` (or whatever it's named) action gains support for channel-targeted bindings. Builder catalog gets the new `EventType` values (`BeforeWrite`/`AfterWrite`/etc.) and the `channelName` filter, so the LLM can map `- add before write on "logger" channel, call X` to the right binding shape. Like other channel-name slots, the name is open-ended and runtime-validated.

### EscalationLevel — dead code, remove

`Actor.@this.EscalationLevel` exists in the code (switch on `Name`: `system => 2, service => 1, user => 1`) but `grep -rn EscalationLevel` returns only the definition itself. Nothing reads it. It's dead.

This branch removes the property entirely. When a future feature (sandboxing, privilege gating, action `level:` parameter) actually consults trust levels, add it back with inverted direction: `system => 0, user => 1, untrusted-others => 100+`. With "0 = most trusted, larger = less trusted", System stays anchored at 0 and arbitrary new actor kinds slot in at any higher number without disturbing the anchor. The current scheme (`system=2`) has no headroom above System for a more-trusted actor, which is the wrong shape if extension matters.

### Coder responsibility

Tests must verify the builder actually emits a `Channel` property for `to <name>` patterns and leaves it unset otherwise. This is coder territory once stage files are carved.

### Goal-as-channel

Register a channel backed by a goal reference:

```csharp
app.User.Channels.Register(name: "logger", goal: app.Goals.Get("Logger"));
```

When a write lands on it, runtime sees the channel is a goal channel and calls `app.Run(channel.Goal, data)` instead of writing bytes. The content becomes the goal's input.

In PLang, a step like `- add channel "logger" call Logger` is the module action that wraps this.

### Channel events

Channels become first-class event-bindable objects. `Channel.@this` exposes an `Events` property — same shape as `Goal.Events`, `Step.Events`, `Action.Events`. The Channel base class wraps `WriteAsync` / `ReadAsync` / `Ask` in before/after firing; concrete subtypes (Stream, Goal, Web) implement Core methods and never think about events.

PLang surface (added to existing `events` module):

```
- add before write on "logger" channel, call AuditGoal
- add after write on "logger" channel, call MetricsGoal
- add on ask on "input" channel, call CaptchaGoal
- add before write on "audit.external", call AskCompliance     # sudo-for-I/O in one line
```

Key contract decisions (full detail in `plan/channel-events.md`):

- Before-handlers can **abort** by throwing, but **cannot mutate** the in-flight Data — mutation belongs in Goal channels (composition), events are for cross-cutting hooks (validation, audit, metrics).
- After-handlers **always fire**, even on failure. Their own errors are suppressed (don't change the operation's result).
- Recursion guard via existing `_activeEventBindings` — a Before-handler that writes to the same channel doesn't infinite-loop.
- Bindings match by channel name across actors (User and Service channels with the same name both trigger).
- Multiple matching bindings fire in registration order; first thrower stops the rest.

The Goal-channel composition pattern and channel events solve different things: composition is for *what* the channel does to writes (transformation, fan-out); events are for *cross-cutting* hooks around writes (validate, audit, alert). Both useful, neither subsumes the other.

See [plan/channel-events.md](plan/channel-events.md) for: full EventType list, EventBinding filter changes, EventContext payload, firing semantics for OnAsk on Session vs. Message, Service-channel matching rules, the Stage 8 deliverables breakdown, and the test surface.

### Fan-out via goal composition

Fan-out (one write going to multiple destinations) doesn't need a new channel type. Goal channels + the recursion rule give it for free via composition.

Example: write everything to `output` to both a file and stdout.

```
- set output channel as Logger

Logger:
- write %!data% to file.txt     # file write
- write out %!data%             # channel write — recursion rule sends to original stdout
```

Every write to `output` fires Logger. Logger writes to the file, then writes to `output` again — but the recursion rule resolves that to the *original* stdout (entry-point-registered), not back into Logger. No infinite loop. Fan-out done.

Multi-destination, transformation, conditional routing, content-based filtering — all expressible as goals. The fan-out test is the same as the recursion-rule test (writes inside a goal channel target the original streams, not the overlay).

Stacked overrides do **not** chain implicitly. If `output` is set to `GoalA`, then later set to `GoalB`, `GoalB`'s `- write out` goes to the *original* stdout, not to `GoalA`. To compose, do it explicitly in the goal:

```
GoalB:
- write %!data% to audit.log
- call GoalA data=%!data%       # explicit composition
- write out %!data%             # fundamental
```

Explicit beats magic chaining.

### Recursion rule for goal channels

A goal channel's body might contain `- write out %!data%`. If the channel that fired is the *current* `output`, that write would loop back into itself — infinite recursion.

**Rule:** when a goal channel runs, its writes resolve against the **original** entry-point-registered streams, never the current overlay. So if PlangConsole registered `output → stdout` at boot, a goal channel's `- write out %!data%` always writes to stdout, even if the user later rebound `output` to a goal.

Implementation: Channel.Goal stores a reference to the entry point's original Channels at registration time (or the runtime executes the goal under a Context whose Channels resolves to the boot-time set). Either way, the goal's writes bypass any user-rebound overlay and go to the fundamental streams.

This applies to *all* writes inside the goal channel's execution, not just `output` — any name resolves against the fundamental set.

## Stage index

| # | Stage                              | Status  |
|---|-----------------------------------|---------|
| 1 | Channel base + Session/Message abstracts + Role + Config properties | pending |
| 2 | Stream channel (refactor today's Channel into Channel.Stream) | pending |
| 3 | Goal channel + recursion rule (original streams) | pending |
| 4 | Channel slot on Write + builder `to <name>` | pending |
| 5 | `channel.set`, `channel.add`, `channel.remove` actions | pending |
| 6 | Entry-point wiring (PlangConsole registers via navigation) | pending |
| 7 | Flat `App.Services` collection; `Service` type with Parent + System-signed identity | pending |
| 8 | Channel events: `Channel.Events`, new EventTypes (`BeforeWrite`/`AfterWrite`/`BeforeRead`/`AfterRead`/`OnAsk`), `channelName` filter on `EventBinding`, firing wrapper in Channel base | pending |
| 9 | `channel.migrate` action (API surface only — cross-device transport deferred) | pending |

(Web channel + `webserver.start` deferred to a follow-up branch.)

(Stage files not yet carved — waiting on plan review first.)

## What this branch does NOT do

- **App pooling / `App.Rent`** — separate concern, future.
- **Web channel + `webserver.start`** — deferred to a follow-up branch. Channel.Message base ships; concrete Web subtype waits.
- **TCP / UDP / WebSocket entry points** — out of scope.
- **App-level channel config holder** — not needed yet; each channel carries its own config. Add later if a real cross-channel override use-case emerges.
- **Reads at PLang surface** — `ask` is the read surface. `- read from input, write to %answer%` would map to `ask`. C# `ReadTextAsync` etc. already exist on Channel for direct use.
- **Renaming** — `App.Channels`, `Channels`, `Channel` are correct OBP names. The global alias `AppChannels = App.Channels.@this` stays as a using statement.

## Open threads

### A — host class in PLang core (RESOLVED)

No shared host type. Each entry project (PlangConsole, future PlangWindow / PlangMobile / PlangTv / PlangWatch) is a thin program against `App`'s public ctor: construct App, register channels for System and User, run a goal. The only thing that varies across them is *which streams* — and that variation can't be abstracted because it IS the variation. A base would just enforce "calls these three methods," which the C# compiler already enforces via `App`'s API.

If shared boot patterns emerge later (error fallback, signal handling, settings resolution), extract from three real implementations rather than designing upfront with one.

Web is built into core (`webserver.start` action), not a separate entry project.

### B — Service actor lifecycle (RESOLVED)

Service is not an actor — it's a per-call I/O scope. Lives in a flat `App.Services` collection. Each Service has a `Parent` reference to the Actor that triggered it. Signs with System identity. See "The actor model" above.

### C — Context inheritance for spawned Services (RESOLVED)

Service has no Context of its own. It's an outbound I/O scope — Channels + System identity + Parent reference + a per-call lifetime.

For transient outbound (HTTP request/response), the action that issued the call runs in the parent actor's Context and uses Service's Channels for I/O. Service disposes when the round-trip completes.

For persistent outbound (TCP, WebSocket), the Service lives until the connection closes. Event handlers (`on message`, etc.) fire goal calls when messages arrive. Each goal call has its own Context (engine's existing per-goal-call pattern). Service does not carry Variables between events.

**Routing rule:** when a goal handler invoked by a Service issues `ask` without a target actor, unhandled propagation goes to System, not User. Service-handler asks are app-level concerns (admin), not user concerns.

**Remote execution does NOT need a special actor.** Sandbox semantics belong on the action that runs externally-supplied code (e.g. `- actions.run %actions%, level: 0`). The action owns its trust contract; the actor model stays uncluttered.

### D — Where `Service` lives in the type tree (RESOLVED)

Service is not an Actor — it's its own type living in a flat `App.Services` collection. Placement is `App/Services/Service/this.cs`. Not part of the Actor type hierarchy.

### E — `Resolve(name)` lookup with many Services (RESOLVED)

`Actor.@this.Resolve("service", context)` is dropped — Service was never an Actor to resolve. Reach a Service only through navigation (`app.Services[i]`). Existing `Resolve` keeps working for System and User.

## Next step

Read this and tell me what's still off before I carve the four stage files.
