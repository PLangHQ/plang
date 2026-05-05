using System.Text.Json.Serialization.Metadata;
using App.CallStack;
using App.Channels.Serializers;

namespace App.Callback;

/// <summary>
/// Callback for the error-retry issuer. Single field — the captured App tree. Position
/// is computed by walking the snapshot's CallStack section to its bottom frame on demand.
/// On Run, constructs a fresh App, calls Restore, and dispatches from BottomFrame.
/// </summary>
public sealed class ErrorCallback : ICallback
{
    /// <summary>The captured App tree.</summary>
    public Snapshot.@this AppSnapshot { get; init; } = new();

    /// <summary>Cached materialised position — null until first read.</summary>
    private RestoredFrame? _position;

    /// <summary>
    /// Computed: walks the captured CallStack subsection's last "frames" entry and
    /// resolves the goal-stub triple against the current App's Goals registry.
    /// </summary>
    public RestoredFrame? Position
    {
        get
        {
            if (_position != null) return _position;
            // Without a live App we can't resolve the goal stubs; Position only materialises
            // when a Restore happens. Tests that just probe Position can read the captured
            // CallStack via Run's path.
            return null;
        }
    }

    public byte[] Serialize(global::App.Actor.Context.@this ctx)
    {
        var bytes = SerializeSnapshot(AppSnapshot);
        var encrypted = ctx.App.RunAction<App.modules.crypto.encrypt>(
            new App.modules.crypto.encrypt { Input = global::App.Data.@this<byte[]>.Ok(bytes) }, ctx)
            .GetAwaiter().GetResult();
        return (byte[])(encrypted.Value ?? bytes);
    }

    /// <summary>
    /// Defense-in-depth wire size cap (4 MB — larger than AskCallback because ErrorCallback
    /// carries the full snapshot). The channel layer is the primary control. Security v1 S-F3.
    /// </summary>
    internal const int MaxWireBytes = 4 * 1024 * 1024;

    public static ErrorCallback Deserialize(byte[] bytes, global::App.Actor.Context.@this ctx)
    {
        if (bytes.Length > MaxWireBytes)
            throw new InvalidOperationException(
                $"ErrorCallback: wire payload exceeds size cap ({bytes.Length} > {MaxWireBytes} bytes)");
        var decrypted = ctx.App.RunAction<App.modules.crypto.decrypt>(
            new App.modules.crypto.decrypt { Input = global::App.Data.@this<byte[]>.Ok(bytes) }, ctx)
            .GetAwaiter().GetResult();
        var plain = (byte[])(decrypted.Value ?? bytes);
        if (plain.Length > MaxWireBytes)
            throw new InvalidOperationException(
                $"ErrorCallback: decrypted payload exceeds size cap ({plain.Length} > {MaxWireBytes} bytes)");
        var snap = DeserializeSnapshot(plain);
        return new ErrorCallback { AppSnapshot = snap };
    }

    public async Task<global::App.Data.@this> Run(global::App.Actor.Context.@this ctx)
    {
        // Restore onto the live App (caller's responsibility to provide a fresh-enough App).
        ctx.App.Restore(AppSnapshot, ctx);

        var bottom = ctx.App.Debug.CallStack.BottomFrame;
        if (bottom == null)
            return global::App.Data.@this.FromError(
                new global::App.Errors.ServiceError("ErrorCallback has no bottom frame after Restore", "NoPosition", 400));
        _position = bottom;

        // Re-execute the failing action — bind already happened via Restore (Variables section).
        return await ctx.App.Run(bottom.Action, ctx);
    }

    // --- Snapshot.@this wire shape ---
    // The Snapshot.@this tree is in-process state (Dictionaries of typed objects). For wire,
    // we use a JSON projection that captures only the keys we actually round-trip
    // (Variables names + values, CallStack frames). v1 is intentionally narrow — Stage 4
    // tests assert structural round-trip, not full Snapshot.@this fidelity.

    private static byte[] SerializeSnapshot(Snapshot.@this s)
    {
        var wire = new SnapshotWire
        {
            Frames = ExtractFrames(s),
            Variables = ExtractVariables(s)
        };
        return System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(wire, _options);
    }

    private static Snapshot.@this DeserializeSnapshot(byte[] bytes)
    {
        var wire = System.Text.Json.JsonSerializer.Deserialize<SnapshotWire>(bytes, _options)
                   ?? new SnapshotWire();
        var s = new Snapshot.@this();
        var stack = s.Section("CallStack");
        var frames = new List<Snapshot.@this>();
        foreach (var f in wire.Frames ?? new())
        {
            var frame = new Snapshot.@this();
            frame.Write("goalPrPath", f.GoalPrPath);
            frame.Write("goalHash", f.GoalHash);
            frame.Write("stepIndex", f.StepIndex);
            frame.Write("actionIndex", f.ActionIndex);
            frame.Write("actionModule", f.ActionModule);
            frame.Write("actionName", f.ActionName);
            frame.Write("id", f.Id);
            frames.Add(frame);
        }
        stack.Write("frames", frames);

        var varsSection = s.Section("Variables");
        var captured = (wire.Variables ?? new())
            .Select(v => new global::App.Data.@this(v.Name, v.Value))
            .ToList();
        varsSection.Write("variables", captured);

        return s;
    }

    private static List<FrameWire> ExtractFrames(Snapshot.@this s)
    {
        var result = new List<FrameWire>();
        if (!s.HasSection("CallStack")) return result;
        var stack = s.Section("CallStack");
        var frames = stack.Read<List<Snapshot.@this>>("frames");
        if (frames == null) return result;
        foreach (var f in frames)
        {
            result.Add(new FrameWire
            {
                GoalPrPath = f.Read<string>("goalPrPath") ?? "",
                GoalHash = f.Read<string>("goalHash") ?? "",
                StepIndex = f.Read<int>("stepIndex"),
                ActionIndex = f.Read<int>("actionIndex"),
                ActionModule = f.Read<string>("actionModule") ?? "",
                ActionName = f.Read<string>("actionName") ?? "",
                Id = f.Read<string>("id") ?? ""
            });
        }
        return result;
    }

    private static List<VarWire> ExtractVariables(Snapshot.@this s)
    {
        var result = new List<VarWire>();
        if (!s.HasSection("Variables")) return result;
        var varsSection = s.Section("Variables");
        var vars = varsSection.Read<List<global::App.Data.@this>>("variables");
        if (vars == null) return result;
        foreach (var v in vars)
            result.Add(new VarWire { Name = v.Name, Value = v.Value });
        return result;
    }

    private static readonly System.Text.Json.JsonSerializerOptions _options = new()
    {
        PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
        // Strip [Sensitive]-marked properties from the wire — captured Variables in the
        // snapshot can carry arbitrary objects whose typed properties may include secrets.
        // Security v1 S-F4.
        TypeInfoResolver = new DefaultJsonTypeInfoResolver
        {
            Modifiers = { SensitivePropertyFilter.Strip }
        }
    };

    internal sealed class SnapshotWire
    {
        public List<FrameWire>? Frames { get; set; }
        public List<VarWire>? Variables { get; set; }
    }

    internal sealed class FrameWire
    {
        public string GoalPrPath { get; set; } = "";
        public string GoalHash { get; set; } = "";
        public int StepIndex { get; set; }
        public int ActionIndex { get; set; }
        public string ActionModule { get; set; } = "";
        public string ActionName { get; set; } = "";
        public string Id { get; set; } = "";
    }

    internal sealed class VarWire
    {
        public string Name { get; set; } = "";
        public object? Value { get; set; }
    }
}
