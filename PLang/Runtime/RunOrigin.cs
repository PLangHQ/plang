using PLang.Interfaces;

namespace PLang.Runtime
{
	/// <summary>
	/// Who a run acts for, which is what %Identity% is.
	/// - A run of the app itself, a local process (the command line, the scheduler), acts as the app: %Identity% is %MyIdentity%.
	/// - A run started from outside (a nostr message, an iroh command, a watched file, a blockchain event, a websocket message)
	///   is the identity that signed it, or nobody.
	/// - An HTTP request is the identity of its X-Signature, or nobody.
	/// A listener runs its goals on the context the app started in, so the origin cannot live on the context: each outside
	/// entry opens a scope around the goal it runs, and the scope flows with the run into every goal it calls.
	/// </summary>
	public static class RunOrigin
	{
		private sealed record Scope(bool IsApp, string? Identity);

		private static readonly AsyncLocal<Scope?> current = new();

		/// <summary>The run is for whoever signed what came in from outside; null when nobody did.</summary>
		public static IDisposable External(string? identity) => Enter(new Scope(false, identity));

		/// <summary>The run is the app's own, e.g. a scheduled goal, whatever started the code that schedules it.</summary>
		public static IDisposable App() => Enter(new Scope(true, null));

		public static string? Identity(PLangContext? context, Func<string?> myIdentity)
		{
			var scope = current.Value;
			if (scope != null) return scope.IsApp ? myIdentity() : scope.Identity;
			if (context == null) return null;
			if (context.ExecutionMode == ExecutionMode.HttpRequest || context.HttpContext != null) return context.Identity;
			return string.IsNullOrEmpty(context.Identity) ? myIdentity() : context.Identity;
		}

		private static IDisposable Enter(Scope scope)
		{
			var previous = current.Value;
			current.Value = scope;
			return new Exit(previous);
		}

		private sealed class Exit(Scope? previous) : IDisposable
		{
			public void Dispose() => current.Value = previous;
		}
	}
}
