using Microsoft.AspNetCore.Http;
using System.Runtime.CompilerServices;

namespace PLang.Services.OutputStream;

public static class ResponseWriteLock
{
	static readonly ConditionalWeakTable<HttpResponse, SemaphoreSlim> gates = new();

	public static SemaphoreSlim For(HttpResponse response)
		=> gates.GetValue(response, _ => new SemaphoreSlim(1, 1));
}
