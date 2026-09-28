# Parked brief: a service is an actor (after app-systems)

Ingi, 2026-09-28:
- "we should be Service in back as main actor, if there is no service, then it is null, if one user starts many services, it is user.service.list";
- "when a service is executing, the variable coming from wire, such as llm running a function, it runs as service actor, having its own context and memory";
- "two shapes, app.actor.service and app.actor.user.service.list, list returns list of app.actor.service";
- "(b) start empty. I think we should send the %var% of the goal to the service, or maybe that should be defined specifically, yes".

## Why

Code that the wire drives runs today as the actor that opened the door. The live case is an LLM tool call (`llm/code/OpenAi.cs`):
- The tool goal runs in the caller's context: `tool.Held.Start(context)` (`:567-568`) behind an argument frame (`Calls.Push`). A variable read checks that frame and then "falls back to the actor-shared dictionary" (`type/item/variable/call/list/this.cs:7`). So a goal the model picks reads all the user's memory, with the user's permission.
- `ParseToolArguments` (`:603-637`) binds **every** name the model sends, not just the tool's `Declared` rows (`:1058`) that the schema showed it. The frame is read first, so the model can shadow a caller variable (`%userId%`), and a valued row "yields to a supplied name" (`:555-557`).
- `OnToolCall` sets `name`, `arguments`, `status` and `result` in the caller's memory (`:541-543`, `:589-592`), overwriting a user's own `%name%`.
- Parallel tool calls (`:358-360`) share one context.

An actor is who acts, and that decides what's allowed. The wire's code must not act as the user.

## Settled

- **A service is an actor**, a subtype of `actor`. The three paths agree:

| plang | C# | file |
|---|---|---|
| `%!app.actor.service%` | type `app.actor.service.@this : actor.@this` | `app/actor/service/this.cs` |
| `%!app.actor.service.current%` | the service the running code is inside; null in a script | answered by the actor itself (a service answers itself, others null), never an `is` check |
| `%!app.actor.user.service%` | the actor's `service` node, `app.actor.service.list.@this` | `app/actor/service/list/this.cs` |
| `%!app.actor.user.service.list%` | `list<app.actor.service>` | |

- **The actor that opened the door owns the service** (as it owns its `channel`). A service is an actor, so it has its own `service` list: a web request that runs an `llm.query` owns that query's tool-call services. Stopping the owner stops its services (linked cancellation).
- **It has its own context and memory.** The caller's memory is out of reach.
- **Permission starts empty (b).** The door grants what it allows. The goal's code is trusted source; what came over the wire isn't.
- **What the service's memory holds is defined specifically** (Ingi's lean: "defined specifically, yes").

- **The door's call is the grant** (proposed by the architect; Ingi ruled the override). A tool is already a held `goal.call` with rows:
  - a **declaration row** (`orderId`) is supplied by the wire (wire origin, see the `.data` brief);
  - a **valued row** (`limit=10`, `userId=%userId%`) is sent by the caller, and **the model may override it** (Ingi: "it should be able to overwrite them, this gives programming ability");
  - the service's memory is exactly those rows. A name the model sends that isn't one of them is refused back to the model, not bound.
  
  So a row is input the model may set. A value the model must not change doesn't ride as a row; the goal reads it from what the service can't be told (its identity, its settings, `%!…%`).
- The same holds for a web route when servers come: the route declares what the request may supply.

## Proposed (architect), waiting on Ingi
- **One service per run the wire drives:** one tool call, one HTTP request, one socket connection. Parallel tool calls then can't race, and OpenAI tools are stateless (the state is the conversation). The cost is met by making the actor lighter (lazy channels, settings falling back to the parent), not by sharing.
- **The identity:** a service's context carries the caller's identity (a signed request's signer; for an LLM, the provider). The `.data` brief's `/.data/identity/<id>/` is where its files go.

## Open

- How a door writes permission grants (paths, hosts) in the step: taught syntax plus the builder.
- The name of the actor member that answers `current` (`service` is taken by the list).
- `actor/this.cs:12` is `sealed`; `actor.Name` (`{system, user}`) stays the closed set for `choice<actor>` slots, since a service isn't named by a slot.

## On app-systems

- Delete `app/service/` (the per-call scope: no production callers; one test, `Stage8_ChannelEventsTests.cs:205`). This reverses decision 129's "services stay", which was my extension, not Ingi's.
- The two live faults, in the llm module's 9b turn: bind only `Declared` names, and bind OnToolCall's run-state in a call frame rather than setting it in the caller's memory. Valued rows keep yielding to the model (Ingi, decision 187).
