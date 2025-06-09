using PLang.Models.ObjectValueConverters;
using PLang.Runtime;
using System.Collections;

namespace PLang.Models.ObjectValueExtractors
{
	internal class DictionaryExtractor : IExtractor
	{
		private IDictionary dict;
		private readonly ObjectValue parent;

		public DictionaryExtractor(IDictionary dict, ObjectValue parent)
		{
			this.dict = dict;
			this.parent = parent;
		}


		public ObjectValue? Extract(PathSegment segment, MemoryStack? memoryStack = null)
		{
			if (segment.Type == SegmentType.Index) throw new NotImplementedException("Is Index on DictionaryExtractor");

			if (dict.Count == 0) return null;

			var key = TryGetKey(dict, segment.Value);
			if (key == null)
			{
				return ObjectValue.Nullable(segment.Value);
			}

			return new ObjectValue(segment.Value, dict[key], parent: parent, properties: parent.Properties);
		}

		private string? TryGetKey(IDictionary dict, string value)
		{
			foreach (var k in dict.Keys)
			{
				if (k is string strKey && string.Equals(strKey, value, StringComparison.OrdinalIgnoreCase))
					return strKey;
			}
			return null;
		}
	}
}
