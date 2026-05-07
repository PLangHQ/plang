# Stage 9: `channel.migrate` action (API stub)

**Goal:** Lay the API surface for migrating a Session channel across identity-aware runtimes. Cross-device transport plug-in is deferred (no entry point exists for it yet); this stage ships only the action shape and the Channel-side `Migrate()` plumbing so the future transport just hands data to it.

**Scope:**
- New action `channel.migrate` in the `channel` module (Stage 5).
- Add a `Migrate()` method on `Channel.@this` that captures the channel's state into a serialisable, signed envelope.
- Document the migration capability in `Documentation/Runtime2/cool.md` (already done).
- **Excluded:** actual transport (TCP/HTTP/etc. delivery between runtimes) — that lands when there's a forcing function.

**Deliverables:**

1. **`App/modules/channel/migrate.cs`** — action handler.
   ```csharp
   [Action("migrate")]
   public partial class Migrate : IContext, IChannel
   {
       public partial Data.@this<string> Name { get; init; }                     // channel name to migrate
       public partial Data.@this<App.Variables.Variable> Target { get; init; }   // target identity (recipient runtime's identity)
       
       public Task<Data.@this> Run() { /* see below */ }
   }
   ```
   Run logic: look up the named channel on current actor's Channels; verify it's a Session channel (Migrate doesn't apply to Message — one-shot has nothing to migrate); call `channel.Migrate(target)`; return the resulting envelope as `Data<MigrationEnvelope>`. Caller is expected to ship the envelope to the target via whatever transport.

2. **`App/Channels/Channel/this.cs`** (base) — add abstract `Migrate(Identity target) : Task<MigrationEnvelope>`. Default implementation throws `NotMigratable`. Session base or concrete subtypes override to actually serialise.

3. **`App/Channels/Channel/Session/Migration/this.cs`** (or alongside Channel.Session) — `MigrationEnvelope` type: `{ ChannelName, Role, Direction, Config (Buffer/Timeout/Mime/etc.), SerialisedState (subtype-specific bytes), TargetIdentity, SourceSignature }`. Signed by source's System identity; verifiable by target.

4. **Channel.Stream** override (in Session): captures the underlying Stream's position/buffer/state into `SerialisedState`. For console-stream-backed channels, migration probably fails (you can't move stdin to another machine) — document this and let `Migrate` return a typed error for unsupported underlying streams. Memory-backed Stream channels migrate trivially.

5. **Channel.Goal** override: serialises just the goal reference (name, version) and current Variables snapshot via the existing snapshot infrastructure from runtime2-callback.

6. **No receive side this stage.** A future entry point ("incoming migration handler") would deserialise an envelope, verify the source signature, reconstruct the channel on the target's actor, register it. Add a `Channel.@this.FromMigration(envelope)` static factory shape but leave it `throw NotImplemented` — the receive side belongs to whoever builds the cross-device transport.

**Dependencies:** Stages 1, 2, 3, 7. Channel base, Stream + Goal concretes (so Migrate has things to migrate), Services (Migration may use Service-style scoping when bridging). Snapshot infrastructure from runtime2-callback (already in place).

## Design

### Why API only

Until there's an entry point that actually receives migration envelopes, the receive side has no clear shape. Shipping just the send-side API surface makes the capability latent — present in the language, awaiting a transport. The day someone builds PlangMigration (or an existing entry point gets a migration receiver), they have a clean API to plug into.

This is the same shape as Stage 5's `webserver.start` would have been — but we held that whole concern back since it needed entry-point work too. `channel.migrate` is smaller because the *send* side is genuinely useful even alone (you can serialise + sign + log a channel state for audit / debug, even if no one's receiving).

### `Migrate()` returns Data, not raw bytes

The envelope is a Data object — `Data<MigrationEnvelope>`. That means it's signed (by the actor's System identity, current PLang signing chain), serialisable through the Serializers infrastructure, transportable via any other channel. The migration envelope IS PLang Data, not a bespoke binary blob.

This composes nicely: ship the envelope through any other channel — a file write, an HTTP POST, an MQTT publish — using existing channel infrastructure. The transport doesn't care it's a migration envelope; the receiver does.

### What this stage does NOT do

- **Doesn't actually migrate channels across processes.** The action returns the envelope; a transport must ship it; a receiver must reconstruct. None of those exist.
- **Doesn't restart goal execution on the target.** Resuming a suspended goal across runtimes is a separate problem (callback infrastructure handles intra-runtime; cross-runtime needs the same identity continuity but with transport).
- **No handshake protocol.** Sender signs; receiver verifies; that's the trust contract. No back-and-forth required.

(All in cool.md — file as a future capability, not a near-term feature. This stage is API-laying so the future has clean ground to build on.)
