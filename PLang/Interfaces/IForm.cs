namespace PLang.Interfaces
{
	public interface IForm
	{
		void SetSize(int width, int height);
		void SetIcon(string? icon);
		void SetTitle(string? title);
		Task BufferContent(object? obj);
		Task AppendContent(string cssSelector, string? content);
		Task ExecuteCode(string content);
		Task Flush();

		public SynchronizationContext SynchronizationContext { get; set; }
	}
}
