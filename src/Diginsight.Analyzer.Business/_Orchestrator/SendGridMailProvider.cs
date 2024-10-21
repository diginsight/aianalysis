using Attachment = Diginsight.Analyzer.Business.Models.Attachment;

namespace Diginsight.Analyzer.Business;

internal sealed class SendGridMailProvider : IMailProvider
{
    private readonly ISendGridClient sendGridClient;

    public SendGridMailProvider(ISendGridClient sendGridClient)
    {
        this.sendGridClient = sendGridClient;
    }

    public async Task SendAsync(
        string from,
        IEnumerable<string> to,
        IEnumerable<string> cc,
        IEnumerable<string> bcc,
        string subject,
        string plainContent,
        string? htmlContent,
        IEnumerable<Attachment>? attachments
    )
    {
        SendGridMessage message = new ()
        {
            From = new EmailAddress(from),
            Subject = subject,
            PlainTextContent = plainContent,
            HtmlContent = htmlContent,
        };

        foreach (string x in to)
        {
            message.AddTo(x);
        }

        foreach (string x in cc)
        {
            message.AddCc(x);
        }

        foreach (string x in bcc)
        {
            message.AddBcc(x);
        }

        foreach (Attachment attachment in attachments ?? Enumerable.Empty<Attachment>())
        {
            message.AddAttachment(attachment.Filename, Convert.ToBase64String(attachment.Content), attachment.MimeType);
        }

        _ = await sendGridClient.SendEmailAsync(message);
    }
}
