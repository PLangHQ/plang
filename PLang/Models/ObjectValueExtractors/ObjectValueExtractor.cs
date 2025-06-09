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

		public static ObjectValue? Extract(ObjectValue objectValue, List<PathSegment> segments, MemoryStack? memoryStack = null)
		{
			if (objectValue.Value == null) return null;

			if (segments[0].Value.StartsWith("!")) return ExtractProperty(objectValue, segments);

			ObjectValue? objectToExtractFrom = objectValue;
			foreach (var segement in segments)
			{
				if (objectToExtractFrom.Value == null) return null;

				var extractor = ExtractorFactory.GetExtractor(objectToExtractFrom, segement);
				if (extractor == null) throw new Exception("Could not find extractor");

				objectToExtractFrom = extractor.Extract(segement, memoryStack);

				if (objectToExtractFrom == null) break;
			}		
			
			return objectToExtractFrom;
		}

		

		public static ObjectValue? ExtractProperty(ObjectValue objectValue, List<PathSegment> segments)
		{

			throw new NotImplementedException("ObjectValueExtractor.ExtractProperty");
		}
		
	}
}
