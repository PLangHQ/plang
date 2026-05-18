using app.modules.code;

namespace app.data.code;

/// <summary>
/// Provider interface for grep operations on Data.
/// Default: text line matching with regex, line numbers, context.
/// Override: register a provider for video (subtitle search), images (OCR), etc.
/// </summary>
public interface IGrep : ICode
{

    /// <summary>
    /// Search data for a pattern. Returns matching content as @this.
    /// </summary>
    /// <param name="data">The data to search</param>
    /// <param name="pattern">Search pattern (regex or text)</param>
    /// <param name="contextLines">Number of lines before/after each match (0 = no context)</param>
    /// <returns>Matching content as @this</returns>
    @this Grep(@this data, string pattern, int contextLines = 0);

    /// <summary>
    /// Count matches in data.
    /// </summary>
    @this GrepCount(@this data, string pattern);
}
