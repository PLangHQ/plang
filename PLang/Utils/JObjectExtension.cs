using Newtonsoft.Json.Linq;

namespace PLang.Utils
{
	public static class JObjectExtension
	{

		public static string NoFormatting(this JObject obj)
		{
			return obj.ToString(Newtonsoft.Json.Formatting.None);
		}
		public static string NoFormatting(this JArray obj)
		{
			return obj.ToString(Newtonsoft.Json.Formatting.None);
		}
		public static string NoFormatting(this JToken obj)
		{
			return obj.ToString(Newtonsoft.Json.Formatting.None);
		}
		public static string NoFormatting(this JProperty obj)
		{
			return obj.ToString(Newtonsoft.Json.Formatting.None);
		}
		
		public static Dictionary<string, object?> ToDictionary(this JObject obj)
		{
			Dictionary<string, object?> dict = new();
			var properties = obj.Properties();
			foreach (var property in properties)
			{
				if (!dict.ContainsKey(property.Name))
				{
					if (VariableHelper.IsVariable(property.Value.ToString()))
					{
						dict.Add(property.Name, property.Value.ToString());
					}
					else
					{
						dict.Add(property.Name, property.Value);
					}
				}
			}
			return dict;
		}
	}
}
