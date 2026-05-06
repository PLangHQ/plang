# Stage 7: Flat `App.Services` + `Service` type

**Goal:** Introduce per-call I/O scope for outbound calls. `Service` is not an Actor — it's a separate type living in a flat `App.Services` collection. Each Service carries its own Channels, System identity, parent reference, and lifetime bounded by the call.

**Scope:**
- New `App/Services/this.cs` — flat collection on App.
- New `App/Services/Service/this.cs` — single Service type.
- Drop `service` from `Actor.@this.ValidValues` (becomes `["user", "system"]`).
- Remove `EscalationLevel` from Actor.@this (dead code per OBP audit).
- **Excluded:** retrofitting outbound modules (`http.request` etc.) to use Services — separate follow-up branch. This stage ships the type and API; consumers wire later.

**Deliverables:**

1. **`App/Services/this.cs`** — flat collection on App.
   ```csharp
   public sealed class @this : IAsyncDisposable
   {
       private readonly ConcurrentDictionary<Guid, Service.@this> _services = new();
       private readonly App.@this _app;
       
       public @this(App.@this app) => _app = app;
       
       public Service.@this New(Actor.@this parent)
       {
           var svc = new Service.@this(_app, parent);
           _services[svc.Id] = svc;
           return svc;
       }
       
       public bool Remove(Guid id) => _services.TryRemove(id, out _);
       public int Count => _services.Count;
       public IEnumerable<Service.@this> All => _services.Values;
       
       public async ValueTask DisposeAsync()
       {
           foreach (var svc in _services.Values) await svc.DisposeAsync();
           _services.Clear();
       }
   }
   ```

2. **`App/Services/Service/this.cs`** — single Service.
   ```csharp
   public sealed class @this : IAsyncDisposable
   {
       public Guid Id { get; } = Guid.NewGuid();
       public App.@this App { get; }
       public Actor.@this Parent { get; }
       public App.Channels.@this Channels { get; }
       public Identity Identity => App.System.Identity;   // always System; navigation, not stored
       
       internal @this(App.@this app, Actor.@this parent)
       {
           App = app;
           Parent = parent;
           Channels = new App.Channels.@this();   // empty registry; caller registers per-call streams
       }
       
       public async ValueTask DisposeAsync()
       {
           await Channels.DisposeAsync();
           App.Services.Remove(Id);   // self-deregister
       }
   }
   ```

3. **`App.@this`** gets a `Services` property — `public Services.@this Services { get; }` initialised in App ctor.

4. **`Actor.@this.ValidValues`** drops to `["user", "system"]`.

5. **Remove `Actor.@this.EscalationLevel`** — dead code per audit. (Confirmed nothing reads it.)

**Dependencies:** Stages 1, 2 (Channel base + Stream channel — Services use the same Channels collection type and register Stream channels per-call).

## Design

### The `await using` pattern

Outbound module handlers (future) use this shape:

```csharp
await using var service = app.Services.New(parent: app.CurrentActor);
service.Channels.Register(new Channel.Stream("input", httpResponseStream, Role.Input));
// ... read response from service.Channels ...
// dispose: removes from app.Services, tears down channels
```

Each call mints its own Service. Two parallel `http.request`s each get their own — no collision, no shared registry.

### Why Service has no EscalationLevel

Privilege belongs to the parent Actor's Context — Service doesn't run code, it just owns I/O scope and identity. The action that opened the Service runs in the parent's privilege.

(Future feature — remote code execution with `actions.run %code% level: 0` — would consult Context.Actor's level, not Service's. So no EscalationLevel is needed on Service.)

### Identity: navigation, not stored

`Service.Identity` is a property that returns `App.System.Identity` via navigation. Not a stored copy. Reasons:
- Stored copies become stale if System's identity changes.
- Navigation matches OBP rule 2 — reach through the object graph, don't decompose into fields.

Per-user signing (deferred per plan) is not in scope. Always System.

### Why `New(parent: ...)` not `New()` defaulting to current

Explicit. The action that opens a Service knows which actor it's working on behalf of. Defaulting to current is convenient but ambiguous in nested calls (parent's parent vs immediate parent). Make the caller name it: `app.Services.New(parent: app.CurrentActor)` or `app.Services.New(parent: app.User)`.

### What this stage does NOT ship

- **No retrofit of `http.request`** or other outbound modules to use Services. They keep their direct `HttpClient` usage. Wiring them through Services is a follow-up branch where the design earns its keep.
- **No `Service.@this.Run()`** or any execution method. Service doesn't execute; it just scopes.
- **No event firing on Service Channels yet** — that's Stage 8 (channel events apply uniformly to actor channels and Service channels).
