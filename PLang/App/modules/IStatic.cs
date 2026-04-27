using System.Collections.Concurrent;

namespace App.modules;

/// <summary>
/// Capability interface giving action handlers access to module-scoped static state.
/// State persists across steps within the same execution context.
/// Actions in the same module namespace share the same Static dictionary.
///
/// Example: timer.start stores start time, timer.end reads it back.
/// The source generator detects IStatic and wires the Static property automatically.
/// </summary>
public interface IStatic
{
    ConcurrentDictionary<string, object?> Static { get; set; }
}
