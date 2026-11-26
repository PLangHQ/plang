using Newtonsoft.Json.Linq;
using PLang.Models.ObjectValueConverters;
using PLang.Runtime;
using System.Collections;

namespace PLang.Models.ObjectValueExtractors
{
	public interface IExtractor
	{
		ObjectValue? Extract(PathSegment segment, MemoryStack? memoryStack = null);
	}

	public static class ObjectValueExtractor
	{
		private static string GetObjectValueName(PathSegment segement, MemoryStack? memoryStack)
		{
			if (segement.Type != SegmentType.Index)
			{
				return segement.Value;
			}
			if (memoryStack == null) throw new Exception("MemoryStack is null trying to get index. This is not valid.");

			if (int.TryParse(segement.Value, out var literalIndex)) return $"[{literalIndex}]";
			if (TryGetIndex(memoryStack.Get(segement.Value), out var indexValue)) return $"[{indexValue}]";
			return $"[{segement.Value}]";
		}

		/// <summary>
		/// The value of an index variable (%list[i]%) as an int: a number of any numeric type, or a string of digits.
		/// False for anything else, e.g. a variable that does not exist yet while building.
		/// </summary>
		public static bool TryGetIndex(object? value, out int index)
		{
			index = 0;
			if (value is ObjectValue ov) value = ov.Value;
			if (value is JValue jValue) value = jValue.Value;
			switch (value)
			{
				case int i: index = i; return true;
				case long or short or byte or uint or ulong or ushort or sbyte:
					try { index = Convert.ToInt32(value); return true; } catch (OverflowException) { return false; }
				case double or float or decimal:
					var number = Convert.ToDecimal(value);
					if (number != decimal.Truncate(number) || number < int.MinValue || number > int.MaxValue) return false;
					index = (int)number;
					return true;
				case string s:
					return int.TryParse(s.Trim(), System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out index);
				default:
					return false;
			}
		}
		public static ObjectValue Extract(ObjectValue objectValue, List<PathSegment> segments, MemoryStack? memoryStack = null)
		{
			if (segments[0].Value.StartsWith("!")) return ExtractProperty(objectValue, segments);

			ObjectValue? objectToExtractFrom = objectValue;
			foreach (var segement in segments)
			{
				var nextParent = objectToExtractFrom;
				if (objectToExtractFrom.Value == null)
				{
					string name = GetObjectValueName(segement, memoryStack);					
					objectToExtractFrom = new ObjectValue(name, null, typeof(Nullable), objectToExtractFrom, false, objectToExtractFrom.Properties);
					if (objectToExtractFrom.Parent == null) objectToExtractFrom.Parent = nextParent;
					continue;
				}

				var extractor = ExtractorFactory.GetExtractor(objectToExtractFrom, segement);
				if (extractor == null) throw new Exception("Could not find extractor");

				var extractedObjectValue = extractor.Extract(segement, memoryStack);

				if (extractedObjectValue == null) {
					string name = GetObjectValueName(segement, memoryStack);
					objectToExtractFrom = new ObjectValue(name, null, typeof(Nullable), objectToExtractFrom, false, objectToExtractFrom.Properties);
				} else
				{
					objectToExtractFrom = extractedObjectValue;
					
				}
				if (objectToExtractFrom.Parent == null) objectToExtractFrom.Parent = nextParent;
			}		
			
			return objectToExtractFrom;
		}

		

		public static ObjectValue? ExtractProperty(ObjectValue objectValue, List<PathSegment> segments)
		{

			throw new NotImplementedException("ObjectValueExtractor.ExtractProperty");
		}
		
	}
}
