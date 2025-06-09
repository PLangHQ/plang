using PLang.Models.ObjectValueConverters;
using PLang.Runtime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PLang.Models.ObjectValueExtractors
{
	internal class ListObjectExtractor : IExtractor
	{
		private IEnumerable<object> list;
		private readonly ObjectValue parent;

		public ListObjectExtractor(IEnumerable<object> list, ObjectValue parent)
		{
			this.list = list;
			this.parent = parent;
		}

		public ObjectValue? Extract(PathSegment segment, MemoryStack? memoryStack = null)
		{
			if (list == null) return ObjectValue.Nullable(segment.Value);
			
			List<ObjectValue> newList = new();
			foreach (var item in list)
			{
				if (item is ObjectValue objectValue && objectValue.Name.Equals(segment.Value, StringComparison.OrdinalIgnoreCase))
				{
					newList.Add(objectValue);
				}
				else
				{
					var extractor = ExtractorFactory.GetExtractor(new ObjectValue(parent.Name, item), segment);
					var obj = extractor.Extract(segment, memoryStack);
					if (obj == null) continue;
					newList.Add(obj);
				}
			}
			return new ObjectValue(segment.Value, newList, parent: parent, properties: parent?.Properties);
		}
	}
}
