using Newtonsoft.Json.Linq;
using PLang.Models.ObjectValueConverters;
using PLang.Runtime;
using System.Collections;
using System.Linq;
using System.Linq.Dynamic.Core;

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
			if (segment.Type == SegmentType.Index)
			{
				// [%name%] on a dictionary is a key lookup, [2] is the second entry. Before this the segment
				// text ("item.key") was parsed as a number, which is the "not in a correct format" error.
				var indexValue = segment.ValueOfPath;
				if (indexValue is string keyName)
				{
					var foundKey = TryGetKey(dict, keyName);
					if (foundKey == null) return ObjectValue.Nullable(keyName);
					return new ObjectValue(keyName, dict[foundKey], parent: parent, properties: parent.Properties);
				}
				if (indexValue != null && long.TryParse(indexValue.ToString(), out long position))
				{
					var list = dict.Values.ToDynamicList();
					if (position >= 0 && list.Count > position) return new ObjectValue($"[{position}]", list[(int)position], parent: parent, properties: parent.Properties);
					return null;
				}
				throw new NotImplementedException($"Index [{segment.Value}] on a dictionary must be a key or a position");
			}
			else if (segment.Type == SegmentType.Property)
			{
				if (parent is IObjectValue parentOv)
				{
					var propertyValue = parentOv.Properties.FirstOrDefault(p => p.Name.Equals(segment.Value, StringComparison.OrdinalIgnoreCase));
					return propertyValue;
				}
			}

			if (dict.Count == 0) return null;

			var key = TryGetKey(dict, segment.Value);
			if (key != null)
			{
				return new ObjectValue(segment.Value, dict[key], parent: parent, properties: parent.Properties);
			}

			var property = dict.GetType().GetProperties().FirstOrDefault(p => p.Name.Equals(segment.Value, StringComparison.OrdinalIgnoreCase));
			if (property != null)
			{
				var value= property.GetValue(dict);
				return new ObjectValue(segment.Value, value, parent: parent, properties: parent.Properties);
			}




			return ObjectValue.Nullable(segment.Value);
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
