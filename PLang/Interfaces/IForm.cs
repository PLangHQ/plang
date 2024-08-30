namespace PLang.Interfaces
{
	public interface IForm
	{
		void SetSize(int width, int height);
		void SetIcon(string? icon);
		void SetTitle(string? title);
		Task ModifyContent(string cssSelector, string? content, string swapping = "innerHTML");
		Task ExecuteCode(string content);
		Task Flush();

		public SynchronizationContext SynchronizationContext { get; set; }
	}
}
