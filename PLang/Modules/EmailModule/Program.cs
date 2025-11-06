using PLang.Errors;
using PLang.Errors.Runtime;

namespace PLang.Modules.EmailModule;

public class Program : BaseProgram
{


	public record EmailMessage(string FromEmail, List<string> ToEmails, string Subject, string Body, string TextBody,
		bool IsHtml = true, List<string>? Cc = null, List<string>? Bcc = null, string? ReplyTo = null, 
		Dictionary<string, string>? Headers = null, List<object>? Attachements = null, string Service = "email.plang.io");

	public async Task<(object?, IError?)> SendEmail(EmailMessage emailMessage)
	{
		if (emailMessage.ToEmails.Count == 0) return (null, new ProgramError("To email address cannot be empty"));
		if (string.IsNullOrEmpty(emailMessage.Body) && string.IsNullOrEmpty(emailMessage.TextBody)) return (null, new ProgramError("The body of the email cannot be empty"));
		
		GroupedErrors groupedErrors = new();
		foreach (var toEmail in emailMessage.ToEmails)
		{
			if (!toEmail.Contains("@")) groupedErrors.Add(new ProgramError("Email must contain @ sign", FixSuggestion: $"The email {toEmail} didn't contain @ sign"));
		}
		if (groupedErrors.Count > 0) return (null, groupedErrors);

		Dictionary<string, object?> parameters = new();
		parameters.Add("EmailMessage", emailMessage);
		var caller = GetProgramModule<AppModule.Program>();
		var result = await caller.RunApp(new Models.AppToCallInfo("Email", "SendEmail", parameters));

		return result;

	}
}

