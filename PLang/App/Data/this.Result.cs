using System.Text.Json.Serialization;
using App.Errors;

namespace App.Data;

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

    [JsonIgnore]
    public IError? Error { get; set; }

    [JsonIgnore]
    public List<Info>? Warnings { get; set; }

    [JsonIgnore]
    public bool Success => Error == null;

    public static implicit operator bool(@this d) => d.Success;

    // --- Static helpers (replace Return helpers) ---

    public static @this Ok() => new("");
    public static @this Ok(object? value, Type? type = null) => new("", value, type);
    public static @this FromError(IError error) => new("") { Error = error };
    public static T FromError<T>(IError error) where T : @this, new() => new() { Error = error };

    /// <summary>
    /// Produces a typed error Data from this instance's error. The error object creates the conversion.
    /// </summary>
    public T ToError<T>() where T : @this, new() => new() { Error = Error };

    /// <summary>
    /// Merge: combines two @this results (logic from Return.Merge).
    /// Treats Value as List&lt;Data&gt;, merge by Name (replace-or-append).
    /// </summary>
    public @this Merge(@this other)
    {
        if (other.Value == null) return this;

        var myData = Value as List<@this> ?? new();
        var otherData = other.Value as List<@this> ?? new();

        foreach (var data in otherData)
        {
            var existing = myData.FindIndex(d => string.Equals(d.Name, data.Name, StringComparison.OrdinalIgnoreCase));
            if (existing >= 0)
                myData[existing] = data;
            else
                myData.Add(data);
        }

        return new @this("") { Value = myData };
    }
}
