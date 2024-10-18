using System.Globalization;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text;

namespace Diginsight.Analyzer.Entities;

[Serializable]
public sealed class AnalysisException : ApplicationException
{
    public AnalysisException(string message, HttpStatusCode statusCode, string label)
        : this(message, statusCode, label, (Exception?)null) { }

    public AnalysisException(string message, HttpStatusCode statusCode, string label, Exception? innerException)
        : base(message, innerException)
    {
        StatusCode = statusCode;
        Label = label;
        Parameters = Array.Empty<object?>();
    }

    public AnalysisException(
        ref InterpolatedStringHandler handler,
        HttpStatusCode statusCode,
        string label
    )
        : this(ref handler, statusCode, label, null) { }

    public AnalysisException(
        ref InterpolatedStringHandler handler,
        HttpStatusCode statusCode,
        string label,
        Exception? innerException
    )
        : base(handler.ToString(), innerException)
    {
        StatusCode = statusCode;
        Label = label;
        Parameters = handler.Parameters.ToArray();
    }

    public AnalysisException(string messageFormat, HttpStatusCode statusCode, string label, object?[] parameters)
        : this(messageFormat, statusCode, label, null, parameters) { }

    public AnalysisException(string messageFormat, HttpStatusCode statusCode, string label, Exception? innerException, object?[] parameters)
        : base(string.Format(messageFormat, parameters), innerException)
    {
        StatusCode = statusCode;
        Label = label;
        Parameters = parameters;
    }

    public HttpStatusCode StatusCode { get; }

    public string Label { get; }

    public object?[] Parameters { get; }

    [InterpolatedStringHandler]
    public readonly ref struct InterpolatedStringHandler
    {
        private readonly StringBuilder sb;
        private readonly ICollection<object?> parameters;

        public InterpolatedStringHandler(int literalLength, int formattedCount)
        {
            sb = new StringBuilder(literalLength);
            parameters = new List<object?>(formattedCount);
        }

        public IEnumerable<object?> Parameters => parameters;

        public void AppendLiteral(string str)
        {
            sb.Append(str);
        }

        public void AppendFormatted<T>(T obj)
        {
            sb.Append(obj);
            parameters.Add(obj);
        }

        public void AppendFormatted<T>(T obj, string? format)
            where T : IFormattable
        {
            sb.Append(obj.ToString(format, CultureInfo.InvariantCulture));
            parameters.Add(obj);
        }

        public override string ToString() => sb.ToString();
    }
}
