using Newtonsoft.Json;
using System;

namespace PLang.Utils.JsonConverters
{
	// Deserializing ObjectValue.Type (a System.Type) from its type-name string throws
	// for names that can't be resolved — anonymous types, dynamic [code] assemblies, etc.
	// That would abort the whole memoryStack round-trip. Here an unresolvable name just
	// becomes null, so the variable's value is still preserved.
	public class SafeTypeJsonConverter : JsonConverter
	{
		public override bool CanConvert(Type objectType)
		{
			return typeof(Type).IsAssignableFrom(objectType);
		}

		public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
		{
			writer.WriteValue((value as Type)?.AssemblyQualifiedName);
		}

		public override object? ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer)
		{
			var name = reader.Value as string;
			if (string.IsNullOrEmpty(name)) return null;
			try
			{
				return Type.GetType(name);
			}
			catch
			{
				return null;
			}
		}
	}
}
