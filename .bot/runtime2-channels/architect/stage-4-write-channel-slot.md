# Stage 4: Write Channel slot + IChannel update + builder

**Goal:** Update the `IChannel` marker to inject a single resolved `Channel` (not the registry); update the source generator to resolve channel name → channel instance per action invocation; teach the builder to recognise channel-name selection from intent.

**Scope:**
- Modify `App/modules/IChannel.cs` to declare `Channel { get; set; }` of type `Channel.@this` (not `Channels { get; set; }`).
- Update source generator (`PLang.Generators/Emission/Action/this.cs`) to emit channel resolution code.
- Modify `App/modules/output/write.cs` to use `Channel.WriteAsync(Data)`.
- Builder catalog change: `IChannel` actions advertise the channel name slot; available channels per actor are passed to the LLM so it can map intent.
- Delete the `WriteAsync(Write action)` overload on `App.Channels.@this`.
- **Excluded:** events on channels (Stage 8); concrete channels (Stages 1-3); console wiring (Stage 6).

**Deliverables:**

1. **`App/modules/IChannel.cs`** updated:
   ```csharp
   public interface IChannel
   {
       Channel.@this Channel { get; set; }
   }
   ```
2. **`App/modules/output/write.cs`** simplified:
   ```csharp
   public partial class Write : IContext, IChannel
   {
       public partial Data.@this Data { get; init; }
       // Channel property is source-gen-emitted from IChannel.
       public Task<Data.@this> Run() => Channel.WriteAsync(Data);
   }
   ```
3. **Source generator** (`PLang.Generators/Emission/Action/this.cs`):
   - Today emits `Channels = (context.Actor ?? app.User).Channels` for IChannel actions.
   - Update to emit channel resolution: read the action's channel-name slot from its data, look up in current actor's Channels, fall back to actor's `Output` role channel if unspecified, set `Channel = resolved`.
4. **`App.Channels.@this`** gets a `Resolve(string? name) -> Channel.@this` method: if name is null/empty, returns the channel registered with `Role.Output`; otherwise returns the channel by name; throws `ChannelNotFound` if absent.
5. **Builder catalog updates** (likely `App/Modules/this.cs` Describe path):
   - For `IChannel` actions, expose a `channel` parameter in the catalog described to the LLM.
   - Pass the per-actor channel inventory at build time so the LLM can map step text intent (e.g., "to logger", "at the debug channel", any 100 ways) to one of the registered names.
   - No `to <name>` parsing rule. Pure intent matching against an inventory.
6. **Delete** the `WriteAsync(Write action)` overload on `App.Channels.@this`. `Channels` namespace stops importing `App.modules.output`.

**Dependencies:** Stage 1 (Channel base for the resolved Channel type to exist).

## Design

### The flow per action invocation

1. Action JSON (built by the LLM at build time) carries the channel name in some property (e.g. `"channel": "logger"`). If no channel was specified by the user, this property is absent.
2. At action setup (source-gen-emitted code), look up `(context.Actor ?? app.User).Channels.Resolve(jsonChannelName)`.
3. `Resolve(null)` returns the actor's `Role.Output` channel; `Resolve("logger")` returns the named channel; absent name throws `ChannelNotFound` (which becomes a typed Data.Error — see plan's "Writing to a non-existent channel" section).
4. Source gen sets `action.Channel = resolved`.
5. `Write.Run()` runs `Channel.WriteAsync(Data)` — talks to one Channel, doesn't see the registry.

### Builder-side: intent over patterns

Today the builder might match `to <name>` or other syntax patterns. Per design (plan.md "Intent over patterns"), the right model is to give the LLM the channel inventory and let it map user intent.

What the catalog passes:
- Action description: "Writes Data to a channel."
- Channel parameter: optional; one of the known channel names. Description tells the LLM "select the channel that matches the user's intent."
- The known names: standard role-channels (`output`, `error`) + any names registered earlier in the goal via `channel.add` (the builder tracks registrations as it processes steps in order).

Cross-goal registrations aren't knowable at build time — accept the limitation, runtime fails loud if the channel doesn't exist.

### What the source-gen emits

Roughly (pseudocode in the generator):

```csharp
if (info.ImplementsIChannel)
{
    sb.AppendLine("        var __actor = context.Actor ?? app.User;");
    sb.AppendLine("        var __chName = action.Json[\"channel\"]?.GetString();");
    sb.AppendLine("        Channel = __actor.Channels.Resolve(__chName);");
}
```

Replaces today's single line that sets `Channels = ...`.

### Why drop the `WriteAsync(Write action)` overload

Today `App.Channels.@this` has an overload that takes the Write action and reaches into its data to extract the channel name and resolve %vars% via the action's Context. That choreography:
- Couples `Channels` namespace to `App.modules.output`.
- Decomposes the action into parts.
- Reads variables on behalf of the action (the action should do that itself).

After Stage 4, `Channels.WriteAsync(string name, ...)` is gone too — only the public surface is `Channels.Resolve(name)` which returns a Channel. Writing is the Channel's job, not the registry's. Single responsibility per type.
