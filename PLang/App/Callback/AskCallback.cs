using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using App.CallStack;
using App.Channels.Serializers;

namespace App.Callback;

/// <summary>
/// Slim callback for the ask-user issuer. Carries only what the resumed dispatch needs:
/// the position (Goal stub + step/action indices), the actor name (referent integrity:
/// names not refs), and the surviving variables annotated by the developer's <c>vars:</c>
/// list. Everything else is fresh App boot on resume — no full Snapshot.
/// </summary>
public sealed class AskCallback : ICallback
{
    /// <summary>The Call frame at which the resumed dispatch lands.</summary>
    public RestoredFrame? Position { get; init; }

    /// <summary>Name of the actor that issued the ask — "User" / "Service" / "System".</summary>
    public string ActorName { get; init; } = "User";

    /// <summary>The variables the developer annotated as surviving the ask.</summary>
    public List<global::App.Data.@this> Variables { get; init; } = new();

    public byte[] Serialize(global::App.Actor.Context.@this ctx)
    {
        var wire = new Wire
        {
            Type = "ask",
            ActorName = ActorName,
            Position = Position == null ? null : PositionWire.From(Position),
            Variables = Variables.Select(VariableWire.From).ToList()
        };
        var bytes = JsonSerializer.SerializeToUtf8Bytes(wire, _options);
        // crypto.encrypt v1 is identity; the call goes through the action handler so the
        // wiring is real (when the real impl lands, only the action body changes).
        var encrypted = ctx.App.RunAction<App.modules.crypto.encrypt>(
            new App.modules.crypto.encrypt { Input = global::App.Data.@this<byte[]>.Ok(bytes) }, ctx)
            .GetAwaiter().GetResult();
        return (byte[])(encrypted.Value ?? bytes);
    }

    /// <summary>
    /// Defense-in-depth wire size cap (1 MB). The channel layer is the primary control
    /// (e.g. http body cap); this cap stops a misconfigured channel from streaming a
    /// multi-GB body into JsonSerializer. Security v1 S-F3.
    /// </summary>
    internal const int MaxWireBytes = 1 * 1024 * 1024;

    public static AskCallback Deserialize(byte[] bytes, global::App.Actor.Context.@this ctx)
    {
        if (bytes.Length > MaxWireBytes)
            throw new InvalidOperationException(
                $"AskCallback: wire payload exceeds size cap ({bytes.Length} > {MaxWireBytes} bytes)");
        var decrypted = ctx.App.RunAction<App.modules.crypto.decrypt>(
            new App.modules.crypto.decrypt { Input = global::App.Data.@this<byte[]>.Ok(bytes) }, ctx)
            .GetAwaiter().GetResult();
        var plain = (byte[])(decrypted.Value ?? bytes);
        if (plain.Length > MaxWireBytes)
            throw new InvalidOperationException(
                $"AskCallback: decrypted payload exceeds size cap ({plain.Length} > {MaxWireBytes} bytes)");
        var wire = JsonSerializer.Deserialize<Wire>(plain, _options)
                   ?? throw new InvalidOperationException("AskCallback: empty wire payload");

        return new AskCallback
        {
            ActorName = wire.ActorName,
            Position = wire.Position?.Resolve(ctx),
            Variables = (wire.Variables ?? new()).Select(v => v.ToData(ctx)).ToList()
        };
    }

    /// <summary>
    /// Optional answer to inject on resume — bound under <c>!ask.answer</c> so the
    /// ask handler returns it instead of issuing a fresh ask. Caller (HTTP channel)
    /// sets this from the user's response before invoking <c>callback.run</c>.
    /// </summary>
    public object? Answer { get; init; }

    public async Task<global::App.Data.@this> Run(global::App.Actor.Context.@this ctx)
    {
        if (Position == null)
            return global::App.Data.@this.FromError(
                new global::App.Errors.ServiceError("AskCallback has no Position", "NoPosition", 400));

        // Bind surviving variables onto the resumed context's Variables.
        // Skip !-prefixed names — infra variables are not user state and must not be injected from wire.
        foreach (var v in Variables)
        {
            if (!string.IsNullOrEmpty(v.Name) && !v.Name.StartsWith("!"))
                ctx.Variables.Set(v.Name, v.Value);
        }

        // Inject the answer under the resume sentinel; ask handler reads + consumes it.
        if (Answer != null)
            ctx.Variables.Set(global::App.modules.output.ask.AnswerVariableName, Answer);

        // Dispatch the original action through the live execution path. The resumed run
        // lands at Position; the ask handler sees !ask.answer and short-circuits to it
        // instead of issuing a fresh ask.
        return await ctx.App.Run(Position.Action, ctx);
    }

    // --- Wire shapes ---

    private static readonly JsonSerializerOptions _options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
        // Strip [Sensitive]-marked properties from the wire — captured Variables can carry
        // arbitrary objects whose typed properties may include secrets. Security v1 S-F4.
        TypeInfoResolver = new DefaultJsonTypeInfoResolver
        {
            Modifiers = { global::App.Channels.Serializers.Filters.Sensitive.Strip }
        }
    };

    internal sealed class Wire
    {
        public string Type { get; set; } = "ask";
        public string ActorName { get; set; } = "User";
        public PositionWire? Position { get; set; }
        public List<VariableWire>? Variables { get; set; }
    }

    internal sealed class PositionWire
    {
        public string GoalPrPath { get; set; } = "";
        public string GoalHash { get; set; } = "";
        public int StepIndex { get; set; }
        public int ActionIndex { get; set; }

        public static PositionWire From(RestoredFrame f) => new()
        {
            GoalPrPath = f.Goal?.PrPath ?? "",
            GoalHash = f.Goal?.Hash ?? "",
            StepIndex = f.StepIndex,
            ActionIndex = f.ActionIndex
        };

        public RestoredFrame Resolve(global::App.Actor.Context.@this ctx)
        {
            var goal = ctx.App.Goals.Get(GoalPrPath)
                ?? throw new global::App.Errors.CallbackGoalNotFound(GoalPrPath);
            var liveHash = goal.Hash ?? "";
            if (!string.Equals(liveHash, GoalHash, StringComparison.OrdinalIgnoreCase))
                throw new global::App.Errors.CallbackGoalHashMismatch(GoalPrPath, GoalHash, liveHash);
            if (StepIndex < 0 || StepIndex >= goal.Steps.Count)
                throw new global::App.Errors.CallbackGoalNotFound($"{GoalPrPath} (stepIndex {StepIndex} out of range)");
            var step = goal.Steps[StepIndex];
            if (ActionIndex < 0 || ActionIndex >= step.Actions.Count)
                throw new global::App.Errors.CallbackGoalNotFound($"{GoalPrPath} (actionIndex {ActionIndex} out of range at step {StepIndex})");
            var action = step.Actions[ActionIndex];
            return new RestoredFrame(action, goal, StepIndex, ActionIndex, "");
        }
    }

    internal sealed class VariableWire
    {
        public string Name { get; set; } = "";
        public object? Value { get; set; }

        public static VariableWire From(global::App.Data.@this d) => new()
        {
            Name = d.Name,
            Value = d.Value
        };

        public global::App.Data.@this ToData(global::App.Actor.Context.@this ctx)
        {
            var d = new global::App.Data.@this(Name, Value);
            d.Context = ctx;
            return d;
        }
    }
}
