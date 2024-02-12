namespace PLang.Utils
{
	public interface IAskUserDialog
	{
		string ShowDialog(string text, string caption, bool isMultiline = false, int formWidth = 300, int formHeight = 200);
	}

	public interface IErrorDialog
	{
		string ShowDialog(Exception ex, string text, string caption, int formWidth = 300, int formHeight = 200);
	}
}
