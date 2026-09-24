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
    /// How many goal boundaries this return crosses. Decremented by RunGoalAsync.
    /// </summary>
    [JsonIgnore]
    public int ReturnDepth { get; set; } = 1;

    // Observation is opt-in handling (ruling 8): reading Success/Error IS the
    // guard. A handler that never looks at a failed param can't swallow it —
    // the generator's post-Run epilogue surfaces any UNOBSERVED param error as
    // the action's result, type-authored message intact.
    private global::app.error.Error? _error;
    private bool _errorObserved;

    [JsonIgnore]
    [Out, Store]
    public global::app.error.Error? Error
    {
        get { _errorObserved = true; return _error; }
        set { _error = value; _errorObserved = false; }
    }

    /// <summary>The door-failure seam — the failing TYPE authors its own error
    /// and reports it here (the blessed binding surface for door/Create
    /// implementations, beside As&lt;T&gt;(answer)/CloneError).</summary>
    public void Fail(global::app.error.Error error)
    {
        // An error meeting its first run here takes this Data's context as where it happened.
        error.Context ??= Context;
        _error = error;
        _errorObserved = false;
    }

    /// <summary>True when a failure was recorded and no one has looked at it —
    /// the generator's post-Run epilogue reads this (without observing).</summary>
    internal bool HasUnobservedError => _error != null && !_errorObserved;

    /// <summary>The error without marking it observed — for relays (wire,
    /// debug views) that carry the failure without handling it.</summary>
    internal global::app.error.Error? ErrorUnobserved => _error;

    [JsonIgnore]
    [Out, Store]
    public bool Success { get { _errorObserved = true; return _error == null; } }

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
            var clr = Type?.ClrType ?? (Type is { } t ? Context?.App.Type.Clr(t.Name) : null);
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

    public static @this Ok() => new("");
    public static @this Ok(object? value, type? type = null) => new("", value, type);
    public static @this FromError(global::app.error.Error error) => new("") { Error = error };
    public static T FromError<T>(global::app.error.Error error) where T : @this, new() => new() { Error = error };

    /// <summary>
    /// Produces a typed error Data from this instance's error. The error object creates the conversion.
    /// </summary>
    public T ToError<T>() where T : @this, new() => new() { Error = Error };

}
