namespace App.Callback;

/// <summary>
/// Callback record contract. Two implementers in v1: <see cref="AskCallback"/> (capture
/// the surviving variables + position; resume by binding and dispatching the original
/// ask action with the bound value) and <see cref="ErrorCallback"/> (capture the whole
/// App tree via Snapshot; resume by reconstructing a fresh App from the snapshot and
/// running from BottomFrame).
///
/// `Position` answers "where does the resumed run land?" Same shape on both impls.
/// `Serialize` produces ready-to-wire bytes (encrypted via crypto.encrypt; v1 = identity).
/// `Run` orchestrates verify-skipped-already → reconstruct → bind → jump → run end-to-end.
/// </summary>
public interface ICallback
{
    /// <summary>The Call frame at which the resumed run lands. Null only in degenerate cases.</summary>
    global::App.CallStack.RestoredFrame? Position { get; }

    /// <summary>
    /// Encrypted ready-to-wire bytes. v1 crypto is identity pass-through, so the bytes
    /// are JSON for now; the real symmetric crypto will land via the same surface.
    /// </summary>
    byte[] Serialize(global::App.Actor.Context.@this ctx);

    /// <summary>
    /// Reconstruct + bind + jump + run. Returns the resumed action's Data result so the
    /// caller can chain. Assumes the consumer has already verified the wire signature
    /// (callback.run does this via signing.verify before dispatching here).
    /// </summary>
    System.Threading.Tasks.Task<global::App.Data.@this> Run(global::App.Actor.Context.@this ctx);
}
