using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PLangWindowForms
{
	public static class AskUserDialog
	{
		public static string ShowDialog(string text, string caption, bool isMultiline = false, int formWidth = 300, int formHeight = 200)
		{
			var prompt = new Form
			{
				Width = formWidth,
				Height = isMultiline ? formHeight : formHeight - 70,
				FormBorderStyle = isMultiline ? FormBorderStyle.Sizable : FormBorderStyle.FixedSingle,
				Text = caption,
				StartPosition = FormStartPosition.CenterScreen,
				MaximizeBox = isMultiline, 
				ShowIcon = false
			};

			var textLabel = new Label
			{
				Left = 10,
				Padding = new Padding(0, 3, 0, 0),
				Text = text,
				Dock = DockStyle.Top
			};

			var textBox = new TextBox
			{
				Left = isMultiline ? 50 : 4,
				Top = isMultiline ? 50 : textLabel.Height + 4,
				Multiline = isMultiline,
				Dock = isMultiline ? DockStyle.Fill : DockStyle.None,
				Width = prompt.Width - 24,
				Anchor = isMultiline ? AnchorStyles.Left | AnchorStyles.Top : AnchorStyles.Left | AnchorStyles.Right
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
			prompt.Controls.Add(textLabel);

			return prompt.ShowDialog() == DialogResult.OK ? textBox.Text : string.Empty;
		}
	}
}
