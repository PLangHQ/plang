using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PLang.Utils
{
	public class OperatorHelper
	{

		public static Dictionary<string, string>? ApplyOperator(Dictionary<string, string>? dict, List<string>? keys = null, string? keyOperator = "equals", string? value = null, string? valueOperator = "contains")
		{
			if (dict == null) return null;

			var dict1 = dict.Where(p =>
			{
				bool returnValue = true;
				if (keys != null && value != null)
				{
					var key = keys.FirstOrDefault(a => a.Equals(p.Key, StringComparison.OrdinalIgnoreCase));
					if (key == null) return false; 
					
					returnValue = Operator(p.Key, key, keyOperator) && Operator(p.Value.ToString(), value, valueOperator);					
				}
				else if (keys != null)
				{
					var key = keys.FirstOrDefault(a => a.Equals(p.Key, StringComparison.OrdinalIgnoreCase));
					if (key == null) return false;

					returnValue = Operator(p.Key, key, keyOperator);
				}
				else if (value != null)
				{
					returnValue = Operator(p.Value.ToString(), value, valueOperator);
				}
				return returnValue;
			});

			return dict1.ToDictionary();
		}



		public static bool Operator(string propertyName, string propertyToFilterOn, string operatorProperty)
		{
			switch (operatorProperty.ToLower())
			{
				case "equals":
				case "=":
				case "==":
					return propertyName.Equals(propertyToFilterOn, StringComparison.OrdinalIgnoreCase);
				case "!=":
					return !propertyName.Equals(propertyToFilterOn, StringComparison.OrdinalIgnoreCase);
				case "startswith":
					return propertyName.StartsWith(propertyToFilterOn, StringComparison.OrdinalIgnoreCase);
				case "endswith":
					return propertyName.EndsWith(propertyToFilterOn, StringComparison.OrdinalIgnoreCase);
				case "contains":
					return propertyName.Contains(propertyToFilterOn, StringComparison.OrdinalIgnoreCase);
				default:
					return false;
			}
		}
	}

	
}
