using NJsonSchema.Validation;
using PLang.Building.Model;
using PLang.Building.Parsers;
using PLang.Errors;
using PLang.Errors.Builder;
using PLang.Exceptions;
using PLang.Interfaces;
using PLang.Models;
using PLang.Runtime;

namespace PLang.Events.Types
{
	/// <summary>
	/// Channels for different types of system asks
	/// </summary>
	public static class AskChannel
	{
		public const string Default = "default";
		public const string Settings = "settings";
		public const string FileAccess = "fileaccess";
	}

	public class AskUser
	{
		/// <summary>
		/// Gets an answer from the user via the appropriate channel handler or AskSystem goal.
		/// </summary>
		/// <param name="engine">The engine instance</param>
		/// <param name="context">The PLang context</param>
		/// <param name="question">The question to ask</param>
		/// <param name="channel">The channel to use (default, settings, fileaccess, etc.)</param>
		/// <returns>The answer and any error</returns>
		public static async Task<(object? Answer, IError? Error)> GetAnswer(IEngine engine, PLangContext context, string question, string channel = AskChannel.Default)
		{
			// First, check if there's a channel handler registered for system actor on this channel
			var handler = context.GetChannelHandler("system", channel);

			// If no specific channel handler, try the default channel
			if (handler == null && channel != AskChannel.Default)
			{
				handler = context.GetChannelHandler("system", AskChannel.Default);
			}

			if (handler != null)
			{
				// Route to the registered goal handler
				return await RunChannelHandler(engine, context, handler, question, channel);
			}

			// Fall back to AskSystem goal lookup (existing behavior)
			return await RunAskSystemGoal(engine, context, question, channel);
		}

		private static async Task<(object? Answer, IError? Error)> RunChannelHandler(
			IEngine engine, PLangContext context, GoalToCallInfo handler, string question, string channel)
		{
			// Clone the handler to avoid modifying the registered one
			var handlerToRun = new GoalToCallInfo(handler.Name, new Dictionary<string, object?>(handler.Parameters ?? new()));

			// Set up parameters for the handler
			handlerToRun.Parameters["question"] = question;
			handlerToRun.Parameters["__plang_question"] = question;
			handlerToRun.Parameters["actor"] = "system";
			handlerToRun.Parameters["channel"] = channel;

			// Also put in memory stack for compatibility
			context.MemoryStack.Put("__plang_actor", "system");
			context.MemoryStack.Put("__plang_question", question);
			context.MemoryStack.Put("__plang_channel", channel);

			// Run the goal using the goal name
			var goalResult = await engine.Run(handlerToRun.Name, context);

			return ProcessGoalResult(goalResult);
		}

		private static async Task<(object? Answer, IError? Error)> RunAskSystemGoal(
			IEngine engine, PLangContext context, string question, string channel)
		{
			var askUser = engine.PrParser.GetEvent("AskSystem");
			if (askUser == null) askUser = engine.PrParser.GetSystemEvent("AskSystem");
			if (askUser == null)
			{
				return (null, new Error("Ask system goal could not be found.",
					FixSuggestion: @"Add a new file to your project, AskSystem.goal, here is the code:
```plang
AskSystem
- ask ""%__plang_question%"", channel=""system"", write to %__plang_answer%
- return %__plang_answer%
```

Or register a channel handler in your Setup goal:
```plang
Setup
- on channel 'system' 'default', call !MyAskHandler
```"));
			}

			context.MemoryStack.Put("__plang_actor", "system");
			context.MemoryStack.Put("__plang_question", question);
			context.MemoryStack.Put("__plang_channel", channel);

			var goalResult = await engine.RunGoal(askUser, context);

			return ProcessGoalResult(goalResult);
		}

		private static (object? Answer, IError? Error) ProcessGoalResult((object? Result, IError? Error) goalResult)
		{
			// Engine puts ReturnVariables in Result (not Error) when a goal has "return" statement
			// The Return error is caught by the engine and its variables are extracted to Result
			if (goalResult.Result is List<ObjectValue> returnVars && returnVars.Count > 0)
			{
				return (returnVars[0].Value, null);
			}

			// Legacy path: Return error in Error field (shouldn't happen with current engine)
			if (goalResult.Error is Return r)
			{
				if (r.ReturnVariables.Count == 0)
				{
					return (null, new Error("Nothing got returned from goal"));
				}

				return (r.ReturnVariables[0].Value, null);
			}

			if (goalResult.Error != null)
			{
				return (null, goalResult.Error);
			}

			return (null, null);
		}
	}
}

