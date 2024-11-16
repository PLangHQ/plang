namespace Plang.Channels
{
	/// <summary>
	/// Represents categories for routing messages.
	/// </summary>
	public enum Category
	{
		// User categories
		UserOutput,
		UserError,
		UserAsk,
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
}
