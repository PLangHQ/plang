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

			var indexValue = memoryStack.GetObjectValue(segement.Value).ValueAs<int>();
			return $"[{indexValue}]";

		}
		public static ObjectValue Extract(ObjectValue objectValue, List<PathSegment> segments, MemoryStack? memoryStack = null)
		{
			if (segments[0].Value.StartsWith("!")) return ExtractProperty(objectValue, segments);

			ObjectValue? objectToExtractFrom = objectValue;
			foreach (var segement in segments)
			{
				if (objectToExtractFrom.Value == null)
				{
					string name = GetObjectValueName(segement, memoryStack);					
					objectToExtractFrom = new ObjectValue(name, null, typeof(Nullable), objectToExtractFrom, false, objectToExtractFrom.Properties);
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
			}		
			
			return objectToExtractFrom;
		}

		

		public static ObjectValue? ExtractProperty(ObjectValue objectValue, List<PathSegment> segments)
		{

			throw new NotImplementedException("ObjectValueExtractor.ExtractProperty");
		}
		
	}
}
