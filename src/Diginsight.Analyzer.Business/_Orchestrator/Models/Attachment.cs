namespace Diginsight.Analyzer.Business.Models;

public sealed class Attachment
{
    public Attachment(string filename, byte[] content, string mimeType)
    {
        Filename = filename;
        Content = content;
        MimeType = mimeType;
    }

    public string Filename { get; }

    public byte[] Content { get; }

    public string MimeType { get; }
}
