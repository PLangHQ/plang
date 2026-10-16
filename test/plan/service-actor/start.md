# service-actor

Traces to: Ingi's brainstorm on 2026-10-16, which started from llm-writer stage 3 (how `%photo%` reaches an llm). It is logged in `.bot/kind-is-type/architect/summary.md` as decision 843 and on the backlog. The chain is idea → conversation → summary → this plan → code. The plan doesn't fix where this branch comes in the queue after kind-is-type, nor its order against branch A, returns and snapshot.

## Why

An outbound call has no party at its far end. Whatever comes back runs as whoever made the call:

- An llm's tool call runs its goal in the step's own context (`module/llm/code/OpenAi.cs:520–540`), under the user's grants. Text planted in a message can make the model call a tool that reads whatever the user can read.
- A service can't ask the app a question as itself, and its errors and warnings arrive as the caller's own.
- What we send out is signed by the actor that sends it.

Ingi: "I am getting more convinced about Service actor doing i/o, this will allow services to ask us questions and we can run it safely", and "ask us, error, warning from service can all go through this".

Making the far end a party means:

- What it sends back (a reply, a tool call, a question, an error, a warning) arrives as that party, under its own grants.
- What it asks for that it hasn't been granted goes to the actor that started the call.
- A remote party gets no grant from the user by default.

## What the developer gets

Nothing new to write. The same steps run more safely:

```plang
- ask llm "summarise %report%", tools: ReadFile
    → the model's ReadFile runs as the service: its first read outside the service's grants
      asks the user; refused, the model gets the error and the user's files stay closed
- get https://shop.is/api/order/1, write to %order%
    → the reply is the service's; a signed plang reply names who said it
- (a service asks a question mid-call)
    → it reaches the user the way any ask does (on.ask, the input channel); unanswered, the call gets the refusal
```

## Ingi's rulings so far (2026-10-16)

1. **The far end of an outbound call is a party, the Service actor.** Its replies, questions, errors and warnings come through it.
2. **The switch is at the I/O layer, not when [Code] runs** ("It should not happen when [Code] runs, it should only happen on I/O layer").
3. **The Service gets none of the starting actor's variables** ("maybe it should be defined in code what he gets, so maybe nothing … yeah probably nothing"). What it needs, the code hands it.
4. **One service identity, the same for every service** ("probably always the same"). The app then holds three identities: system, user and service. User and service stay apart in the structure ("I do like in a theoratical way that user and service identity is seperated").

## Open, to settle before stage 1

- **One identity, or one per domain.** One identity lets every service the app talks to recognise the same key and link the calls (Google, OpenAI and a shop alike). Passkeys are per site for that reason. Per domain costs a key per domain.
- **File I/O.** Ingi: "what about file i/o then?? … in future all files will be saved with signature, then it would belong to the service.identity, which is strange maybe". My reading, not ruled:
  - A Service is for I/O that has a far end. A file on the app's own disk has none, so it stays with the actor whose step it is, and that actor signs it.
  - The I/O's own type says which applies: the path family is already split into file and http paths.
  - A service's reply saved to a file is signed by the writer, and its content keeps where it came from.
- **Which I/O has a far end.** Stage 0 lists every site. The likely set: the http module, an http path read, the llm call, a terminal subprocess (it can prompt, fail and warn), and a browser page (`browser.start OnMessage=…`).
- **Out of this plan:** `variable.serializer`, the serializer a `%var%` writes through at a stage. Ingi: "not sold on variable.serializer yet".

## What is there today

- **Service exists but isn't an actor.** `app/service/this.cs` is a "per-outbound-call I/O scope. Not an Actor". It holds per-call `Channels`, `Parent` (`:22`) and `Identity => …System.Identity` (`:31`).
- **This is a deliberate runtime2 choice, and the plan reverses it.** `service/list/this.cs:6–11` says it "Replaces the runtime1 Service-as-actor model". It was made in runtime2-channels stage 7 (a5113c069). The reason given in `.bot/runtime2-channels/architect/stage-7-services.md` was: "Service doesn't run code, it just owns I/O scope and identity. The action that opened the Service runs in the parent's privilege." That was an architect design, not a ruling of Ingi's that I can find. This plan changes the one thing it rested on: what the far end causes does run, so it needs its own privilege.
- **Nothing opens a Service.** `app.Services` (`app/this.cs:247`) has no caller.
- **The actors are System and User.** See `actor/list/this.cs:24–30`, where `this[Name] => name == Name.system ? System : User`. An actor holds its context, permission, channels, tasks, identity and setting (`actor/this.cs:35–90`).
- **A Service's channel events fire as System.** A Service-owned channel list has no actor, so its events fire in System's context (`channel/this.cs:113–114`). A channel's `Signing` is "auto": System's identity at write time (`channel/this.cs:63`).
- **Outbound I/O today:**
  - **http** calls `HttpClient` directly under the action's context (`module/http/code/Default.cs:343`). An application/plang reply must be signed unless the request is `Unsigned` (`module/http/request.cs:44`).
  - **The llm** goes through `http.request`, and its tool calls run in the step's context (`OpenAi.cs:520–540`).
  - **The terminal** starts a sandboxed process (`module/terminal/code/Default.cs:48`).
