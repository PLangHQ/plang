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
	}
}
