using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using PLang.Runtime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PLang.Models.ObjectValueConverters
{
	public class JsonObjectValueConverter : JsonConverter<IObjectValue>
	{
		public override ObjectValue ReadJson(JsonReader reader, Type objectType, IObjectValue existingValue, bool hasExistingValue, JsonSerializer serializer)
		{
			var token = JToken.Load(reader);
			return new ObjectValue("object", token.ToObject<object>(), objectType);
		}

		public override void WriteJson(JsonWriter writer, IObjectValue value, JsonSerializer serializer)
		{
			serializer.Serialize(writer, value.Value);
		}
	}
}
