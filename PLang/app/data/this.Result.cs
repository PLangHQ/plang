using System.Text.Json.Serialization;
using app.error;

namespace app.data;

using type = global::app.type.@this;

/// <summary>
/// Data — result/error concern.
/// Handled, Error, Success, Ok/FromError factories, Merge.
/// </summary>
public partial class @this
{
    /// <summary>
    /// When true, a before-event has handled this action/step/goal.
    /// The original execution should be skipped and this Data's Value used instead.
    /// </summary>
    [JsonIgnore]
    public bool Handled { get; set; }

    /// <summary>
    /// Set by goal.return to signal RunSteps to stop iteration — even for successful results.
    /// </summary>
    [JsonIgnore]
    public bool Returned { get; set; }

    /// <summary>
    /// How many goal boundaries this return crosses. Decremented as each goal's Start returns it.
    /// </summary>
    [JsonIgnore]
    public int ReturnDepth { get; set; } = 1;

    [JsonIgnore]
    [Out, Store]
    public global::app.error.@this? Error { get; set; }

    // What travels with a result, taken whole from the Data this one is made over: whether it was handled and how far a
    // return goes, always; its error, the snapshot an exit holds and the signature it arrived in, unless this Data holds
    // its own. What a value carries (its attachment) is the maker's: a face shares it, a copy takes a copy.
    private protected void Take(@this result)
    {
        Error ??= result.Error;
        Handled = result.Handled;
        Returned = result.Returned;
        ReturnDepth = result.ReturnDepth;
        Snapshot ??= result.Snapshot;
        _signature ??= result._signature;
    }

    /// <summary>The door-failure seam — the failing TYPE authors its own error
    /// and reports it here (the blessed binding surface for door/Create
    /// implementations, beside As&lt;T&gt;(answer)/CloneError).</summary>
    public void Fail(global::app.error.@this error)
    {
        // An error meeting its first run here takes this Data's context as where it happened.
        error.Context ??= Context;
        Error = error;
    }

    [JsonIgnore]
    [Out, Store]
    public bool Success => Error == null;

    public static implicit operator bool(@this d) => d.Success;

    /// <summary>
    /// True when this Data's type exits the goal — an <see cref="global::app.IExitsGoal"/> class
    /// (an ask awaiting its answer, a stateless suspend). Asked through this Data's own context:
    /// a typed absence (`ask` with no value yet) names its type without carrying the class.
    /// </summary>
    [JsonIgnore]
    public bool Exits
    {
        get
        {
            var clr = Type?.ClrType;
            return clr != null && typeof(global::app.IExitsGoal).IsAssignableFrom(clr);
        }
    }

    /// <summary>
    /// The step loop's one stop test: an unhandled failure, an explicit return, or a result that
    /// exits the goal. A value can declare itself resolved (<see cref="global::app.IExitsGoal.ShouldExit"/>)
    /// — a typed return like Data&lt;Ask&gt; with its answer bound flows through. A raw-backed,
    /// untouched payload is never a flow-control signal, so the probe never materializes it.
    /// </summary>
    public bool ShouldExit()
    {
        if (!Success && !Handled) return true;
        if (Returned) return true;
        if (!RawUntouched && Peek() is global::app.IExitsGoal eg) return eg.ShouldExit();
        return Exits;
    }

    // --- Static helpers (replace Return helpers) ---

    public static @this FromError(global::app.error.@this error, actor.context.@this context)
        => new("", context: context) { Error = error };

    /// <summary>
    /// Produces a typed error Data from this instance's error. The error object creates the conversion.
    /// </summary>
    public T ToError<T>() where T : @this, new() => new() { Error = Error };

}