- **Docs:** `Documentation/v0.2/execution-flow.md:66–73` still draws a "Service actor (escalation=1)". This branch makes a Service actor true again, so docs rewrite that section to say what it is.

## Stages

Each stage ends with its tests built and run. Each test must go red without its change. Each stage's shape comes to the architect before it's built.

### 0. Trace: every outbound I/O and what it runs as

For each site with a far end (http, http paths, llm, terminal, browser, any channel with a remote end), the trace records:

- whose context it runs in, and whose identity signs what goes out;
- where its reply, its errors, its warnings and anything it triggers (tool calls, callbacks, `OnMessage`) land;
- its disposition: through a Service, stays with the actor (no far end), or not this branch.

It also lists everything that reads `Service`, `app.Services` or the actor list's two-actor shape.

### 1. Service is an actor

```
- a call opens a Service           → an actor of its own: its own context, no variables of the user's,
                                      no standing grants, its own channels, the service identity, Parent = the caller
- the call ends                    → the Service and its channels end with it
- inside a tool call               → the running actor is the service, not the user
```

- **How plang names the running actor is NEW.** I found no `%!actor%` door today. The test reads it through whatever stage 1 settles.

- **One list, not two.** The Service lives where actors live, so `app.Services` and the actor list don't both hold it (stored twice).
- **Locked core.** The actor model and `app`'s members both change. This plan is the proposal, and Ingi's yes approves it. The surface golden is re-pinned under 843.

### 2. The switch at the I/O layer

```
- http.request / get https://… / ask llm …   → the far end is a Service, opened where the I/O happens;
                                               the module's [Code] doesn't open it or know about it
- read file.txt                               → no far end: the step's own actor, as today
```

- **One door.** A Service is opened in one place every far-end I/O passes through, not once per module.
- **Who decides.** Whether there is a far end is a fact of the I/O's own type (the path, the channel), not a list of rules.

### 3. What comes back runs as the Service

```
- the model calls ReadFile           → ReadFile runs as the Service; a read outside its grants asks the Parent
- the user refuses                   → the tool call answers the refusal to the model; nothing is read
- the user grants once               → the next read in the same call goes through
- a callback the far end triggers    → (OnStream, OnMessage) runs as the Service
```

### 4. A service asks, fails and warns as itself

```
- a service asks a question          → on.ask, the Parent's input channel; the answer goes back to the call
- a service fails                    → an error naming the service, its own key; not the caller's error
- a service warns                    → a warning on the Service's channel, reaching the Parent the way the actor's own warnings do
```

### 5. The service identity

```
- the first outbound call            → the service identity is made and kept, like the user's and the system's
- what goes out                      → signed as the service, never as the user
- a signed reply                     → checked; its signer rides with the value as its origin
```

This stage settles the open identity question first: one identity or one per domain.

Two variables exist today (`actor/this.cs:103–107`):
- `%MyIdentity%` is the app's own identity.
- `%Identity%` is "who this actor acts for … (a caller's, in a service)".

Stage 5 says what each one is inside a Service.

## Demolition

**Dies in stage 1:**

- [ ] `service/this.cs:6–13`, the "Not an Actor" summary. It describes what is no longer true.
- [ ] `service/this.cs:31`, `Identity => …System.Identity`. The Service holds the service identity.
- [ ] `channel/this.cs:113–114`, the System-context fallback for a Service-owned channel list. The list has an actor now.
- [ ] `service/list/this.cs:6–11`, the "Replaces the runtime1 Service-as-actor model" summary.
- [ ] Whichever of `app.Services` and the actor list stops holding Services (the stage 1 shape decides which one goes).

**Dies in stage 3:**

- [ ] The tool call running in the step's own context (`OpenAi.cs:520–540`). It runs in the Service's context.

**Stays:**

- per-call lifetime and `await using` disposal;
- `Parent`;
- the sandbox in the terminal module;
- `Unsigned` on http;
- the System and User actors as they are.

EscalationLevel stays gone. Grants are per actor; there are no levels.

The verdicts above are the contract; the order and mechanics inside each stage are the coder's. If a verdict looks wrong against real callers, flag it back, and never quietly keep the member.

## What tests can't show (checks the coder runs and reports)

- **Security reviews the injection path:** text planted in an llm message can't reach a file the user didn't grant to the Service.
- **The grant prompt reads well:** it names the service and what it wants.
- **The locked surface** (the actor model, `app`'s members) is re-pinned under 843 with Ingi's yes.
- **Every stage:** the gate by name; PLNG001/002 at 0; the plang suite no worse than runtime2's.

## OBP validation

| new surface | check | result |
|---|---|---|
| Service as an actor | one collection holds it (not `app.Services` and the actor list both); registry = selection + lifecycle | to verify at stage 1 |
| `Parent` | a reference, navigated; no copies of the caller's facts on the Service (flat copy) | kept as today |
| the I/O switch | one door every far-end I/O passes through; the far end is a fact of the path or channel type, not a type-switch in the switch | to verify at stage 2 |
| the service identity | navigated from where identities live, not stored twice | to verify at stage 5 |
| asks, errors, warnings | the existing doors (`on.ask`, error, the channel), no second route for services | to verify at stage 4 |
| the three paths | the running actor as plang names it (NEW, stage 1) = C# `context.Actor` = the actor's folder | to verify at stage 1 |

The coder owns the code shape; the sketches here are intent.
