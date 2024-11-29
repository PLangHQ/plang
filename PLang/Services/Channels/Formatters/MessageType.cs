namespace PLang.Services.Channels;

public enum MessageType
{
    // User categories
    UserOutput,
    UserAsk,
    UserError,
    UserNotification,

    // System categories
    SystemOutput,
    SystemError,
    SystemAsk,
    SystemNotification,
    SystemMetrics,
    SystemAudit,
    SystemEvent,
    SystemDebug,
    SystemWarning,
    SystemLog,
    SystemTrace
    
}

public static class MessageTypeExtensions
{
    public static string ToString(this MessageType value)
    {
        return value switch
        {
            MessageType.UserOutput => value.ToString(),
            MessageType.UserAsk => value.ToString(),
            MessageType.UserError => value.ToString(),
            MessageType.UserNotification => value.ToString(),

            // System types
            MessageType.SystemOutput => value.ToString(),
            MessageType.SystemError => "ERROR",
            MessageType.SystemAsk => "SYSTEM_ASK",
            MessageType.SystemNotification => "NOTIFICATION",
            MessageType.SystemMetrics => "METRICS",
            MessageType.SystemAudit => "AUDIT",
            MessageType.SystemEvent => "EVENT",
            MessageType.SystemDebug => "DEBUG",
            MessageType.SystemWarning => "WARNING",
            MessageType.SystemLog => "LOG",
            MessageType.SystemTrace => "TRACE",
            _ => value.ToString() // Default fallback
        };
    }
}