namespace App.Channels.Channel;

/// <summary>
/// Stage 9 API stub: serialised, signed snapshot of a Channel suitable for
/// shipping to another identity-aware runtime, where it resumes with state
/// intact. The cross-device transport plug-in lands when the entry point
/// that consumes it (cool.md) ships; for now Migrate produces the envelope
/// and FromMigration throws NotImplemented on the receive side.
/// </summary>
public sealed class MigrationEnvelope
{
    public required string Name { get; init; }
    public required Role.@this Role { get; init; }
    public required ChannelDirection Direction { get; init; }
    public required ChannelConfigSnapshot Config { get; init; }

    /// <summary>Concrete-specific payload (goal name + variables snapshot for Goal channels; bytes/position for memory streams; null for non-migratable kinds).</summary>
    public object? Payload { get; init; }

    /// <summary>Signature attesting the envelope was produced under the source app's System identity.</summary>
    public required Signature Signature { get; init; }
}

/// <summary>Per-channel config carried in the envelope. ISO 8601 for Timeout.</summary>
public sealed class ChannelConfigSnapshot
{
    public long Buffer { get; init; }
    public TimeSpan Timeout { get; init; }
    public string Mime { get; init; } = "text/plain";
    public string Encoding { get; init; } = "utf-8";
    public string? Encryption { get; init; }
    public string? Signing { get; init; }
}

/// <summary>Migration envelope signature — identity reference + bytes.</summary>
public sealed class Signature
{
    /// <summary>The identity name that signed the envelope (always source app's System).</summary>
    public required string IdentityName { get; init; }
    /// <summary>Identity public key (for verification on the receive side).</summary>
    public required string PublicKey { get; init; }
    /// <summary>Signature bytes over (Name, Role, Direction, Config, Payload).</summary>
    public required byte[] Bytes { get; init; }
}
