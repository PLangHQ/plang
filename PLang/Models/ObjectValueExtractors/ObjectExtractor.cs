using Microsoft.Playwright;
using Newtonsoft.Json.Linq;
using PLang.Models.ObjectValueConverters;
using PLang.Runtime;
using PLang.Utils;
using ReverseMarkdown.Converters;

namespace PLang.Models.ObjectValueExtractors
{
	internal class ObjectExtractor : IExtractor
	{
		private object obj;
		private readonly ObjectValue parent;
		private static List<string> ops = ["sum", "avg", "average", "mean", "max", "min", "count", "first", "last", "random", "range", "median"];
		public ObjectExtractor(object obj, ObjectValue parent)
		{
			this.obj = obj;
			this.parent = parent;
		}

		public ObjectValue? Extract(PathSegment segment, MemoryStack? memoryStack = null)
		{
			if (segment.Type == SegmentType.Index) throw new NotImplementedException("Is Index on DictionaryExtractor");

			if (obj == null) return null;

			var property = obj.GetType().GetProperties()
				.FirstOrDefault(p => p.Name.Equals(segment.Value, StringComparison.OrdinalIgnoreCase));
			if (property == null)
			{
				return CheckConverter(segment, memoryStack);
			}

			var newObj = property.GetValue(obj);
			if (newObj == null) return null;

			return new ObjectValue(property.Name, newObj, parent: parent, properties: parent.Properties);

		}

		private ObjectValue? CheckConverter(PathSegment segment, MemoryStack? memoryStack)
		{

			if (segment.Value.Equals("first", StringComparison.OrdinalIgnoreCase) || segment.Value.Equals("last", StringComparison.OrdinalIgnoreCase))
			{
				if (obj is ObjectValue ov) return new ObjectValue(segment.Value, ov.Value, parent: parent, properties: parent.Properties);
				return new ObjectValue(segment.Value, obj, parent: parent, properties: parent.Properties);
			}

			if (segment.Value.Equals("toDouble", StringComparison.OrdinalIgnoreCase))
			{
				if (double.TryParse(obj.ToString(), out double result))
				{
					return new ObjectValue(segment.Value, result, parent: parent, properties: parent.Properties);
				}
			}
			if (segment.Value.Equals("toBool", StringComparison.OrdinalIgnoreCase))
			{
				if (TypeHelper.IsBoolValue(obj.ToString(), out bool? boolValue))
				{
					return new ObjectValue(segment.Value, boolValue, parent: parent, properties: parent.Properties);
				}
			}


			if (segment.Value.Equals("toInt", StringComparison.OrdinalIgnoreCase) ||
				segment.Value.Equals("toLong", StringComparison.OrdinalIgnoreCase))
			{
				if (long.TryParse(obj.ToString(), out long longValue))
				{
					return new ObjectValue(segment.Value, longValue, parent: parent, properties: parent.Properties);
				}
			}

			if (segment.Value.Equals("toDate", StringComparison.OrdinalIgnoreCase) ||
				segment.Value.Equals("toDateTime", StringComparison.OrdinalIgnoreCase))
			{
				if (DateTime.TryParse(obj.ToString(), out DateTime dt))
				{
					return new ObjectValue(segment.Value, dt, parent: parent, properties: parent.Properties);
				}
			}
			
			if (ops.Any(p => p.Equals(segment.Value, StringComparison.OrdinalIgnoreCase))) {
				return new ObjectValue(segment.Value, obj, parent: parent, properties: parent.Properties);
			}
			

			if (obj is string str && !string.IsNullOrEmpty(str) && JsonHelper.IsJson(str, out object? parseObject) && parseObject is JToken token)
			{
				var jsonExtractor = new JsonExtractor(token, parent);
				return jsonExtractor.Extract(segment, memoryStack);
			}
		

			return null;
		}

		
	}
}
