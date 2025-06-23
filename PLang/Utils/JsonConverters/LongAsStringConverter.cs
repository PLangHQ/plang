using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PLang.Utils.JsonConverters
{
	class LongAsStringConverter : JsonConverter<long>
	{
		public override void WriteJson(JsonWriter writer, long value, JsonSerializer serializer)
		{
			writer.WriteValue(value.ToString(System.Globalization.CultureInfo.InvariantCulture));
		}

		public override long ReadJson(JsonReader reader, Type objectType, long existingValue, bool hasExistingValue, JsonSerializer serializer)
		{
			return Convert.ToInt64(reader.Value, System.Globalization.CultureInfo.InvariantCulture);
		}
	}
}
