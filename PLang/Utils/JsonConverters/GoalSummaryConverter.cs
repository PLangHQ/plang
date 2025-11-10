using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PLang.Building.Model;

namespace PLang.Utils.JsonConverters
{
	public class GoalSummaryConverter : JsonConverter<Goal>
	{
		public override void WriteJson(JsonWriter writer, Goal value, JsonSerializer serializer)
		{
			var obj = JObject.FromObject(value, serializer);
			obj.Remove("GoalSteps");

			obj.WriteTo(writer);
		}

		public override Goal ReadJson(JsonReader reader, Type objectType, Goal existingValue, bool hasExistingValue, JsonSerializer serializer)
			=> serializer.Deserialize<Goal>(reader); // optional, usually unused
	}

}
