using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static PLang.Modules.BaseBuilder;

namespace PLang.Building.Model
{

	public record Instruction(object Action)
	{
		public string? Text { get; set; }
		public bool Reload { get; set; }
		public LlmQuestion LlmQuestion { get; set; }
		public bool RunOnBuild { get; set; }
		public GenericFunction[] GetFunctions()
		{
			if (Action == null) return new GenericFunction[0];

			if (Action.GetType() == typeof(JArray))
			{
				return JsonConvert.DeserializeObject<GenericFunction[]>(Action.ToString());
			}
			else if (Action.GetType() == typeof(JObject))
			{
				var gf = JsonConvert.DeserializeObject<GenericFunction>(Action.ToString());
				return new GenericFunction[] { gf };
			}

			if (Action.ToString().EndsWith("[]")) return Action as GenericFunction[];
			return new GenericFunction[] { Action as GenericFunction };
		}
	};
}
