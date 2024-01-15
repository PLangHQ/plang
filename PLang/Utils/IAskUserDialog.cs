namespace PLang.Utils
{
	public interface IAskUserDialog
	{
		string ShowDialog(string text, string caption, bool isMultiline = false, int formWidth = 300, int formHeight = 200);
	}
}
