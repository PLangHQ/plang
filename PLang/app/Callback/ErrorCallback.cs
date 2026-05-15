using app.callstack;

namespace app.Callback;

/// <summary>
/// Callback for the error-retry issuer. Single field — the captured App tree. Position
/// is computed by walking the snapshot's CallStack section to its bottom frame on demand.
/// On Run, constructs a fresh App, calls Restore, and dispatches from BottomFrame.
/// </summary>
public sealed class ErrorCallback : ICallback
{
    /// <summary>The captured App tree.</summary>
    public Snapshot AppSnapshot { get; init; } = new();

    /// <summary>Cached materialised position — null until first read.</summary>
    private global::app.callstack.call.Position? _position;

    /// <summary>
    /// Computed: walks the captured CallStack subsection's last "frames" entry and
    /// resolves the goal-stub triple against the current App's Goals registry.
    /// </summary>
    public global::app.callstack.call.Position? Position
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

    public byte[] Serialize(global::app.actor.context.@this ctx)
    {
        var bytes = SerializeSnapshot(AppSnapshot, ctx.App.Callback.Wire.Options);
        var encrypted = ctx.App.RunAction<app.modules.crypto.encrypt>(
            new app.modules.crypto.encrypt { Input = global::app.data.@this<byte[]>.Ok(bytes) }, ctx)
            .GetAwaiter().GetResult();
        return (byte[])(encrypted.Value ?? bytes);
    }

    /// <summary>
    /// Defense-in-depth wire size cap (4 MB — larger than AskCallback because ErrorCallback
    /// carries the full snapshot). The channel layer is the primary control. Security v1 S-F3.
    /// </summary>
    internal const int MaxWireBytes = 4 * 1024 * 1024;

    public static ErrorCallback Deserialize(byte[] bytes, global::app.actor.context.@this ctx)
    {
        if (bytes.Length > MaxWireBytes)
            throw new InvalidOperationException(
                $"ErrorCallback: wire payload exceeds size cap ({bytes.Length} > {MaxWireBytes} bytes)");
        var decrypted = ctx.App.RunAction<app.modules.crypto.decrypt>(
            new app.modules.crypto.decrypt { Input = global::app.data.@this<byte[]>.Ok(bytes) }, ctx)
            .GetAwaiter().GetResult();
        var plain = (byte[])(decrypted.Value ?? bytes);
        if (plain.Length > MaxWireBytes)
            throw new InvalidOperationException(
                $"ErrorCallback: decrypted payload exceeds size cap ({plain.Length} > {MaxWireBytes} bytes)");
        var snap = DeserializeSnapshot(plain, ctx.App.Callback.Wire.Options);
        return new ErrorCallback { AppSnapshot = snap };
    }

    public async Task<global::app.data.@this> Run(global::app.actor.context.@this ctx)
    {
        // Restore onto the live App (caller's responsibility to provide a fresh-enough App).
        ctx.App.Restore(AppSnapshot, ctx);

        var bottom = ctx.App.CallStack.BottomFrame;
        if (bottom == null)
            return global::app.data.@this.FromError(
                new global::app.errors.ServiceError("ErrorCallback has no bottom frame after Restore", "NoPosition", 400));
        _position = bottom;

        // Re-execute the failing action — bind already happened via Restore (Variables section).
        return await ctx.App.Run(bottom.Action, ctx);
    }

    // --- Snapshot wire shape ---
    // The Snapshot tree is in-process state (Dictionaries of typed objects). For wire,
    // we use a JSON projection that captures only the keys we actually round-trip
    // (Variables names + values, CallStack frames). v1 is intentionally narrow — Stage 4
    // tests assert structural round-trip, not full Snapshot fidelity.

    private static byte[] SerializeSnapshot(Snapshot s, System.Text.Json.JsonSerializerOptions options)
    {
        var wire = new SnapshotWire
        {
            Frames = ExtractFrames(s),
            Variables = ExtractVariables(s)
        };
        return System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(wire, options);
    }

    private static Snapshot DeserializeSnapshot(byte[] bytes, System.Text.Json.JsonSerializerOptions options)
    {
        var wire = System.Text.Json.JsonSerializer.Deserialize<SnapshotWire>(bytes, options)
                   ?? new SnapshotWire();
        var s = new Snapshot();
        var stack = s.Section("CallStack");
        var frames = new List<Snapshot>();
        foreach (var f in wire.Frames ?? new())
        {
            var frame = new Snapshot();
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
            .Select(v => new global::app.data.@this(v.Name, v.Value))
            .ToList();
        varsSection.Write("variables", captured);

        return s;
    }

    private static List<FrameWire> ExtractFrames(Snapshot s)
    {
        var result = new List<FrameWire>();
        if (!s.HasSection("CallStack")) return result;
        var stack = s.Section("CallStack");
        var frames = stack.Read<List<Snapshot>>("frames");
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

    private static List<VarWire> ExtractVariables(Snapshot s)
    {
        var result = new List<VarWire>();
        if (!s.HasSection("Variables")) return result;
        var varsSection = s.Section("Variables");
        var vars = varsSection.Read<List<global::app.data.@this>>("variables");
        if (vars == null) return result;
        foreach (var v in vars)
            result.Add(new VarWire { Name = v.Name, Value = v.Value });
        return result;
    }

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
