using System.Text;
using PLang.Runtime2.Engine.Context;

namespace PLang.Runtime2.Engine.Errors;

/// <summary>
/// Error that occurred during a DataSource operation (SQLite read/write/delete).
/// Captures table and key for diagnostics.
/// </summary>
public class DataSourceError : Error
{
    public override ErrorCategory Category => ErrorCategory.Runtime;

    public string? TableName { get; init; }
    public string? KeyName { get; init; }

    public DataSourceError(string message, string key = "DataSourceError", int statusCode = 500)
        : base(message, key, statusCode) { }

    public DataSourceError(string message, PLangContext context, string key = "DataSourceError", int statusCode = 500)
        : base(message, context, key, statusCode) { }

    public static DataSourceError FromException(Exception ex, string? tableName = null, string? keyName = null)
    {
        var (suggestion, errorKey) = ClassifyException(ex);
        return new DataSourceError(ex.Message, errorKey, 500)
        {
            Exception = ex,
            TableName = tableName,
            KeyName = keyName,
            FixSuggestion = suggestion
        };
    }

    private static (string? Suggestion, string Key) ClassifyException(Exception ex)
    {
        var msg = ex.Message;

        if (msg.Contains("database is locked", StringComparison.OrdinalIgnoreCase))
            return ("Another process may be holding a lock on the database file. Close other connections and retry.", "DatabaseLocked");

        if (msg.Contains("disk I/O error", StringComparison.OrdinalIgnoreCase) ||
            msg.Contains("disk full", StringComparison.OrdinalIgnoreCase))
            return ("The disk may be full or the database file may be on a read-only filesystem.", "DiskError");

        if (msg.Contains("corrupt", StringComparison.OrdinalIgnoreCase))
            return ("The database file may be corrupt. Delete the .db file and let it be recreated.", "DatabaseCorrupt");

        if (msg.Contains("permission", StringComparison.OrdinalIgnoreCase) ||
            msg.Contains("access", StringComparison.OrdinalIgnoreCase))
            return ("Check file system permissions on the .db directory.", "PermissionDenied");

        return (null, "DataSourceError");
    }

    protected override void FormatExtra(StringBuilder sb, string indent)
    {
        if (TableName != null)
            sb.AppendLine($"{indent}    Table: {TableName}");
        if (KeyName != null)
            sb.AppendLine($"{indent}    Key: {KeyName}");
    }
}
