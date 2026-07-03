namespace app.error;

/// <summary>
/// Error indicating a required value is missing and needs to be provided by the user.
/// Carries the table and data key where the value should be stored once provided.
/// Runtime handling (prompt-store-retry) is out of scope — comes in a separate branch.
/// </summary>
public class AskError : Error
{
    public override ErrorCategory Category => ErrorCategory.Application;

    /// <summary>
    /// The DataSource table where the value belongs (e.g., "settings").
    /// </summary>
    public string Table { get; }

    /// <summary>
    /// The key within the table (e.g., "ApiKey").
    /// </summary>
    public string DataKey { get; }

    public AskError(string message, string table, string dataKey)
        : base(message, "AskUser", 400)
    {
        Table = table;
        DataKey = dataKey;
        FixSuggestion = $"Set the value: set settings '{dataKey}' = '<value>'";
    }

    /// <summary>An ask carries where the value belongs — the table + key to store it.</summary>
    protected override void WriteSpecific(global::app.channel.serializer.IWriter writer)
    {
        writer.Name("table");   writer.String(Table);
        writer.Name("dataKey"); writer.String(DataKey);
    }
}
