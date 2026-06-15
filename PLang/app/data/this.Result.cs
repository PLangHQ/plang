using System.Text.Json.Serialization;
using app.error;

namespace app.data;

using type = global::app.type.@this;

/// <summary>
/// Data — result/error concern.
/// Handled, Error, Warnings, Success, Ok/FromError factories, Merge.
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
    private IError? _error;
    private bool _errorObserved;

    [JsonIgnore]
    [Out, Store]
    public IError? Error
    {
        get { _errorObserved = true; return _error; }
        set { _error = value; _errorObserved = false; }
    }

    /// <summary>The door-failure seam — the failing TYPE authors its own error
    /// and reports it here (the blessed binding surface for door/Create
    /// implementations, beside ShallowClone/CloneError).</summary>
    public void Fail(IError error) { _error = error; _errorObserved = false; }

    /// <summary>True when a failure was recorded and no one has looked at it —
    /// the generator's post-Run epilogue reads this (without observing).</summary>
    internal bool HasUnobservedError => _error != null && !_errorObserved;

    /// <summary>The error without marking it observed — for relays (wire,
    /// debug views) that carry the failure without handling it.</summary>
    internal IError? ErrorUnobserved => _error;

    [JsonIgnore]
    public List<Info>? Warnings { get; set; }

    [JsonIgnore]
    [Out, Store]
    public bool Success { get { _errorObserved = true; return _error == null; } }

    public static implicit operator bool(@this d) => d.Success;

    // --- Static helpers (replace Return helpers) ---

    public static @this Ok() => new("");
    public static @this Ok(object? value, type? type = null) => new("", value, type);
    public static @this FromError(IError error) => new("") { Error = error };
    public static T FromError<T>(IError error) where T : @this, new() => new() { Error = error };

    /// <summary>
    /// Produces a typed error Data from this instance's error. The error object creates the conversion.
    /// </summary>
    public T ToError<T>() where T : @this, new() => new() { Error = Error };

}
