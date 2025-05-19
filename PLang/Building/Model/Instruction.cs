using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using PLang.Exceptions;
using PLang.Models;
using static PLang.Modules.BaseBuilder;

namespace PLang.Building.Model
{

	public record Instruction(object Action)
	{
		
		public string? Text { get; set; }
		public bool Reload { get; set; }
		public LlmRequest LlmRequest { get; set; }
		public bool RunOnBuild { get; set; }
		public Dictionary<string, object?> Properties { get; set; } = new();
		public GenericFunction[] GetFunctions()
		{
			try
			{
				string? action = Action.ToString();
				if (action == null) return new GenericFunction[0];

				if (Action.GetType() == typeof(JArray))
				{
					return JsonConvert.DeserializeObject<GenericFunction[]>(action) ?? [];
				}
				else if (Action.GetType() == typeof(JObject))
				{
					var gf = JsonConvert.DeserializeObject<GenericFunction>(action);
					if (gf == null) return [];

					return new GenericFunction[] { gf };
				}

				if (action.EndsWith("[]")) return Action as GenericFunction[] ?? [];
				return new GenericFunction[] { Action as GenericFunction };
			} catch (JsonSerializationException ex)
			{
				if (ex.Message.Contains("Could not find member")) throw new InvalidInstructionFileException("Instruction file was not valid. You might need to rebuild your code", ex);
				throw;
			}

		}
	};
}
