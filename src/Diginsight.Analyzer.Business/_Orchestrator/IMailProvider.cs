using Diginsight.Analyzer.Business.Models;

namespace Diginsight.Analyzer.Business;

public interface IMailProvider
{
    Task SendAsync(
        string from,
        IEnumerable<string> to,
        IEnumerable<string> cc,
        IEnumerable<string> bcc,
        string subject,
        string plainContent,
        string? htmlContent = null,
        IEnumerable<Attachment>? attachments = null
    );
}
