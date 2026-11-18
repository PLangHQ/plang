using Microsoft.Extensions.Logging;
using System.Runtime.CompilerServices;

namespace PLang
{
	/// <summary>
	/// Makes `logger.LogTrace($"...")` and `logger.LogDebug($"...")` free when the level is off.
	///
	/// With the framework overload `LogTrace(string, params object[])` the interpolated text is built (and every
	/// `{stopwatch.ElapsedMilliseconds}` evaluated) before the logger is asked whether the level is on. These
	/// extensions take an interpolated string handler instead: the compiler picks the handler overload for an
	/// interpolated string, and the handler skips formatting when `ILogger.IsEnabled` is false. This class sits in
	/// namespace PLang so that it is found before the `using Microsoft.Extensions.Logging` extensions in every
	/// file whose namespace starts with PLang. Calls that pass a plain string are not affected.
	/// The text that is logged when the level is on is the same as before.
	/// </summary>
	public static class LazyLogExtensions
	{
		public static void LogTrace(this ILogger logger, [InterpolatedStringHandlerArgument("logger")] ref TraceLogHandler handler)
		{
			if (handler.Enabled) LoggerExtensions.LogTrace(logger, handler.ToStringAndClear());
		}

		public static void LogDebug(this ILogger logger, [InterpolatedStringHandlerArgument("logger")] ref DebugLogHandler handler)
		{
			if (handler.Enabled) LoggerExtensions.LogDebug(logger, handler.ToStringAndClear());
		}
	}

	[InterpolatedStringHandler]
	public ref struct TraceLogHandler
	{
		private DefaultInterpolatedStringHandler inner;
		public bool Enabled { get; }

		public TraceLogHandler(int literalLength, int formattedCount, ILogger logger, out bool isEnabled)
		{
			Enabled = isEnabled = logger.IsEnabled(LogLevel.Trace);
			inner = Enabled ? new DefaultInterpolatedStringHandler(literalLength, formattedCount) : default;
		}

		public void AppendLiteral(string s) { if (Enabled) inner.AppendLiteral(s); }
		public void AppendFormatted<T>(T value) { if (Enabled) inner.AppendFormatted(value); }
		public void AppendFormatted<T>(T value, string? format) { if (Enabled) inner.AppendFormatted(value, format); }
		public void AppendFormatted<T>(T value, int alignment) { if (Enabled) inner.AppendFormatted(value, alignment); }
		public void AppendFormatted<T>(T value, int alignment, string? format) { if (Enabled) inner.AppendFormatted(value, alignment, format); }
		public string ToStringAndClear() => inner.ToStringAndClear();
	}

	[InterpolatedStringHandler]
	public ref struct DebugLogHandler
	{
		private DefaultInterpolatedStringHandler inner;
		public bool Enabled { get; }

		public DebugLogHandler(int literalLength, int formattedCount, ILogger logger, out bool isEnabled)
		{
			Enabled = isEnabled = logger.IsEnabled(LogLevel.Debug);
			inner = Enabled ? new DefaultInterpolatedStringHandler(literalLength, formattedCount) : default;
		}

		public void AppendLiteral(string s) { if (Enabled) inner.AppendLiteral(s); }
		public void AppendFormatted<T>(T value) { if (Enabled) inner.AppendFormatted(value); }
		public void AppendFormatted<T>(T value, string? format) { if (Enabled) inner.AppendFormatted(value, format); }
		public void AppendFormatted<T>(T value, int alignment) { if (Enabled) inner.AppendFormatted(value, alignment); }
		public void AppendFormatted<T>(T value, int alignment, string? format) { if (Enabled) inner.AppendFormatted(value, alignment, format); }
		public string ToStringAndClear() => inner.ToStringAndClear();
	}
}
