using Markdig.Helpers;
using PLang.Models.ObjectValueConverters;
using PLang.Runtime;
using PLang.Utils;
using System;
using System.Collections;
using System.Collections.Generic;

namespace PLang.Models.ObjectValueExtractors
{
	internal class ListExtractor : IExtractor
	{
		private IEnumerable<object> list;
		private readonly ObjectValue parent;

		public ListExtractor(IEnumerable<object> enumerable, ObjectValue parent)
		{
			this.list = enumerable;
			this.parent = parent;
		}

		public ObjectValue? Extract(PathSegment segment, MemoryStack? memoryStack = null)
		{

			if (list == null || !list.Any()) return null;

			int index = 0;

			if (segment.Type == SegmentType.Path)
			{
				var extractor = ExtractorFactory.GetListExtractor(list, segment, parent);
				if (extractor == null) throw new Exception("Could not find extractor");

				return extractor.Extract(segment, memoryStack);
			}

			if (!int.TryParse(segment.Value, out index))
			{
				if (memoryStack == null) throw new Exception("MemoryStack cannot be null when searching for variable in " + segment.Value);

				index = memoryStack.Get<int>(segment.Value);
			}


			return new ObjectValue(segment.Value, list.ElementAtOrDefault(index), parent: parent, properties: parent.Properties);

		}

	}
}
