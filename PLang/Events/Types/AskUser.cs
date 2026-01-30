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

		public static async Task<(object? Answer, IError? Error)> GetAnswer(IEngine engine, PLangContext context, string question)
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
				return (null, goalResult.Error);
			}
			return (null, null);


		}
	}
}

