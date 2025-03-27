using PLang.Errors.Runtime;
using PLang.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace PLang.Models
{
	public class Identity : BaseModel
	{
		private string identifier;

		public Identity(string identifier, string name, string type = "plang-0.1")
		{
			this.identifier = identifier;
			this.Name = name;
			this.Type = type;
		}

		public override string ToString()
		{
			return this.identifier.ToString();
		}
		public string Identifier { get { return identifier; } }
		public string Name { get; set; }

		[Newtonsoft.Json.JsonIgnore]
		[IgnoreDataMemberAttribute]
		[System.Text.Json.Serialization.JsonIgnore]
		public Signature? Signature { get; set; }


	}
}
