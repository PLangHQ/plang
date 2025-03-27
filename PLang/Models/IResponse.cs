using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PLang.Models
{
	public interface IResponse
	{
	}

	public class HttpResponse : IResponse
	{
		public Dictionary<string, object?> Headers { get; set; }
		public Dictionary<string, object?> ContentHeaders { get; set; }
		public bool IsSuccess { get; set; } = false;
		public string ReasonPhrase { get; set; }
		public int StatusCode { get; set; }	

	}
}
