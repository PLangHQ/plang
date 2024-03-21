namespace PLang.Utils
{
	public class SimpleHttpClientFactory : IHttpClientFactory
	{
		public HttpClient CreateClient(string name)
		{
			var handler = new HttpClientHandler
			{
				AllowAutoRedirect = true,
				MaxAutomaticRedirections = 10
			};
			return new HttpClient(handler);
		}
	}

}
