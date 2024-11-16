using PLang.Errors;

namespace PLang.Utils;

public interface IAskUserDialog
{
    string ShowDialog(string text, string caption, bool isMultiline = false, int formWidth = 300, int formHeight = 200);
}

public interface IErrorDialog
{
    string ShowDialog(IError error, string caption, int formWidth = 500, int formHeight = 400);
}