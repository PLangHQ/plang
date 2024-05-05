using Newtonsoft.Json;
using PLang.Errors;
using PLang.Utils;

namespace PLangWindowForms
{

	public class ErrorDialog : IErrorDialog
	{
		public string ShowDialog(IError error, string caption, int formWidth = 500, int formHeight = 400)
		{
			var prompt = new Form
			{
				Width = formWidth,
				Height = formHeight,
				FormBorderStyle = FormBorderStyle.Sizable,
				Text = caption,
				StartPosition = FormStartPosition.CenterScreen,
				MaximizeBox = true, 
				ShowIcon = false
			};

			var textBox = new TextBox
			{
				Left = 4,
				Top = 4,
				Multiline = true,
				Dock = DockStyle.Fill,
				Width = prompt.Width - 24,
				Anchor = AnchorStyles.Left | AnchorStyles.Top,
				Text = error.ToFormat().ToString()
			};

			var confirmationButton = new Button
			{
				Text = @"OK",
				Cursor = Cursors.Hand,
				DialogResult = DialogResult.OK,
				Dock = DockStyle.Bottom,
			};

			confirmationButton.Click += (sender, e) =>
			{
				prompt.Close();
			};

			prompt.Controls.Add(textBox);
			prompt.Controls.Add(confirmationButton);
			prompt.Focus();
			return prompt.ShowDialog() == DialogResult.OK ? "Ok" : string.Empty;
		}
	}
}
