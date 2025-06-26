using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static PLang.Modules.BaseBuilder;

namespace PLang.Utils
{
	public class GenericFunctionHelper
	{
		public static string? GetParameterValueAsString(IGenericFunction gf, string parameterName, string? defaultValue = null)
		{
			if (gf.Parameters == null) return defaultValue;
			var parameter = gf.Parameters.FirstOrDefault(p => p.Name.Equals(parameterName, StringComparison.OrdinalIgnoreCase));
			if (parameter == null) return defaultValue;

			return parameter.Value?.ToString() ?? null;
		}
		public static Dictionary<string, object>? GetParameterValueAsDictionary(IGenericFunction gf, string parameterName)
		{
			if (gf.Parameters == null) return null;

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

		public static List<string>? GetParameterValueAsList(IGenericFunction gf, string parameterName)
		{
			if (gf.Parameters == null) return null;

			var parameterValue = gf.Parameters.FirstOrDefault(p => p.Name == parameterName)?.Value?.ToString();
			if (parameterValue == null) return null;

			if (JsonHelper.IsJson(parameterValue))
			{
				var parameters = JsonConvert.DeserializeObject<List<string>>(parameterValue);
				return parameters;
			}
			var list = new List<string>();
			list.Add(parameterValue);
			return list;
		}
		public static bool? GetParameterValueAsBool(IGenericFunction gf, string parameterName)
		{
			if (gf.Parameters == null) return null;

			object? obj = gf.Parameters.FirstOrDefault(p => p.Name == parameterName)?.Value;
			if (obj == null) return null;

			bool.TryParse(obj.ToString(), out bool boolValue);
			return boolValue;
		}
	}
}
