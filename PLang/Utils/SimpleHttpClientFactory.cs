using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PLang.Utils
{
	public class SimpleHttpClientFactory : IHttpClientFactory
	{
		public HttpClient CreateClient(string name)
		{
			return new HttpClient();
		}
	}

}
