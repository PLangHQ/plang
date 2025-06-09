using Microsoft.Playwright;
using PLang.Models.ObjectValueConverters;
using PLang.Runtime;

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
			if (property == null) return null;

			var newObj = property.GetValue(obj);
			if (newObj == null) return null;

			return new ObjectValue(property.Name, newObj, parent: parent, properties: parent.Properties);

		}
	}
}
