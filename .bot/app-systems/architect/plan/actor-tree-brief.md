# The actor tree: `app.actor.service` — brief (Ingi, 2026-10-01)

## Why

External plang code (a service that sends plang to be built and run) must run as its own actor, so it only ever sees its own variables (Ingi, on the load-vars question: "if external service sends plang code, that is then build by plang and then executed by that service, that will all happen in the actor.service.context, so he will only see his variables"). Today a service isn't an actor: `PLang/app/actor/Name.cs` says "Service is not an actor since the per-call service scopes (`app.Services`)", and the actors are a closed pair, `system | user` (`actor/list/this.cs:24–30`).

## Ingi's design

"we need to graduate Service actor to app.actor.service, each can have a parent which is then the user that created it, just like user.parent would be the system. then, actor.user.service.list can then find all the child services"

- Actors form a tree: **system → user → service**. Singular, as every concept is named (Ingi: "is not services. it is service. singular"); a user's services are its `service.list`.
- Every actor has a **`parent`**: `%!actor.user.parent%` is the system; a service's parent is the user that created it.
- A user's child service actors: **`%!actor.user.service.list%`**.
- A service actor has its own context, so its variables, memory and settings are its own; code it brings runs there.

## Open, for the design talk

1. `app.actor.service`: the node of every service, or the service kind of actor (and so what `%!app.actor.service%` reads: a list, or the current service).
2. Naming a service: by an id or name (`%!actor.user.service["mail"]%`); actors stop being a closed `choice` (`Name.cs`), since services come and go.
3. Permissions: a service's grants lie within its parent's, and what a parent can see of its child's.
4. Lifetime: when a service actor is born (a service registered, a call received) and when it ends; what replaces `app.Services` (the per-call service scopes).
5. Identity and signing: a service actor's key, and how its signed Data is told apart from its parent's.
6. How it meets the container-as-an-app work (the host as an actor inside the container: the same tree?).
