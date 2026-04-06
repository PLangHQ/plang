using App.Engine.Providers;

namespace App.Engine.Variables.Providers;

/// <summary>
/// Provider interface for grep operations on Data.
/// Default: text line matching with regex, line numbers, context.
/// Override: register a provider for video (subtitle search), images (OCR), etc.
/// </summary>
public interface IGrepProvider : IProvider
{

    /// <summary>
    /// Search data for a pattern. Returns matching content as Data.
    /// </summary>
    /// <param name="data">The data to search</param>
    /// <param name="pattern">Search pattern (regex or text)</param>
    /// <param name="contextLines">Number of lines before/after each match (0 = no context)</param>
    /// <returns>Matching content as Data</returns>
    Data Grep(Data data, string pattern, int contextLines = 0);

    /// <summary>
    /// Count matches in data.
    /// </summary>
    Data GrepCount(Data data, string pattern);
}
