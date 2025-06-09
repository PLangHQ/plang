using Newtonsoft.Json.Serialization;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using PLang.Attributes;

namespace PLang.Utils
{

	public class IgnoreWhenInstructedResolver : DefaultContractResolver
	{
		private readonly bool ignore;

		public IgnoreWhenInstructedResolver(bool ignore = false)
		{
			this.ignore = ignore;
		}
		protected override JsonProperty CreateProperty(MemberInfo member, MemberSerialization serialization)
		{
			var prop = base.CreateProperty(member, serialization);

			var hasAttr = member.GetCustomAttribute<IgnoreWhenInstructedAttribute>() != null;
			if (hasAttr && ignore)
			{
				prop.ShouldSerialize = _ => false;
			}

			return prop;
		}
	}
}
