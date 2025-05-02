using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static PLang.Modules.BaseBuilder;

namespace PLang.Utils
{
	public class GeneralFunctionHelper
	{
		public static string? GetParameterValueAsString(GenericFunction gf, string parameterName, string? defaultValue = null)
		{
			return gf.Parameters.FirstOrDefault(p => p.Name == parameterName)?.Value?.ToString() ?? defaultValue;
		}
		public static Dictionary<string, object>? GetParameterValueAsDictionary(GenericFunction gf, string parameterName)
		{
			var parameterValue = gf.Parameters.FirstOrDefault(p => p.Name == parameterName)?.Value?.ToString();
			if (parameterValue == null) return null;

			if (JsonHelper.IsJson(parameterValue))
			{
				var parameters = JsonConvert.DeserializeObject<Dictionary<string, object>>(parameterValue);
				return parameters;
			}
			var dict = new Dictionary<string, object>();
			dict.Add(parameterName, parameterValue);
			return dict;
		}
		public static bool? GetParameterValueAsBool(GenericFunction gf, string parameterName)
		{
			object? obj = gf.Parameters.FirstOrDefault(p => p.Name == parameterName)?.Value;
			bool.TryParse(obj.ToString(), out bool boolValue);
			return boolValue;
		}
	}
}
