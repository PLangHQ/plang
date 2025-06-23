using Microsoft.Playwright;
using PLang.Models.ObjectValueConverters;
using PLang.Runtime;
using PLang.Utils;

namespace PLang.Models.ObjectValueExtractors
{
	internal class ObjectExtractor : IExtractor
	{
		private object obj;
		private readonly ObjectValue parent;

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
			if (segment.Value.Equals("toDouble", StringComparison.OrdinalIgnoreCase))
			{
				if (double.TryParse(obj.ToString(), out double result))
				{
					return new ObjectValue(segment.Value, result, parent: parent, properties: parent.Properties);
				}
			}
			if (segment.Value.Equals("toBool", StringComparison.OrdinalIgnoreCase))
			{
				if (TypeHelper.IsBoolValue(segment.Value, out bool? boolValue))
				{
					return new ObjectValue(segment.Value, boolValue, parent: parent, properties: parent.Properties);
				}
			}


			if (segment.Value.Equals("toInt", StringComparison.OrdinalIgnoreCase) ||
				segment.Value.Equals("toLong", StringComparison.OrdinalIgnoreCase))
			{
				if (long.TryParse(segment.Value, out long longValue))
				{
					return new ObjectValue(segment.Value, longValue, parent: parent, properties: parent.Properties);
				}
			}

			if (segment.Value.Equals("toDate", StringComparison.OrdinalIgnoreCase) ||
				segment.Value.Equals("toDateTime", StringComparison.OrdinalIgnoreCase))
			{
				if (DateTime.TryParse(segment.Value, out DateTime dt))
				{
					return new ObjectValue(segment.Value, dt, parent: parent, properties: parent.Properties);
				}
			}

		

			return null;
		}
	}
}
