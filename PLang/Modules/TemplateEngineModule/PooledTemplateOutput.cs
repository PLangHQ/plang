using Scriban;
using Scriban.Runtime;
using System.Buffers;

namespace PLang.Modules.TemplateEngineModule
{
	/// <summary>
	/// Collects what a template writes in a char[] rented from ArrayPool, so a render allocates the result
	/// string and nothing else of the size of the page.
	///
	/// Template.RenderAsync uses a new StringBuilder per render: the page is first copied into its chunks
	/// (the chunk arrays are garbage afterwards) and then copied again by StringBuilder.ToString(). Measured
	/// with a 220,000 char page: 1.46 MB allocated per render with RenderAsync, 0.51 MB with this class (the
	/// result string is about 0.44 MB of that). A front page is rendered at least twice (content, then the
	/// layout around it), and both strings are large object heap sized, so this also takes load off gen2.
	/// </summary>
	internal sealed class PooledTemplateOutput : IScriptOutput, IDisposable
	{
		// Buffers up to this many chars come from the shared pool. A larger one is a plain array the GC takes back:
		// the shared pool keeps what is returned to it per core and per thread, so the buffers of one 42 MB render
		// (every size from 4K to 64M chars, about 250 MB) stayed in the process after each render on a new thread.
		internal const int MaxPooledLength = 1024 * 1024;

		private char[]? buffer = Rent(4096);
		private int length;

		private static char[] Rent(int minimumLength) =>
			minimumLength <= MaxPooledLength ? ArrayPool<char>.Shared.Rent(minimumLength) : new char[minimumLength];

		private static void Return(char[] array)
		{
			if (array.Length <= MaxPooledLength) ArrayPool<char>.Shared.Return(array);
		}

		public void Write(string text, int offset, int count)
		{
			if (count <= 0) return;
			var current = buffer ?? throw new ObjectDisposedException(nameof(PooledTemplateOutput));
			if (length + count > current.Length)
			{
				var bigger = Rent(Math.Max(current.Length * 2, length + count));
				Array.Copy(current, bigger, length);
				Return(current);
				buffer = current = bigger;
			}
			text.CopyTo(offset, current, length, count);
			length += count;
		}

		public ValueTask WriteAsync(string text, int offset, int count, CancellationToken cancellationToken)
		{
			Write(text, offset, count);
			return ValueTask.CompletedTask;
		}

		public override string ToString() => new string(buffer!, 0, length);

		public void Dispose()
		{
			var current = buffer;
			buffer = null;
			if (current != null) Return(current);
		}

		/// <summary>
		/// Same result as <c>template.RenderAsync(context)</c>, written through a pooled buffer. A template with
		/// parse errors is handed to RenderAsync, which throws the same exception as before.
		/// </summary>
		public static async ValueTask<string> RenderAsync(Template template, TemplateContext context)
		{
			if (template.HasErrors || template.Page == null) return await template.RenderAsync(context);

			using var output = new PooledTemplateOutput();
			context.PushOutput(output);
			try
			{
				await context.EvaluateAsync(template.Page);
			}
			finally
			{
				context.PopOutput();
			}
			return output.ToString();
		}
	}
}
