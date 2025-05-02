using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PLang.Models.Formats
{
	public class JsonRpcResponse
	{
		public string Jsonrpc { get; set; } = "2.0";
		public string Method { get; set; }
		public object Params { get; set; }
		public object Id { get; set; }
	}

	public class PlangResponse
	{
		public Dictionary<string, object>? Headers { get; set; }
		public object Data { get; set; }
		public object? Callback { get; set; }
	}
}
