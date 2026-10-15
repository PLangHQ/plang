# Services — per-call I/O scopes

A service is the I/O scope of one outbound call: the channels that call writes
to, the identity it runs under, and a reference to the actor that triggered it.
This page is for the C# developer changing how an outbound call is scoped. It
says what a service holds, how one is created and disposed, and how it differs
from an actor.

```
app.service.@this         PLang/app/service/this.cs        namespace app.service
app.service.list.@this    PLang/app/service/list/this.cs   namespace app.service.list
app.@this.Services        PLang/app/this.cs                (PascalCase C# property)
```

> **Scope.** This is the C# page. A service is not a plang-navigable node: its
> collection is a PascalCase property on `app` (see §5), so no plang path reaches
> it. Nothing in the `os/system/` teaching markdown describes services.

---

## 1. What a service is

A service is a `sealed` class that implements `IAsyncDisposable`. It is a plain
C# class. It does not derive from `app.type.item.@this`, and it does not derive
from `app.actor.@this`.

It holds three things:

| Member | Type | Meaning |
|---|---|---|
| `Id` | `Guid` | the stable key the collection indexes it by |
| `Parent` | `app.actor.@this` | the actor that spawned the call (audit and tracing) |
| `Channels` | `app.channel.list.@this` | this call's channel set, created empty |
| `Identity` | `Identity?` | computed: `Parent.App.actor.list.System.Identity` |

`Identity` is always the System actor's identity. An outbound call goes out
under the app's name, whichever actor triggered it. `Parent` is kept only so the
call can be traced back to who made it.

The constructor is `internal`. A service is made only through the collection's
`New`, and it creates its own empty channel set with `actor: null`. Its channels
belong to the call, not to an actor.

---

## 2. Creating and disposing

The collection hands out a service and keeps a reference to it until it is
disposed:

```csharp
public global::app.service.@this New(global::app.actor.@this parent)
```

The caller owns the lifetime and disposes the service when the call completes:

```csharp
await using var service = app.Services.New(parent: actor);
service.Channels.Register(channel);
```

`DisposeAsync` does two things: it removes the service from the collection, and
it disposes `Channels`. Removal is keyed by `Id` through `ConcurrentDictionary`,
so a `New` racing a `Remove` does not drop either entry.

> **Lifetime.** The class doc comment says a service is bounded by one HTTP
> request or one connection. That is the intent. The code does not enforce it:
> the caller's `await using` is what ends the scope. No production caller was
> found in `PLang/` (§6).

---

## 3. The collection

`app.service.list.@this` is the flat, per-call collection. It is `sealed`, and it
implements `IEnumerable<app.service.@this>`.

| Member | Visibility | Meaning |
|---|---|---|
| `New(parent)` | `public` | create a service, add it, return it |
| `Remove(service)` | `internal` | called by the service's `DisposeAsync` |
| `Count` | `public` | number of live services |
| `GetEnumerator()` | `public` | enumerate the live services |

It is flat. A service is not nested under an actor, and the collection is not
stored on the actor. Every service of every actor sits in one set on `app`. The
`Parent` reference is what links a service back to its actor.

`app.Services` is created lazily on first access (`??=` in
`PLang/app/this.cs`). The field is private, and the property is PascalCase.

---

## 4. A service is not an actor

The two types answer different questions, and the code keeps them apart:

| | `actor.@this` | `service.@this` |
|---|---|---|
| Base | `app.type.item.@this` | none (plain class) |
| Identity | its own (System, User) | always System's |
| Channels | its own set, on the actor | its own set, on the service |
| Collection | `app.actor.list` (`System`, `User`) | `app.Services` (flat) |
| Plang-navigable | yes (`app.actor.list.User`) | no |

`app.actor.Name` (`PLang/app/actor/Name.cs`) enumerates the actors that exist
as a set: `system` and `user`. Its doc comment says that a service is not an
actor and is not in that set. Services are reached through `app.Services`.

> **Stale comments.** `PLang/app/actor/this.cs` still lists `"Service"` as an
> actor name in the `Name` doc (line 33), and says cancellation cascades to
> "User and Service" (line 71). The code has no `Service` actor. Those two
> comments predate the split and describe the old model.

---

## 5. How `Services` is reached

`Services` is a C# property on `app`, not a plang member. In `PLang/app/this.cs`:

```csharp
private global::app.service.list.@this? _services;
public global::app.service.list.@this Services => _services ??= new global::app.service.list.@this(this);
```

The naming rule from `PLang/app/start.md` applies: a lowercase member is what
plang navigates, and a PascalCase member is C# plumbing. `Services` is
PascalCase, so it is C# plumbing. A plang path does not reach it.

---

## 6. Callers

- **Tests only.** `PLang.Tests/Wire/App/ChannelsTests/Stage7_AppServicesTests.cs`
  exercises `New`, `Count`, dispose, and concurrent `New` and `Remove`.
  `Stage8_ChannelEventsTests.cs` uses a service's `Channels`.
- **No production caller was found.** A search of `PLang/`, `PlangConsole/`,
  and `PLang.Tests/` for `.Services` and `service.list` found the definitions,
  the tests, and nothing else. Which outbound call creates a service is not
  wired in production code. Treat that as unverified, and check before you
  rely on a service being present at run time.

---

## 7. Find ...

| You want | Source |
|---|---|
| the service class: `Id`, `Parent`, `Channels`, `Identity`, `DisposeAsync` | `PLang/app/service/this.cs` |
| the collection: `New`, `Remove`, `Count`, enumeration | `PLang/app/service/list/this.cs` |
| the `Services` property on `app` | `PLang/app/this.cs` (the line declaring `Services`) |
| the actor set (`system`, `user`) and the "not an actor" note | `PLang/app/actor/Name.cs` |
| the channel set a service owns | `PLang/app/channel/list/this.cs` |
| the tests that pin the collection behaviour | `PLang.Tests/Wire/App/ChannelsTests/Stage7_AppServicesTests.cs` |

---

## The tree from here

| Doc | Covers |
|---|---|
| `../start.md` | the app tree: the root, the `@this` pattern, the collection-node rule |
| `../actor/start.md` | the actor, the identity a service borrows from System |
| `../channel/start.md` | the channel set a service owns, and the write, read and ask surface |
