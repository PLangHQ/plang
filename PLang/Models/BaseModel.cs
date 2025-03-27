using Newtonsoft.Json;
using PLang.Modules.IdentityModule;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PLang.Models
{
	public abstract class BaseModel
	{
		
		public BaseModel()
		{
		}

		public string Type { get; set; }
		public Dictionary<string, object?> Headers { get; set; }
		

	}
}
