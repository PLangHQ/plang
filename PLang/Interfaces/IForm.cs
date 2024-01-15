namespace PLang.Interfaces
{
	public interface IForm
	{
		void SetSize(int width, int height);
		void SetIcon(string? icon);
		void SetTitle(string? title);
	}
}
