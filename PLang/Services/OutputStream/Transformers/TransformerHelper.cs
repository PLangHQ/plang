using Newtonsoft.Json;
using PLang.Errors;
using PLang.Interfaces;
using PLang.Services.OutputStream.Messages;
using PLang.Utils;
using System.Collections.Concurrent;
using System.Diagnostics;
using static PLang.Utils.StepHelper;

namespace PLang.Services.OutputStream.Transformers
{
	public class TransformerHelper
	{

		public static async Task<SemaphoreSlim> GetGate(ConcurrentDictionary<string, object?> SharedItems, CancellationToken ct)
		{
			var gate = (SemaphoreSlim)SharedItems.GetOrAdd("__plang_gatekey", _ => new SemaphoreSlim(1, 1));

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


		public static (object?, IError?) BuildEnvelope(OutMessage m, Interfaces.PLangContext context)
		{
			try
			{
				var envelope = m switch
				{
					TextMessage t => new Dictionary<string, object?>
					{
						["id"] = context.Id,
						["kind"] = "text",
						["channel"] = m.Channel,
						["level"] = m.Level,
						["status"] = m.StatusCode,
						["target"] = m.Target,
						["actions"] = m.Actions,
						["content"] = t.Content,
						["skipNewLine"] = t.SkipNewline,
					},
					RenderMessage r => new Dictionary<string, object?>
					{
						["id"] = context.Id,
						["kind"] = "render",
						["channel"] = m.Channel,
						["level"] = m.Level,
						["status"] = m.StatusCode,
						["target"] = m.Target,
						["actions"] = m.Actions,
						["content"] = r.Content,
					},
					ExecuteMessage e => new Dictionary<string, object?>
					{
						["id"] = context.Id,
						["kind"] = "execute",
						["channel"] = m.Channel,
						["level"] = m.Level,
						["status"] = m.StatusCode,
						["target"] = m.Target,
						["actions"] = m.Actions,
						["function"] = e.Function,
						["data"] = e.Data
					},
					AskMessage a => new Dictionary<string, object?>
					{
						["id"] = context.Id,
						["kind"] = "ask",
						["channel"] = m.Channel,
						["level"] = m.Level,
						["status"] = m.StatusCode,
						["target"] = m.Target,
						["actions"] = m.Actions,
						["content"] = a.Content,
						["callback"] = a.Callback
					},

					ErrorMessage a => new Dictionary<string, object?>
					{
						["id"] = context.Id,
						["kind"] = "error",
						["channel"] = m.Channel,
						["key"] = a.Key,
						["level"] = m.Level,
						["status"] = m.StatusCode,
						["target"] = m.Target,
						["actions"] = m.Actions,
						["content"] = a.Content,
						["fixSuggestion"] = a.FixSuggestion,
						["helpfullLinks"] = a.HelpfullLinks,
						["callback"] = a.Callback
					},

					StreamMessage s => new Dictionary<string, object?>
					{
						["id"] = context.Id,
						["kind"] = "stream",
						["channel"] = m.Channel,
						["level"] = m.Level,
						["status"] = m.StatusCode,
						["target"] = m.Target,
						["actions"] = m.Actions,
						["meta"] = m.Properties,
						["streamId"] = s.StreamId,
						["phase"] = s.Phase.ToString().ToLowerInvariant(),
						["text"] = s.Text,
						["hasBinary"] = s.HasBinary,
						["bytes"] = s.Bytes != null ? Convert.ToBase64String(s.Bytes.Value.Span) : null,
						["fileName"] = s.FileName,
						["contentType"] = s.ContentType,
					},
					_ => new Dictionary<string, object?>
					{
						["id"] = context.Id,
						["kind"] = "unknown",
						["level"] = m.Level,
						["status"] = m.StatusCode
					}
				};
				
				if (context.DebugMode)
				{
					envelope["debug"] = GetDebugInfo(context);
				}

				return (envelope, null);
			}
			catch (Exception ex)
			{
				var envelope = new Dictionary<string, object?>
				{
					["id"] = context.Id,
					["kind"] = "error",
					["channel"] = "default",
					["key"] = "CriticalError",
					["level"] = "error",
					["status"] = 500,
					["content"] = $"",
				};

				if (context.DebugMode)
				{
					envelope["debug"] = GetDebugInfo(context);
				}

				return (null, new Error($"Message:{ex.Message}\nOutputMessage:{JsonConvert.SerializeObject(m)}\nException:{ex}"));
			}

			
		}

		private static object? GetDebugInfo(PLangContext context)
		{
			if (context.CallingStep == null) return null;

			var step = context.CallingStep;
			var goal = step.Goal;

			return new
			{
				goal = new { name = goal.GoalName, path = goal.RelativeGoalPath, absolutePath = goal.AbsoluteGoalPath },
				step = new { text = step.Text, step.Stopwatch, line = context.CallingStep.LineNumber }
			};
		}
	}
}

