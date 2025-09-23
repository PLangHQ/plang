using PLang.Services.OutputStream.Messages;
using System.Diagnostics;

namespace PLang.Services.OutputStream.Transformers
{
	public class TransformerHelper
	{

		public static async Task<SemaphoreSlim> GetGate(HttpContext httpContext, CancellationToken ct)
		{
			var gate = (SemaphoreSlim)(httpContext.Items[PlangTransformer.GateKey] ??= new SemaphoreSlim(1, 1));
			var got = await gate.WaitAsync(0, ct);
			if (!got)
			{
				int id = Environment.CurrentManagedThreadId;
				Console.WriteLine($"[{DateTime.UtcNow:O}] T{id} waiting for body stream…");
				var sw = Stopwatch.StartNew();
				await gate.WaitAsync(ct);
				sw.Stop();
				Console.WriteLine($"[{DateTime.UtcNow:O}] T{id} acquired after {sw.ElapsedMilliseconds} ms");
			}
			return gate;
		}


		public static object BuildEnvelope(OutMessage m) =>
		m switch
		{
			TextMessage t => new
			{
				kind = "text",
				channel = m.Channel,
				level = m.Level,
				status = m.StatusCode,
				target = m.Target,
				actions = m.Actions,
				content = t.Content,
			},
			RenderMessage r => new
			{
				kind = "render",
				channel = m.Channel,
				level = m.Level,
				status = m.StatusCode,
				target = m.Target,
				actions = m.Actions,
				content = r.Content,
			},
			ExecuteMessage e => new
			{
				kind = "execute",
				channel = m.Channel,
				level = m.Level,
				status = m.StatusCode,
				target = m.Target,
				actions = m.Actions,
				function = e.Function,
				data = e.Data,
			},
			AskMessage a => new
			{
				kind = "ask",
				channel = m.Channel,
				level = m.Level,
				status = m.StatusCode,
				target = m.Target,
				actions = m.Actions,
				template = a.Content,
				callback = a.Callback,
				callbackData = a.CallbackData,
			},
			StreamMessage s => new
			{
				kind = "stream",
				channel = m.Channel,
				level = m.Level,
				status = m.StatusCode,
				target = m.Target,
				actions = m.Actions,
				meta = m.Meta,
				streamId = s.StreamId,
				phase = s.Phase.ToString().ToLowerInvariant(),
				text = s.Text,
				hasBinary = s.HasBinary,
				contentType = s.ContentType,
			},
			_ => new { kind = "unknown", m.Level, status = m.StatusCode }
		};
	}
}

