using System.Text.Json.Serialization;
using PLang.Runtime2.Engine.Errors;

namespace PLang.Runtime2.Engine.Memory;

/// <summary>
/// Data — result/error concern.
/// Handled, Error, Warnings, Success, Ok/FromError factories, Merge.
/// </summary>
public partial class Data
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

    public static implicit operator bool(Data d) => d.Success;

    // --- Static helpers (replace Return helpers) ---

    public static Data Ok() => new("");
    public static Data Ok(object? value, Type? type = null) => new("", value, type);
    public static Data FromError(IError error) => new("") { Error = error };
    public static T FromError<T>(IError error) where T : Data, new() => new() { Error = error };

    /// <summary>
    /// Produces a typed error Data from this instance's error. The error object creates the conversion.
    /// </summary>
    public T ToError<T>() where T : Data, new() => new() { Error = Error };

    /// <summary>
    /// Merge: combines two Data results (logic from Return.Merge).
    /// Treats Value as List&lt;Data&gt;, merge by Name (replace-or-append).
    /// </summary>
    public Data Merge(Data other)
    {
        if (other.Value == null) return this;

        var myData = Value as List<Data> ?? new();
        var otherData = other.Value as List<Data> ?? new();

        foreach (var data in otherData)
        {
            var existing = myData.FindIndex(d => string.Equals(d.Name, data.Name, StringComparison.OrdinalIgnoreCase));
            if (existing >= 0)
                myData[existing] = data;
            else
                myData.Add(data);
        }

        return new Data("") { Value = myData };
    }
}
