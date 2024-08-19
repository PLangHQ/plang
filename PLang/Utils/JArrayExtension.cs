using Newtonsoft.Json.Linq;
using PLang.Runtime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace PLang.Utils
{
	public static class JArrayExtension
	{

		public static List<Dictionary<string, object>> ToList(this JArray jArray)
		{
			var records = new List<Dictionary<string, object>>();

			foreach (var item in jArray)
			{
				var record = item.ToObject<Dictionary<string, object>>();
				records.Add(record);
			}
			return records;
		}

		public static JObject? After(this JArray jArray, JObject item)
		{
			for (int i = 0; i < jArray.Count - 1; i++)
			{
				if (JToken.DeepEquals(jArray[i], item))
				{
					return (JObject)jArray[i + 1];
				}
			}

			return null;
			
		}
	}
}
