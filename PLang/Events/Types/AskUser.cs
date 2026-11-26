using NJsonSchema.Validation;
using PLang.Building.Model;
using PLang.Building.Parsers;
using PLang.Errors;
using PLang.Errors.Builder;
using PLang.Exceptions;
using PLang.Interfaces;
using PLang.Runtime;

namespace PLang.Events.Types
{
	public class AskUser
	{

		/// <summary>
		/// A run nobody can answer at the console: a web request, a scheduled or "dont wait" run. Not while building.
		/// </summary>
		public static bool IsNotInteractive(PLangContext? context)
		{
			if (context == null) return false;
			if (AppContext.TryGetSwitch("Builder", out bool building) && building) return false;
			return context.HttpContext != null || context.ExecutionMode == ExecutionMode.HttpRequest
				|| context.ExecutionMode == ExecutionMode.ScheduledTask || context.IsAsync;
		}

		public static async Task<(object? Answer, IError? Error)> GetAnswer(IEngine engine, PLangContext context, string question)
		{
			// The system ask goes to the console. In a run nobody answers there it waited for a line on stdin that never
			// came, and a web request hung until the client gave up.
			if (IsNotInteractive(context))
			{
				return (null, new Error($"The app needs an answer to a question, and this run cannot ask it (it is not at a terminal): {question}",
					Key: "CannotAsk", StatusCode: 500));
			}


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
```"));
			}

			context.MemoryStack.Put("__plang_actor", "system");
			context.MemoryStack.Put("__plang_question", question);

			var goalResult = await engine.RunGoal(askUser, context);
			if (goalResult.Error is Return r)
			{
				if (r.ReturnVariables.Count == 0)
				{
					return (null, new Error("Nothing got returned from goal"));
				}

				return (r.ReturnVariables[0].Value, null);
			}
			else if (goalResult.Error != null)
			{
				// Every ask in plang comes through here. If the ask goal itself cannot run, its own
				// failure says nothing about why anyone was being asked; the question does, so keep it.
				goalResult.Error.ErrorChain.Add(new Error($"Could not ask the user this question: {question}", Key: "AskFailed"));
				return (null, goalResult.Error);
			}
			return (null, null);


		}
	}
}

