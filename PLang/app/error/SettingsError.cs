using app.actor.context;

namespace app.error;

/// <summary>
/// Error that occurred during a Settings operation (SQLite read/write/delete).
/// Captures table and key for diagnostics.
/// </summary>
public class SettingsError : Error
{
    public string? TableName { get; init; }
    public string? KeyName { get; init; }

    public SettingsError(string message, string key = "SettingsError", global::app.type.item.status.@this? status = null)
        : base(message, key, status ?? 500) { }

    public SettingsError(string message, actor.context.@this context, string key = "SettingsError", global::app.type.item.status.@this? status = null)
        : base(message, context, key, status ?? 500) { }

    public static SettingsError FromException(Exception ex, string? tableName = null, string? keyName = null)
    {
        var (suggestion, errorKey) = ClassifyException(ex);
        return new SettingsError(ex.Message, errorKey, 500)
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

        return (null, "SettingsError");
    }
}
